import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  signal,
  untracked,
} from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { ApiError, asApiError } from '../../../core/api/api-error';
import { DEFAULT_PAGE_SIZE } from '../../../core/api/paged-result';
import { ToastService } from '../../../core/notifications/toast-service';
import { Permission } from '../../../core/session/permissions';
import { SessionState } from '../../../core/session/session-state';
import { formatDate } from '../../../shared/presentation/business-time';
import { formatMoney } from '../../../shared/presentation/money';
import { formatBps } from '../../../shared/presentation/percent';
import { ApiErrorAlert } from '../../../shared/ui/api-error-alert/api-error-alert';
import { Button } from '../../../shared/ui/button/button';
import { EmptyState } from '../../../shared/ui/empty-state/empty-state';
import { Icon } from '../../../shared/ui/icon/icon';
import { Pager } from '../../../shared/ui/pager/pager';
import { Skeleton } from '../../../shared/ui/skeleton/skeleton';
import { StatusChip } from '../../../shared/ui/status-chip/status-chip';
import { Viewport } from '../../../shared/ui/viewport/viewport';
import { ProjectBondsCard } from '../../bonds/project-bonds-card/project-bonds-card';
import { ProjectContext } from '../../projects/project-context';
import { BillFormDialog, BillFormContext } from '../bill-form-dialog';
import { BillingApi } from '../billing-api';
import { BILL_KIND, billName, billStatus } from '../billing-rules';
import { Bill, BillKind, BillingTerms, ProjectBilling } from '../billing.models';
import { TermsDialog } from '../terms-dialog';

/**
 * "10% retention, 2% withholding, payment due in 30 days": the terms in one line. A down payment, VAT withheld and a government
 * client are named only when they apply.
 */
export function termsSummary(terms: BillingTerms): string {
  const retention =
    terms.retentionBps === 0 ? 'no retention' : `${formatBps(terms.retentionBps)} retention`;
  const withholding =
    terms.creditableWithholdingBps === 0
      ? 'no withholding'
      : `${formatBps(terms.creditableWithholdingBps)} withholding`;
  const due =
    terms.paymentTermsDays === 0
      ? 'payment due on billing'
      : `payment due in ${terms.paymentTermsDays} day${terms.paymentTermsDays === 1 ? '' : 's'}`;
  const parts = [retention, withholding];
  if (terms.vatWithholdingBps > 0) parts.push(`${formatBps(terms.vatWithholdingBps)} VAT withheld`);
  if (terms.advanceBps > 0) parts.push(`${formatBps(terms.advanceBps)} down payment`);
  parts.push(due);
  if (terms.isGovernmentClient) parts.push('government client');
  return parts.join(', ');
}

/** What the folded terms explain, one line per term that applies. */
export function termsNotes(terms: BillingTerms): string[] {
  const notes = [
    'Each progress bill holds back the retention until the project is completed. Withholding tax is taken from the amount before VAT, and the client gives a certificate for it.',
  ];
  if (terms.vatWithholdingBps > 0)
    notes.push(
      'The VAT the client withholds is also taken from the amount before VAT, with its own certificate.',
    );
  if (terms.advanceBps > 0)
    notes.push(
      'The down payment is billed before the first progress bill. Each progress bill then recovers it in proportion to the work billed, so it is all recovered when the work reaches the contract price.',
    );
  if (terms.isGovernmentClient)
    notes.push(
      'A government client allows a down payment of up to 15%, and added work over 10% of the original contract price is flagged on the change order.',
    );
  return notes;
}

/**
 * A project's billing: the money line (contract price, billed to date, collected, still to collect, retention held, and the down
 * payment still to recover once one is billed), the bills with their state, and the one next step ("Bill the down payment" while
 * the terms take one and none exists, otherwise "New bill" for people who prepare bills). Terms are folded away until needed. An
 * archived project is read-only and says so in one line. Every figure is the server's.
 */
