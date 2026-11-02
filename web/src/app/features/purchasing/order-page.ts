import { ChangeDetectionStrategy, Component, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { ApiError, asApiError } from '../../core/api/api-error';
import { ToastService } from '../../core/notifications/toast-service';
import { Permission } from '../../core/session/permissions';
import { SessionState } from '../../core/session/session-state';
import { formatDate, formatDateTime } from '../../shared/presentation/business-time';
import { formatMoney } from '../../shared/presentation/money';
import { Alert } from '../../shared/ui/alert/alert';
import { ApiErrorAlert } from '../../shared/ui/api-error-alert/api-error-alert';
import { Button } from '../../shared/ui/button/button';
import { ConfirmDialog } from '../../shared/ui/dialog/confirm-dialog';
import { PageHeader } from '../../shared/ui/page-header/page-header';
import { Skeleton } from '../../shared/ui/skeleton/skeleton';
import { StatusChip } from '../../shared/ui/status-chip/status-chip';
import { ReasonDialog } from '../costing/ui/reason-dialog';
import { CloseOrderDialog } from './close-order-dialog';
import { PurchasingApi } from './purchasing-api';
import {
  aheadOfDeliveryNote,
  formatQuantity,
  invoicedSummary,
  lineFlow,
  presentDelivery,
  presentStatus,
} from './purchasing-presentation';
import { RecordDeliveryDialog } from './record-delivery-dialog';
import { GoodsReceipt, PurchaseOrder } from './purchasing.models';
import { Discussion } from '../collaboration/discussion';

type Dialog = 'approve' | 'cancel' | 'close' | 'receive' | 'voidDelivery';

/**
 * One purchase order: who it is to and what for, its lines with what has been invoiced against each, the invoices matched to it,
 * and the next step. The one primary button is Approve for a person who may approve a draft; editing a draft, cancelling and
 * closing are quieter. Every figure arrives from the server. An action the person may not take, or the order's state does not
 * allow, is not shown.
 */
@Component({
  selector: 'app-order-page',
  imports: [
    RouterLink,
    PageHeader,
    Button,
    Alert,
    ApiErrorAlert,
    Skeleton,
    StatusChip,
    ConfirmDialog,
    ReasonDialog,
    CloseOrderDialog,
    RecordDeliveryDialog,
    Discussion,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './order-page.html',
  styleUrl: './order-page.scss',
})
export class OrderPage {
  /** Bound from the route. */
  readonly id = input.required<string>();

  private readonly api = inject(PurchasingApi);
  private readonly session = inject(SessionState);
  private readonly toast = inject(ToastService);

  protected readonly order = signal<PurchaseOrder | null>(null);
  protected readonly loading = signal(true);
  protected readonly loadError = signal<ApiError | null>(null);
  protected readonly dialog = signal<Dialog | null>(null);
  protected readonly busy = signal(false);
  protected readonly actionError = signal<ApiError | null>(null);
  /** The delivery the person is voiding while the reason dialog is open. */
  protected readonly voidTarget = signal<GoodsReceipt | null>(null);

  private readonly canManage = this.session.permit(Permission.purchasingManage);
  private readonly canApprove = this.session.permit(Permission.purchasingApprove);
  private readonly canReceive = this.session.permit(Permission.purchasingReceive);
  protected readonly currency = this.session.currencyCode;

  protected readonly showEdit = computed(() => this.canManage() && (this.order()?.canEdit ?? false));
  protected readonly showApprove = computed(() => this.canApprove() && (this.order()?.canApprove ?? false));
  protected readonly showCancel = computed(() => this.canApprove() && (this.order()?.canCancel ?? false));
  protected readonly showClose = computed(() => this.canApprove() && (this.order()?.canClose ?? false));
  /** The one primary action of an approved order that still has something to receive. */
  protected readonly showReceive = computed(() => this.canReceive() && (this.order()?.canReceive ?? false));
  protected readonly showVoidDelivery = this.canReceive;

  protected readonly view = computed(() => {
    const order = this.order();
    if (!order) return null;
    const money = (minor: number) => formatMoney(minor, this.currency());
    const timeZone = this.session.timeZoneId();
    const history = [
      `Drafted by ${order.createdByName} on ${formatDateTime(order.createdAtUtc, timeZone)}`,
      order.approvedAtUtc ? `Approved by ${order.approvedByName} on ${formatDateTime(order.approvedAtUtc, timeZone)}` : null,
      order.closedAtUtc ? `Closed by ${order.closedByName} on ${formatDateTime(order.closedAtUtc, timeZone)}` : null,
      order.cancelledAtUtc ? `Cancelled by ${order.cancelledByName} on ${formatDateTime(order.cancelledAtUtc, timeZone)}` : null,
    ].filter((line): line is string => line !== null);
    const started = order.status === 'Approved' || order.status === 'Closed';
    const anyReceived = order.lines.some((line) => line.receivedQuantity > 0);
    return {
      status: presentStatus(order.status),
      orderDate: formatDate(order.orderDate),
      expectedDate: order.expectedDate ? formatDate(order.expectedDate) : null,
      net: money(order.netMinor),
      vat: money(order.vatMinor),
      gross: money(order.grossMinor),
      invoiced: money(order.invoicedGrossMinor),
      committed: money(order.committedGrossMinor),
      showInvoiced: started && order.invoiceCount > 0,
      showCommitted: order.status === 'Approved',
      history,
      reason: order.status === 'Cancelled' ? order.cancelReason : order.status === 'Closed' ? order.closeReason : null,
      delivery: presentDelivery(order.deliveryState),
      showDelivery: started && order.receiptCount > 0,
      lines: order.lines.map((line) => ({
        id: line.id,
        number: line.lineNumber,
        description: line.description,
        detail: `${line.quantity} ${line.unit} at ${money(line.unitCostMinor)}`,
        total: money(line.grossMinor),
        standing: started ? invoicedSummary(line, this.currency(), order.status === 'Closed') : null,
        over: line.overByGrossMinor > 0,
        // Ordered, received and invoiced side by side once anything has arrived; before that the order's own words are enough.
        flow: started && anyReceived ? lineFlow(line, this.currency()) : null,
        ahead: aheadOfDeliveryNote(line, this.currency()),
      })),
      receipts: order.receipts.map((receipt) => ({
        id: receipt.id,
        void: receipt.status === 'Void',
        title: receipt.deliveryReceiptNumber
          ? `${formatDate(receipt.receivedDate)} · ${receipt.deliveryReceiptNumber}`
          : formatDate(receipt.receivedDate),
        items: receipt.lines
          .map((line) => `${formatQuantity(line.quantity)} ${line.unit} ${line.description}`)
          .join(', '),
        note: receipt.note,
        recorded: `Recorded by ${receipt.recordedByName} on ${formatDateTime(receipt.recordedAtUtc, timeZone)}`,
        voided: receipt.voidedAtUtc
          ? `Voided by ${receipt.voidedByName} on ${formatDateTime(receipt.voidedAtUtc, timeZone)}: ${receipt.voidReason}`
          : null,
        canVoid: receipt.canVoid,
      })),
      moreReceipts: order.receiptCount > order.receipts.length ? order.receiptCount - order.receipts.length : 0,
      invoices: order.invoices.map((invoice) => ({
        id: invoice.costEntryId,
        date: formatDate(invoice.entryDate),
        supplier: invoice.sourceDealer,
        number: invoice.invoiceNumber,
        line: invoice.lineNumber,
        amount: money(invoice.grossMinor),
      })),
      moreInvoices: order.invoiceCount > order.invoices.length ? order.invoiceCount - order.invoices.length : 0,
    };
  });

  constructor() {
    effect(() => {
      const id = this.id();
      untracked(() => void this.load(id));
    });
  }

  protected async load(id: string = this.id()): Promise<void> {
    this.loading.set(true);
    this.loadError.set(null);
    try {
      this.order.set(await firstValueFrom(this.api.getOrder(id)));
    } catch (failure) {
      this.loadError.set(asApiError(failure));
    } finally {
      this.loading.set(false);
    }
  }

  protected open(which: Dialog): void {
    this.actionError.set(null);
    this.dialog.set(which);
  }

  protected closeDialog(): void {
    this.dialog.set(null);
    this.voidTarget.set(null);
  }

  protected openVoid(receiptId: string): void {
    this.voidTarget.set(this.order()?.receipts.find((receipt) => receipt.id === receiptId) ?? null);
    this.open('voidDelivery');
  }

  protected readonly voidDelivery = (reason: string) => {
    const order = this.order()!;
    const receipt = this.voidTarget()!;
    return this.api.voidDelivery(order.id, receipt.id, reason, receipt.version);
  };

  /** The delivery was recorded: the server sent the order back as it now stands, so there is nothing to refetch. */
  protected delivered(order: PurchaseOrder): void {
    this.order.set(order);
    this.dialog.set(null);
    this.toast.success('Delivery recorded.');
  }

  protected readonly cancelOrder = (reason: string) => {
    const order = this.order()!;
    return this.api.cancelOrder(order.id, reason, order.version);
  };

  protected async approve(): Promise<void> {
    const order = this.order();
    if (!order || this.busy()) return;
    this.busy.set(true);
    this.actionError.set(null);
    try {
      this.order.set(await firstValueFrom(this.api.approveOrder(order.id, order.version)));
      this.dialog.set(null);
      this.toast.success('Purchase order approved.');
    } catch (failure) {
      this.dialog.set(null);
      this.actionError.set(asApiError(failure));
    } finally {
      this.busy.set(false);
    }
  }

  protected async finished(message: string): Promise<void> {
    this.dialog.set(null);
    this.voidTarget.set(null);
    await this.load();
    this.toast.success(message);
  }

  protected async reloadAfterConflict(): Promise<void> {
    this.dialog.set(null);
    this.voidTarget.set(null);
    await this.load();
  }
}
