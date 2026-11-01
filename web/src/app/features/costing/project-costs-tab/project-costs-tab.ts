import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { ToastService } from '../../../core/notifications/toast-service';
import { Permission } from '../../../core/session/permissions';
import { SessionState } from '../../../core/session/session-state';
import { formatMoney } from '../../../shared/presentation/money';
import { ApiErrorAlert } from '../../../shared/ui/api-error-alert/api-error-alert';
import { Button } from '../../../shared/ui/button/button';
import { EmptyState } from '../../../shared/ui/empty-state/empty-state';
import { Control } from '../../../shared/ui/form-field/control';
import { FormField } from '../../../shared/ui/form-field/form-field';
import { Icon } from '../../../shared/ui/icon/icon';
import { Pager } from '../../../shared/ui/pager/pager';
import { SearchBox } from '../../../shared/ui/search-box/search-box';
import { Skeleton } from '../../../shared/ui/skeleton/skeleton';
import { ProjectContext } from '../../projects/project-context';
import { CostingApi } from '../costing-api';
import { costEntriesExportUrl } from '../costing-exports';
import { CostingReferenceState } from '../costing-reference';
import { CostEntry } from '../costing.models';
import { COST_ORIGINS } from '../list-fields';
import { presentOrigin } from '../costing-labels';
import { FilterToggle } from '../ui/filter-toggle';
import { ExistingInvoice, LineDialog } from '../ui/line-dialog';
import { ReasonDialog } from '../ui/reason-dialog';
import { CostEntriesList } from './cost-entries-list';
import { CostsState } from './costs-state';

type DialogState = { readonly kind: 'add' } | { readonly kind: 'edit'; readonly entry: CostEntry };

/**
 * A project's Costs tab: what the project has actually spent, with filters and the totals of everything that matches. Entries
 * made by payroll or stock say where to correct them. People with `costs.write` can record, edit and void manual costs unless
 * the project is archived; everyone else sees the same entries without the buttons.
 */
@Component({
  selector: 'app-project-costs-tab',
  imports: [
    ApiErrorAlert,
    Button,
    Control,
    EmptyState,
    FormField,
    Icon,
    Pager,
    SearchBox,
    Skeleton,
    FilterToggle,
    LineDialog,
    ReasonDialog,
    CostEntriesList,
  ],
  providers: [CostsState],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './project-costs-tab.html',
  styleUrls: ['../ui/list-chrome.scss', './project-costs-tab.scss'],
})
export class ProjectCostsTab {
  protected readonly state = inject(CostsState);
  protected readonly context = inject(ProjectContext);
  protected readonly referenceState = inject(CostingReferenceState);
  private readonly session = inject(SessionState);
  private readonly api = inject(CostingApi);
  private readonly toast = inject(ToastService);

  /** Holds costs.write (an archived project still turns writing off, below). */
  private readonly mayWrite = this.session.permit(Permission.costsWrite);
  protected readonly canWrite = computed(() => this.mayWrite() && !this.context.isArchived());
  /** Says why the buttons are missing, to the people who would otherwise have them. */
  protected readonly archivedNote = computed(() => this.mayWrite() && this.context.isArchived());

  protected readonly filtersOpen = signal(this.state.activeFilterCount() > 0);
  protected readonly dialog = signal<DialogState | null>(null);
  protected readonly voiding = signal<CostEntry | null>(null);

  protected readonly subProjects = this.context.subProjects;
  protected readonly divisions = this.referenceState.divisions;
  protected readonly itemTypes = this.referenceState.itemTypes;
  protected readonly origins = COST_ORIGINS.map((origin) => ({
    value: origin,
    label: presentOrigin(origin).label,
  }));

  /** No entry at all and nothing filtered away: the screen says so once, with the one next step. */
  protected readonly noCostsYet = computed(
    () =>
      !this.state.loadingFirst() &&
      !this.state.error() &&
      this.state.result() !== undefined &&
      this.state.total() === 0 &&
      !this.state.query.isFiltered(),
  );

  protected readonly totalsLabel = computed(() =>
    this.state.query.isFiltered() ? 'Matching total with VAT' : 'Total spent with VAT',
  );
  protected readonly totalsView = computed(() => {
    const totals = this.state.totals();
    if (!totals) return null;
    const currency = this.session.currencyCode();
    return {
      gross: formatMoney(totals.grossMinor, currency),
      net: formatMoney(totals.netMinor, currency),
      vat: formatMoney(totals.vatMinor, currency),
    };
  });

  protected readonly costsCsv = computed(() => {
    const id = this.context.projectId();
    return id ? costEntriesExportUrl(id, this.state.filters()) : '';
  });

  protected readonly anyVoidedShown = computed(() =>
    this.state.entries().some((entry) => entry.status === 'Voided'),
  );

  constructor() {
    void this.referenceState.load();
  }

  protected openAdd(): void {
    this.dialog.set({ kind: 'add' });
  }

  protected openEdit(entry: CostEntry): void {
    this.dialog.set({ kind: 'edit', entry });
  }

  protected editing(): CostEntry | null {
    const state = this.dialog();
    return state?.kind === 'edit' ? state.entry : null;
  }

  /** The dialog saved an entry: close it and fetch the list again. */
  protected saved(): void {
    this.dialog.set(null);
    this.state.reload();
  }

  /** The person chose to look at the entry that already holds the invoice: the list shows it. */
  protected showExisting(invoice: ExistingInvoice): void {
    this.dialog.set(null);
    this.filtersOpen.set(invoice.date !== null);
    this.state.showExisting(invoice);
  }

  protected readonly performVoid = (reason: string) => {
    const entry = this.voiding();
    if (!entry) throw new Error('No cost is being voided.');
    return this.api.voidCostEntry(entry.id, reason, entry.version);
  };

  protected voided(): void {
    this.voiding.set(null);
    this.toast.success('Cost voided.');
    this.state.reload();
  }

  protected voidOutdated(): void {
    this.voiding.set(null);
    this.state.reload();
  }

  protected setDivision(value: string): void {
    this.state.query.set({ division: Number(value) || 0 });
  }

  protected setItemType(value: string): void {
    this.state.query.set({ itemType: value as '' | CostEntry['itemType'] });
  }

  protected setOrigin(value: string): void {
    this.state.query.set({ origin: value as '' | CostEntry['origin'] });
  }

  protected setSubProject(value: string): void {
    this.state.query.set({ subProject: value });
  }
}
