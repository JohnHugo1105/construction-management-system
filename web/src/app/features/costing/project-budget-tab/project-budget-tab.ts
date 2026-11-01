import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
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
import { costSummaryExportUrl, budgetLinesExportUrl } from '../costing-exports';
import { CostingReferenceState } from '../costing-reference';
import { BudgetLine } from '../costing.models';
import { describeBasis } from '../figures';
import { CostPosition } from '../ui/cost-position';
import { FilterToggle } from '../ui/filter-toggle';
import { LineDialog } from '../ui/line-dialog';
import { ReasonDialog } from '../ui/reason-dialog';
import { BudgetLinesList } from './budget-lines-list';
import { BudgetState } from './budget-state';
import { CostBreakdown } from './cost-breakdown';

type DialogState = { readonly kind: 'add' } | { readonly kind: 'edit'; readonly line: BudgetLine };

/**
 * A project's Budget tab: where the project stands (budget, spent, remaining, health, margins), where the money goes, and the
 * budget lines with filters and totals. People with `costs.write` can add, edit and remove lines unless the project is
 * archived; everyone else sees the same figures without the buttons.
 */
@Component({
  selector: 'app-project-budget-tab',
  imports: [
    RouterLink,
    ApiErrorAlert,
    Button,
    Control,
    EmptyState,
    FormField,
    Icon,
    Pager,
    SearchBox,
    Skeleton,
    CostPosition,
    FilterToggle,
    LineDialog,
    ReasonDialog,
    BudgetLinesList,
    CostBreakdown,
  ],
  providers: [BudgetState],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './project-budget-tab.html',
  styleUrls: ['../ui/list-chrome.scss', './project-budget-tab.scss'],
})
export class ProjectBudgetTab {
  protected readonly state = inject(BudgetState);
  protected readonly context = inject(ProjectContext);
  protected readonly referenceState = inject(CostingReferenceState);
  private readonly session = inject(SessionState);
  private readonly api = inject(CostingApi);
  private readonly toast = inject(ToastService);

  /** Holds costs.write (an archived project still turns writing off, below). */
  private readonly mayWrite = this.session.permit(Permission.costsWrite);
  /** A locked budget changes only through change orders, so the line controls give way to one line saying so. */
  protected readonly budgetLocked = computed(
    () => this.context.project().budgetBaselineLocked === true,
  );
  protected readonly canWrite = computed(
    () => this.mayWrite() && !this.context.isArchived() && !this.budgetLocked(),
  );
  /** Says why the buttons are missing, to the people who would otherwise have them. */
  protected readonly archivedNote = computed(() => this.mayWrite() && this.context.isArchived());
  protected readonly lockedNote = computed(
    () => this.mayWrite() && !this.context.isArchived() && this.budgetLocked(),
  );
  protected readonly canSeeChanges = this.session.permit(Permission.changeOrdersRead);

  protected readonly filtersOpen = signal(this.state.activeFilterCount() > 0);
  protected readonly dialog = signal<DialogState | null>(null);
  protected readonly removing = signal<BudgetLine | null>(null);

  protected readonly subProjects = this.context.subProjects;
  protected readonly divisions = this.referenceState.divisions;
  protected readonly itemTypes = this.referenceState.itemTypes;

  protected readonly basisLine = computed(() => {
    const summary = this.state.summary();
    return summary ? describeBasis(summary.basis) : '';
  });

  /** Anything to show about the project's money: something budgeted, or something spent. */
  protected readonly hasPosition = computed(() => {
    const summary = this.state.summary();
    return summary !== undefined && summary.position.health !== 'NoActivity';
  });

  /** The margins, worked out by the server, when the project has a contract price. */
  protected readonly margins = computed(() => {
    const summary = this.state.summary();
    if (!summary || summary.contractPriceMinor === null) return null;
    const currency = this.session.currencyCode();
    const show = (minor: number | null) => ({
      text: minor === null ? '' : formatMoney(minor, currency),
      negative: minor !== null && minor < 0,
    });
    return {
      soFar: show(summary.marginAfterActualMinor),
      onBudget: show(summary.budgetMarginMinor),
      vatNote: summary.basis === 'ExcludingVat',
    };
  });

  /** No budget line yet and nothing filtered away: the screen says so once, with the one next step. */
  protected readonly noBudgetYet = computed(
    () =>
      !this.state.loadingFirst() &&
      !this.state.error() &&
      this.state.result() !== undefined &&
      this.state.total() === 0 &&
      !this.state.query.isFiltered(),
  );

  protected readonly totalsLabel = computed(() =>
    this.state.query.isFiltered() ? 'Matching total with VAT' : 'Total with VAT',
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

  protected readonly budgetCsv = computed(() => {
    const id = this.context.projectId();
    return id ? budgetLinesExportUrl(id, this.state.filters()) : '';
  });
  protected readonly summaryCsv = computed(() => {
    const id = this.context.projectId();
    return id ? costSummaryExportUrl(id) : '';
  });

  protected readonly anyRemovedShown = computed(() =>
    this.state.lines().some((line) => line.status === 'Removed'),
  );

  constructor() {
    void this.referenceState.load();
  }

  protected openAdd(): void {
    this.dialog.set({ kind: 'add' });
  }

  protected openEdit(line: BudgetLine): void {
    this.dialog.set({ kind: 'edit', line });
  }

  protected editing(): BudgetLine | null {
    const state = this.dialog();
    return state?.kind === 'edit' ? state.line : null;
  }

  /** The dialog saved a line: close it and fetch the lines and the position again. */
  protected saved(): void {
    this.dialog.set(null);
    this.state.reload();
  }

  protected readonly performRemove = (reason: string) => {
    const line = this.removing();
    if (!line) throw new Error('No budget line is being removed.');
    return this.api.removeBudgetLine(line.id, reason, line.version);
  };

  protected removed(): void {
    this.removing.set(null);
    this.toast.success('Budget line removed.');
    this.state.reload();
  }

  protected removalOutdated(): void {
    this.removing.set(null);
    this.state.reload();
  }

  protected setDivision(value: string): void {
    this.state.query.set({ division: Number(value) || 0 });
  }

  protected setItemType(value: string): void {
    this.state.query.set({ itemType: value as '' | BudgetLine['itemType'] });
  }

  protected setSubProject(value: string): void {
    this.state.query.set({ subProject: value });
  }
}
