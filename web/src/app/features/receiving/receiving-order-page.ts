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
import { firstValueFrom } from 'rxjs';
import { ApiError, asApiError } from '../../core/api/api-error';
import { ToastService } from '../../core/notifications/toast-service';
import { SessionState } from '../../core/session/session-state';
import { formatDate, formatDateTime } from '../../shared/presentation/business-time';
import { Alert } from '../../shared/ui/alert/alert';
import { ApiErrorAlert } from '../../shared/ui/api-error-alert/api-error-alert';
import { Button } from '../../shared/ui/button/button';
import { PageHeader } from '../../shared/ui/page-header/page-header';
import { Skeleton } from '../../shared/ui/skeleton/skeleton';
import { StatusChip } from '../../shared/ui/status-chip/status-chip';
import { ReasonDialog } from '../costing/ui/reason-dialog';
import { ReceiveDeliveryDialog } from './receive-delivery-dialog';
import { ReceivingApi } from './receiving-api';
import {
  expectedOn,
  formatQuantity,
  notOpenMessage,
  placeOf,
  presentDelivery,
} from './receiving-presentation';
import { ReceivingDelivery, ReceivingOrder } from './receiving.models';

type Dialog = 'receive' | 'voidDelivery';

/**
 * One order to receive against: where it is going, what was ordered and how much of each item has come, and the deliveries recorded
 * so far. The one primary action is Record delivery, shown only while there is something left to receive. A delivery entered by
 * mistake can be voided, with a reason. Quantities only: no amount appears on this page.
 */
@Component({
  selector: 'app-receiving-order-page',
  imports: [
    RouterLink,
    PageHeader,
    Button,
    Alert,
    ApiErrorAlert,
    Skeleton,
    StatusChip,
    ReasonDialog,
    ReceiveDeliveryDialog,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './receiving-order-page.html',
  styleUrl: './receiving-order-page.scss',
})
export class ReceivingOrderPage {
  /** Bound from the route. */
  readonly id = input.required<string>();

  private readonly api = inject(ReceivingApi);
  private readonly session = inject(SessionState);
  private readonly toast = inject(ToastService);

  protected readonly order = signal<ReceivingOrder | null>(null);
  protected readonly loading = signal(true);
  protected readonly loadError = signal<ApiError | null>(null);
  protected readonly dialog = signal<Dialog | null>(null);
  /** The delivery the person is voiding while the reason dialog is open. */
  protected readonly voidTarget = signal<ReceivingDelivery | null>(null);

  protected readonly view = computed(() => {
    const order = this.order();
    if (!order) return null;
    const timeZone = this.session.timeZoneId();
    const anyLeft = order.lines.some((line) => line.remainingQuantity > 0);
    return {
      place: placeOf(order),
      expected: expectedOn(order.expectedDate),
      supplierReference: order.supplierReference,
      delivery: presentDelivery(order.deliveryState),
      showDelivery: order.status === 'Approved',
      // An order that can't take a delivery says why, in words. An approved order with items still to come that still can't take one
      // belongs to an archived project.
      notOpen: notOpenMessage(order.status),
      archived: order.status === 'Approved' && anyLeft && !order.canRecord,
      lines: order.lines.map((line) => ({
        id: line.id,
        number: line.lineNumber,
        description: line.description,
        standing: `Ordered ${formatQuantity(line.orderedQuantity)} ${line.unit}`,
        received: `Received ${formatQuantity(line.receivedQuantity)} ${line.unit}`,
        left:
          line.remainingQuantity > 0
            ? `${formatQuantity(line.remainingQuantity)} ${line.unit} still to come`
            : 'All received',
        complete: line.remainingQuantity <= 0,
      })),
      deliveries: order.deliveries.map((delivery) => ({
        id: delivery.id,
        void: delivery.status === 'Void',
        title: delivery.deliveryReceiptNumber
          ? `${formatDate(delivery.receivedDate)} · ${delivery.deliveryReceiptNumber}`
          : formatDate(delivery.receivedDate),
        items: delivery.lines
          .map((line) => `${formatQuantity(line.quantity)} ${line.unit} ${line.description}`)
          .join(', '),
        note: delivery.note,
        recorded: `Recorded by ${delivery.recordedByName} on ${formatDateTime(delivery.recordedAtUtc, timeZone)}`,
        voided: delivery.voidedAtUtc
          ? `Voided by ${delivery.voidedByName} on ${formatDateTime(delivery.voidedAtUtc, timeZone)}`
          : null,
        canVoid: delivery.canVoid,
      })),
      moreDeliveries:
        order.deliveryCount > order.deliveries.length
          ? order.deliveryCount - order.deliveries.length
          : 0,
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
    this.dialog.set(which);
  }

  protected closeDialog(): void {
    this.dialog.set(null);
    this.voidTarget.set(null);
  }

  protected openVoid(deliveryId: string): void {
    this.voidTarget.set(
      this.order()?.deliveries.find((delivery) => delivery.id === deliveryId) ?? null,
    );
    this.open('voidDelivery');
  }

  protected readonly voidDelivery = (reason: string) => {
    const order = this.order()!;
    const delivery = this.voidTarget()!;
    return this.api.voidDelivery(order.id, delivery.id, reason, delivery.version);
  };

  /** The delivery was recorded: the server sent the order back as it now stands, so there is nothing to refetch. */
  protected delivered(order: ReceivingOrder): void {
    this.order.set(order);
    this.dialog.set(null);
    this.toast.success('Delivery recorded.');
  }

  protected async voided(): Promise<void> {
    this.dialog.set(null);
    this.voidTarget.set(null);
    await this.load();
    this.toast.success('Delivery voided.');
  }

  protected async reloadAfterConflict(): Promise<void> {
    this.dialog.set(null);
    this.voidTarget.set(null);
    await this.load();
  }
}
