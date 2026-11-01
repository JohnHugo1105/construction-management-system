import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  signal,
  untracked,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { Observable, firstValueFrom } from 'rxjs';
import { ApiError, asApiError } from '../../core/api/api-error';
import { ToastService } from '../../core/notifications/toast-service';
import { Permission } from '../../core/session/permissions';
import { SessionState } from '../../core/session/session-state';
import { formatDate, formatDateTime } from '../../shared/presentation/business-time';
import { formatMoney } from '../../shared/presentation/money';
import { Alert } from '../../shared/ui/alert/alert';
import { ApiErrorAlert } from '../../shared/ui/api-error-alert/api-error-alert';
import { Button } from '../../shared/ui/button/button';
import { PageHeader } from '../../shared/ui/page-header/page-header';
import { Skeleton } from '../../shared/ui/skeleton/skeleton';
import { StatusChip } from '../../shared/ui/status-chip/status-chip';
import { BillFormDialog, BillFormContext, advanceRows, breakdownRows } from './bill-form-dialog';
import { BillState } from './bill-state';
import { BillingApi } from './billing-api';
import { BILL_KIND, billActions, billName, billStatus, paymentMethodLabel } from './billing-rules';
import { Bill, BillPayment } from './billing.models';
import { PaymentDialog } from './payment-dialog';
import { ReasonDialog } from './reason-dialog';

type ReasonAction = 'void' | 'discard' | { readonly payment: BillPayment };

interface ReasonPrompt {
  readonly title: string;
  readonly message: string;
  readonly label: string;
  readonly confirm: string;
  readonly keep: string;
  readonly done: string;
  readonly action: (reason: string) => Observable<Bill>;
}

/**
 * One bill to the client: its breakdown, what is still to collect, and the payments recorded against it. The one next step leads
 * (Issue bill for an approver looking at a draft, Record payment while something is owed); the rest are quieter. A person sees
 * only what they may do, and an archived project's bill is read-only. Every figure is the server's.
 */
@Component({
  selector: 'app-bill-page',
  imports: [
    RouterLink,
    PageHeader,
    Alert,
    ApiErrorAlert,
    Button,
    Skeleton,
    StatusChip,
    BillFormDialog,
    PaymentDialog,
    ReasonDialog,
  ],
  providers: [BillState],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './bill-page.html',
  styleUrl: './bill-page.scss',
})
export class BillPage {
  /** Bound from the route (`/billing/bills/:id`). */
  readonly id = input.required<string>();

  protected readonly state = inject(BillState);
  private readonly api = inject(BillingApi);
  private readonly session = inject(SessionState);
  private readonly toast = inject(ToastService);

  protected readonly editing = signal(false);
  protected readonly paying = signal(false);
  protected readonly reasonFor = signal<ReasonAction | null>(null);
  protected readonly issuing = signal(false);
  protected readonly issueError = signal<ApiError | null>(null);

  protected readonly title = computed(() =>
    this.state.status() === 'ready' ? billName(this.state.bill()) : 'Bill',
  );
  protected readonly backLink = computed(() =>
    this.state.status() === 'ready'
      ? ['/projects', this.state.bill().projectId, 'billing']
      : ['/billing'],
  );
  protected readonly backLabel = computed(() =>
    this.state.status() === 'ready' ? `${this.state.bill().projectName} billing` : 'Billing',
  );

  protected readonly actions = computed(() =>
    billActions(this.state.bill(), {
      canManage: this.session.can(Permission.billingManage),
      canApprove: this.session.can(Permission.billingApprove),
    }),
  );