@Component({
  selector: 'app-project-billing-tab',
  imports: [
    RouterLink,
    ApiErrorAlert,
    Button,
    EmptyState,
    Icon,
    Pager,
    Skeleton,
    StatusChip,
    BillFormDialog,
    ProjectBondsCard,
    TermsDialog,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './project-billing-tab.html',
  styleUrl: './project-billing-tab.scss',
})
export class ProjectBillingTab {
  private readonly context = inject(ProjectContext);
  private readonly api = inject(BillingApi);
  private readonly session = inject(SessionState);
  private readonly router = inject(Router);
  private readonly toast = inject(ToastService);
  protected readonly viewport = inject(Viewport);

  protected readonly billing = signal<ProjectBilling | null>(null);
  protected readonly error = signal<ApiError | null>(null);
  protected readonly loading = signal(true);
  protected readonly page = signal(1);
  protected readonly newBill = signal<BillKind | null>(null);
  protected readonly editingTerms = signal(false);

  protected readonly isArchived = this.context.isArchived;
  protected readonly projectId = this.context.projectId;
  private readonly canManage = computed(
    () => this.session.can(Permission.billingManage) && !this.isArchived(),
  );

  protected readonly view = computed(() => {
    const billing = this.billing();
    if (!billing) return null;
    const currency = this.session.currencyCode();
    const money = (minor: number): string => formatMoney(minor, currency);
    const hasPrice = billing.contractPriceMinor !== null && billing.contractPriceMinor > 0;
    const hasBills = billing.bills.total > 0;
    return {
      hasPrice,
      hasBills,
      price: billing.contractPriceMinor === null ? '' : money(billing.contractPriceMinor),
      billed: money(billing.billedToDateMinor),
      percentBilled:
        billing.percentBilledBps === null
          ? ''
          : `${formatBps(billing.percentBilledBps)} of the contract`,
      collected: money(billing.collectedMinor),
      outstanding: money(billing.outstandingMinor),
      overdue:
        billing.overdueMinor > 0
          ? `${money(billing.overdueMinor)} overdue on ${billing.overdueCount} bill${billing.overdueCount === 1 ? '' : 's'}`
          : '',
      retentionHeld: money(billing.retentionHeldMinor),
      showRetention: billing.retentionWithheldMinor > 0,
      showAdvance: billing.advanceBilledMinor > 0,
      advanceToRecover: money(billing.advanceToRecoverMinor),
      advanceBilled: `of the ${money(billing.advanceBilledMinor)} down payment`,
      // The down payment leads only while one can be billed: the terms take one and none exists yet.
      canBillAdvance: this.canManage() && billing.advanceToBillMinor !== null,
      advanceMessage:
        billing.advanceToBillMinor === null
          ? ''
          : `The billing terms take a down payment of ${money(billing.advanceToBillMinor)}, ${formatBps(billing.terms.advanceBps)} of the contract price. Bill it before the first progress bill; each progress bill then recovers its share.`,
      terms: termsSummary(billing.terms),
      termsNotes: termsNotes(billing.terms),
      termsDefault: billing.terms.isDefault,
      canBill: this.canManage() && hasPrice && (billing.remainingToBillMinor ?? 0) > 0,
      canRelease: this.canManage() && billing.canReleaseRetention,
      canEditTerms: this.canManage(),
      fullyBilled: hasPrice && billing.remainingToBillMinor === 0,
      rows: billing.bills.items.map((bill) => ({
        id: bill.id,
        name: billName(bill),
        kind: BILL_KIND[bill.kind],
        status: billStatus(bill),
        date: formatDate(bill.billingDate),
        due: formatDate(bill.dueDate),
        amount: money(bill.grossMinor),
        outstanding: bill.status === 'Issued' ? money(bill.outstandingMinor) : '',
        description: bill.description,
      })),
      total: billing.bills.total,
      pageSize: billing.bills.pageSize,
    };
  });

  protected readonly formContext = computed<BillFormContext | null>(() => {
    const billing = this.billing();
    if (!billing) return null;
    return {
      projectId: billing.projectId,
      contractPriceMinor: billing.contractPriceMinor,
      billedToDateMinor: billing.billedToDateMinor,
      retentionHeldMinor: billing.retentionHeldMinor,
      advanceBps: billing.terms.advanceBps,
      advanceToBillMinor: billing.advanceToBillMinor,
    };
  });

  constructor() {
    effect(() => {
      const id = this.context.projectId();
      const page = this.page();
      if (id) untracked(() => void this.load(id, page));
    });
  }

  protected async reload(): Promise<void> {
    const id = this.context.projectId();
    if (id) await this.load(id, this.page());
  }

  protected goTo(page: number): void {
    this.page.set(page);
  }

  protected async billSaved(bill: Bill): Promise<void> {
    this.newBill.set(null);
    this.toast.success('Draft bill saved.');
    await this.router.navigate(['/billing/bills', bill.id]);
  }

  protected termsSaved(): void {
    this.editingTerms.set(false);
    this.toast.success('Billing terms saved.');
    void this.reload();
  }

  private async load(projectId: string, page: number): Promise<void> {
    this.loading.set(this.billing() === null);
    this.error.set(null);
    try {
      this.billing.set(
        await firstValueFrom(this.api.projectBilling(projectId, page, DEFAULT_PAGE_SIZE)),
      );
    } catch (failure) {
      this.error.set(asApiError(failure));
    } finally {
      this.loading.set(false);
    }
  }
}
