import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { ToastService } from '../../../core/notifications/toast-service';
import { Permission } from '../../../core/session/permissions';
import { SessionState } from '../../../core/session/session-state';
import { Alert } from '../../../shared/ui/alert/alert';
import { ApiErrorAlert } from '../../../shared/ui/api-error-alert/api-error-alert';
import { Button } from '../../../shared/ui/button/button';
import { EmptyState } from '../../../shared/ui/empty-state/empty-state';
import { Icon } from '../../../shared/ui/icon/icon';
import { Pager } from '../../../shared/ui/pager/pager';
import { Skeleton } from '../../../shared/ui/skeleton/skeleton';
import { ProjectContext } from '../../projects/project-context';
import { DocumentAddDialog } from '../document-add-dialog';
import { DocumentDetailsDialog } from '../document-details-dialog';
import { DocumentVersionsDialog } from '../document-versions-dialog';
import { DocumentVoidDialog } from '../document-void-dialog';
import { DocumentsApi } from '../documents-api';
import {
  CATEGORY_CHOICES,
  categoryChoices,
  documentActions,
  documentChip,
  groupDocuments,
  openWords,
  versionLine,
} from '../documents-presentation';
import { DocumentCategory, DocumentListItem } from '../documents.models';
import { DocumentRow, DocumentRowView } from './document-row';
import { ProjectDocumentsState } from './project-documents-state';

type Step = 'add' | 'version' | 'details' | 'void' | 'versions';

interface OpenStep {
  readonly step: Step;
  readonly document: DocumentListItem | null;
}

/**
 * The Documents tab of a project: drawings, permits, contracts, certificates, photos and reports, grouped by kind, each with its newest
 * version to download or open, a chip for a valid-until date that is near or past, and the older versions one step away. "Add document"
 * is the one primary action, for people who manage documents on a project that is not archived. Contracts and insurance policies reveal
 * prices, so the server leaves them out for anyone who cannot see costs. When file storage is not working, the add dialog says so and
 * offers a link instead.
 */
@Component({
  selector: 'app-project-documents-tab',
  imports: [
    Alert,
    ApiErrorAlert,
    Button,
    EmptyState,
    Icon,
    Pager,
    Skeleton,
    DocumentRow,
    DocumentAddDialog,
    DocumentDetailsDialog,
    DocumentVersionsDialog,
    DocumentVoidDialog,
  ],
  providers: [ProjectDocumentsState],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './project-documents-tab.html',
  styleUrl: './project-documents-tab.scss',
})
export class ProjectDocumentsTab {
  protected readonly context = inject(ProjectContext);
  protected readonly state = inject(ProjectDocumentsState);
  private readonly session = inject(SessionState);
  private readonly api = inject(DocumentsApi);
  private readonly toast = inject(ToastService);

  private readonly canManage = this.session.permit(Permission.documentsManage);
  protected readonly canAdd = computed(() => this.canManage() && !this.context.isArchived());

  protected readonly open = signal<OpenStep | null>(null);

  protected readonly kinds = computed(() =>
    categoryChoices(this.session.can(Permission.costsRead)),
  );
  protected readonly kind = computed(() => this.state.query.values().kind);

  /** The kind filter appears once there is more than one page to look through, or while a kind is chosen. */
  protected readonly showFilter = computed(
    () => this.kind() !== '' || this.state.total() > this.state.pageSize,
  );

  protected readonly groups = computed(() => {
    const zone = this.session.timeZoneId();
    const archived = this.context.isArchived();
    const canManage = this.canManage();
    return groupDocuments(this.state.items()).map((group) => ({
      ...group,
      rows: group.items.map((item): DocumentRowView => {
        const download = item.current.kind === 'File';
        return {
          item,
          chip: documentChip(item),
          meta: versionLine(item.current, zone),
          open: openWords(item.current),
          openLabel: `${openWords(item.current)} ${item.title}`,
          download,
          href: download ? this.api.fileUrl(item.id, item.current.number) : (item.current.url ?? ''),
          earlier: item.current.number > 1,
          can: documentActions(item, canManage, archived),
        };
      }),
    }));
  });

  protected readonly activeGroups = computed(() => this.groups().filter((group) => group.key !== 'void'));
  protected readonly voidGroup = computed(() => this.groups().find((group) => group.key === 'void') ?? null);

  protected readonly emptyTitle = computed(() => {
    const kind = this.kind();
    if (kind) {
      const heading = CATEGORY_CHOICES.find((choice) => choice.value === kind)?.heading ?? 'documents';
      return `No ${heading.toLowerCase()} on this project`;
    }
    return 'No documents yet';
  });
  protected readonly emptyMessage = computed(() => {
    if (this.kind()) return 'Choose another kind, or show every document.';
    if (this.canAdd()) return 'Keep drawings, permits, contracts, photos and reports here, as a file or a link.';
    return 'Documents appear here once someone adds them.';
  });

  protected setKind(value: DocumentCategory | ''): void {
    this.state.query.set({ kind: value });
  }

  protected chooseKind(event: Event): void {
    const value = (event.target as HTMLSelectElement).value;
    this.setKind(CATEGORY_CHOICES.some((choice) => choice.value === value) ? (value as DocumentCategory) : '');
  }

  protected start(step: Step, document: DocumentListItem | null = null): void {
    this.open.set({ step, document });
  }

  protected close(): void {
    this.open.set(null);
  }

  protected done(message: string): void {
    this.open.set(null);
    this.state.reload();
    this.toast.success(message);
  }

  /** The record changed while a dialog was open: close it and show the latest list. */
  protected reloadAll(): void {
    this.open.set(null);
    this.state.reload();
  }
}