  protected readonly view = computed(() => {
    if (this.state.status() !== 'ready') return null;
    const bill = this.state.bill();
    const currency = this.session.currencyCode();
    const zone = this.session.timeZoneId();
    const money = (minor: number): string => formatMoney(minor, currency);
    const history: string[] = [];
    if (bill.createdByName)
      history.push(
        `Prepared by ${bill.createdByName} on ${formatDateTime(bill.createdAtUtc, zone)}`,
      );
    if (bill.issuedAtUtc)
      history.push(
        `Issued${bill.issuedByName ? ` by ${bill.issuedByName}` : ''} on ${formatDateTime(bill.issuedAtUtc, zone)}`,
      );
    if (bill.voidedAtUtc)
      history.push(
        `${bill.number ? 'Voided' : 'Discarded'}${bill.voidedByName ? ` by ${bill.voidedByName}` : ''} on ${formatDateTime(bill.voidedAtUtc, zone)}`,
      );
    const progress = bill.kind === 'Progress';
    const draft = bill.status === 'Draft';
    return {
      status: billStatus(bill),
      kind: BILL_KIND[bill.kind],
      issued: bill.status === 'Issued',
      draft,
      // A down payment draft follows the contract price and the terms; any other draft follows the bills issued before it.
      staleReason:
        bill.kind === 'Advance'
          ? 'The contract price or the down payment in the billing terms changed after it was prepared.'
          : 'Another bill was issued or voided after it was prepared.',
      leftover: draft && bill.canOnlyBeDiscarded,
      archived: bill.projectStatus === 'Archived',
      description: bill.description,
      billingDate: formatDate(bill.billingDate),
      dueDate: formatDate(bill.dueDate),
      outstanding: money(bill.outstandingMinor),
      paid: money(bill.paidMinor),
      receivable: money(bill.netReceivableMinor),
      work: progress
        ? [
            { label: 'Work completed to date', amount: money(bill.cumulativeWorkMinor ?? 0) },
            { label: 'Billed before this bill', amount: money(bill.previousCumulativeWorkMinor) },
          ]
        : [],
      rows: progress
        ? breakdownRows(bill, currency)
        : bill.kind === 'Advance'
          ? advanceRows(bill, currency)
          : [
              { label: 'Retention released', amount: money(bill.grossMinor) },
              { label: 'The client pays', amount: money(bill.netReceivableMinor), total: true },
            ],
      history,
      voidReason: bill.voidReason,
      payments: bill.payments.map((payment) => ({
        payment,
        id: payment.id,
        amount: money(payment.amountMinor),
        when: formatDate(payment.paidOn),
        how: [paymentMethodLabel(payment.method), payment.reference].filter(Boolean).join(' · '),
        note: payment.note,
        voided: payment.isVoided,
        voidReason: payment.voidReason,
      })),
    };
  });

  /** The project's figures now, read when the draft is opened for a change (the draft's own may be out of date). */
  protected readonly formContext = signal<BillFormContext | null>(null);
  protected readonly opening = signal(false);

  protected readonly prompt = computed<ReasonPrompt | null>(() => {
    const target = this.reasonFor();
    if (target === null || this.state.status() !== 'ready') return null;
    const bill = this.state.bill();
    if (target === 'void')
      return {
        title: `Void ${billName(bill)}?`,
        message:
          'The bill stays on record as void and no longer counts in what the client owes. The bill before it becomes the latest again.',
        label: 'Why is it being voided?',
        confirm: 'Void bill',
        keep: 'Keep bill',
        done: 'Bill voided.',
        action: (reason) => this.api.void(bill.id, reason, bill.version),
      };
    if (target === 'discard')
      return {
        title: 'Discard this draft?',
        message: 'The draft stays on record as discarded. It never had a number and never counted.',
        label: 'Why is it being discarded?',
        confirm: 'Discard draft',
        keep: 'Keep draft',
        done: 'Draft discarded.',
        action: (reason) => this.api.discard(bill.id, reason, bill.version),
      };
    const payment = target.payment;
    return {
      title: 'Void this payment?',
      message: `The payment of ${formatMoney(payment.amountMinor, this.session.currencyCode())} stays on record as void, and the amount is owed again.`,
      label: 'Why is it being voided?',
      confirm: 'Void payment',
      keep: 'Keep payment',
      done: 'Payment voided.',
      action: (reason) => this.api.voidPayment(bill.id, payment.id, reason, bill.version),
    };
  });

  constructor() {
    effect(() => {
      const id = this.id();
      untracked(() => void this.state.load(id));
    });
  }

  protected async edit(): Promise<void> {
    if (this.opening()) return;
    const bill = this.state.bill();
    this.opening.set(true);
    this.issueError.set(null);
    try {
      const billing = await firstValueFrom(this.api.projectBilling(bill.projectId, 1, 1));
      this.formContext.set({
        projectId: bill.projectId,
        contractPriceMinor: billing.contractPriceMinor,
        billedToDateMinor: billing.billedToDateMinor,
        retentionHeldMinor: billing.retentionHeldMinor,
        advanceBps: billing.terms.advanceBps,
        advanceToBillMinor: billing.advanceToBillMinor,
      });
      this.editing.set(true);
    } catch (failure) {
      this.issueError.set(asApiError(failure));
    } finally {
      this.opening.set(false);
    }
  }

  protected async issue(): Promise<void> {
    if (this.issuing()) return;
    const bill = this.state.bill();
    this.issuing.set(true);
    this.issueError.set(null);
    try {
      const issued = await firstValueFrom(this.api.issue(bill.id, bill.version));
      this.state.replace(issued);
      this.toast.success(`${issued.number} issued.`);
    } catch (failure) {
      this.issueError.set(asApiError(failure));
    } finally {
      this.issuing.set(false);
    }
  }

  protected saved(bill: Bill, message: string): void {
    this.editing.set(false);
    this.paying.set(false);
    this.reasonFor.set(null);
    this.state.replace(bill);
    this.toast.success(message);
  }

  protected reload(): void {
    this.editing.set(false);
    this.paying.set(false);
    this.reasonFor.set(null);
    this.issueError.set(null);
    void this.state.reload();
  }
}
