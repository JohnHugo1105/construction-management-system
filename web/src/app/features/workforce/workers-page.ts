import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ToastService } from '../../core/notifications/toast-service';
import { Permission } from '../../core/session/permissions';
import { SessionState } from '../../core/session/session-state';
import { ApiErrorAlert } from '../../shared/ui/api-error-alert/api-error-alert';
import { Button } from '../../shared/ui/button/button';
import { EmptyState } from '../../shared/ui/empty-state/empty-state';
import { Icon } from '../../shared/ui/icon/icon';
import { PageHeader } from '../../shared/ui/page-header/page-header';
import { Pager } from '../../shared/ui/pager/pager';
import { SearchBox } from '../../shared/ui/search-box/search-box';
import { Skeleton } from '../../shared/ui/skeleton/skeleton';
import { StatusChip } from '../../shared/ui/status-chip/status-chip';
import { Viewport } from '../../shared/ui/viewport/viewport';
import { AssignDialog, AssignTarget } from './assign-dialog';
import { injectProjectChoices } from './project-choices';
import { WORKER_FILTERS, WORKER_STATUS, filterLabel } from './worker-status';
import { WorkersState } from './workers-state';

/**
 * The worker list: search, status chips, a project filter, a table on wide screens and cards on phones, paged by the server.
 * It never shows pay. People who manage workers can tick several and post them to a project in one step.
 */
@Component({
  selector: 'app-workers-page',
  imports: [
    RouterLink,
    PageHeader,
    SearchBox,
    Button,
    Icon,
    StatusChip,
    Pager,
    EmptyState,
    Skeleton,
    ApiErrorAlert,
    AssignDialog,
  ],
  providers: [WorkersState],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './workers-page.html',
  styleUrl: './workers-page.scss',
})
export class WorkersPage {
  protected readonly state = inject(WorkersState);
  protected readonly viewport = inject(Viewport);
  private readonly session = inject(SessionState);
  private readonly toast = inject(ToastService);
  protected readonly choices = injectProjectChoices();

  protected readonly canManage = this.session.permit(Permission.workforceManage);
  protected readonly filters = WORKER_FILTERS;
  protected readonly filterLabel = filterLabel;

  /** The workers ticked for a crew posting: id and the version the list showed. */
  protected readonly selected = signal<
    ReadonlyMap<string, AssignTarget & { readonly name: string }>
  >(new Map());
  protected readonly assigning = signal(false);

  /** Only worth offering when there is more than one project to tell apart. */
  protected readonly showProjectFilter = computed(
    () => this.choices.projects().length > 1 || this.state.query.values().project !== '',
  );

  /** There are no workers at all, so there is nothing to search or filter yet: the page offers the one next step. */
  protected readonly nothingYet = computed(
    () =>
      !this.state.loadingFirst() &&
      !this.state.error() &&
      this.state.total() === 0 &&
      !this.state.query.isFiltered(),
  );

  protected readonly rows = computed(() =>
    this.state.items().map((worker) => ({
      id: worker.id,
      name: worker.fullName,
      number: worker.employeeNumber,
      designation: worker.designation ?? 'No designation',
      status: WORKER_STATUS[worker.status],
      project: worker.currentProjectName ?? 'Not posted to a project',
      posted: worker.currentProjectName !== null,
      version: worker.version,
    })),
  );

  protected readonly selectedCount = computed(() => this.selected().size);
  protected readonly selectedLabel = computed(() => {
    const count = this.selectedCount();
    return count === 1 ? '1 worker selected' : `${count} workers selected`;
  });
  protected readonly allOnPageSelected = computed(
    () => this.rows().length > 0 && this.rows().every((row) => this.selected().has(row.id)),
  );
  protected readonly targets = computed<readonly AssignTarget[]>(() =>
    Array.from(this.selected().values()).map(({ id, version }) => ({ id, version })),
  );
  protected readonly targetSummary = computed(() => {
    const picked = Array.from(this.selected().values());
    const first = picked[0];
    return picked.length === 1 && first ? first.name : `${picked.length} workers`;
  });

  protected isSelected(id: string): boolean {
    return this.selected().has(id);
  }

  protected toggle(row: { id: string; name: string; version: number }, on: boolean): void {
    const next = new Map(this.selected());
    if (on) next.set(row.id, { id: row.id, name: row.name, version: row.version });
    else next.delete(row.id);
    this.selected.set(next);
  }

  protected toggleAll(on: boolean): void {
    const next = new Map(this.selected());
    for (const row of this.rows()) {
      if (on) next.set(row.id, { id: row.id, name: row.name, version: row.version });
      else next.delete(row.id);
    }
    this.selected.set(next);
  }

  protected clearSelection(): void {
    this.selected.set(new Map());
  }

  protected assigned(): void {
    this.assigning.set(false);
    this.clearSelection();
    this.toast.success('Workers assigned.');
    this.state.reload();
  }

  protected isChecked(event: Event): boolean {
    return (event.target as HTMLInputElement).checked;
  }

  protected projectFilter(event: Event): void {
    this.state.query.set({ project: (event.target as HTMLSelectElement).value });
  }
}
