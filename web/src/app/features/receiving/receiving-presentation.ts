import { formatDate } from '../../shared/presentation/business-time';
import { ChipTone } from '../../shared/ui/status-chip/status-chip';
import { IconName } from '../../shared/ui/icon/icon';
import { DeliveryState, OrderStatus } from './receiving.models';

export interface Chip {
  readonly label: string;
  readonly tone: ChipTone;
  readonly icon: IconName;
}

const DELIVERY_STATE: Record<DeliveryState, Chip> = {
  NotReceived: { label: 'Nothing delivered yet', tone: 'neutral', icon: 'clock' },
  PartlyReceived: { label: 'Partly delivered', tone: 'info', icon: 'clock' },
  FullyReceived: { label: 'Delivered in full', tone: 'success', icon: 'check-circle' },
};

/** The words for a delivery state; one this client does not know is shown as it came, so a new state never breaks a page. */
export function presentDelivery(state: string): Chip {
  return Object.hasOwn(DELIVERY_STATE, state)
    ? DELIVERY_STATE[state as DeliveryState]
    : { label: state, tone: 'neutral', icon: 'info' };
}

/** A quantity as a person says it: no trailing zeros, at most four decimals ("2.5", "10"). */
export function formatQuantity(quantity: number): string {
  return quantity.toLocaleString('en-US', { maximumFractionDigits: 4 });
}

/** Where a project's order is going: "Riverside Residences" or "Riverside Residences, Tower A". */
export function placeOf(order: { projectName: string; subProjectName: string | null }): string {
  return order.subProjectName ? `${order.projectName}, ${order.subProjectName}` : order.projectName;
}

/** "Expected 5 Mar 2026", or "No date given" when the office did not say. */
export function expectedOn(date: string | null): string {
  return date ? `Expected ${formatDate(date)}` : 'No date given';
}

/** Why a delivery can't be recorded on an order that is not open, in the person's words. */
export function notOpenMessage(status: OrderStatus): string | null {
  switch (status) {
    case 'Draft':
      return 'The office has not approved this order yet, so deliveries can’t be recorded against it.';
    case 'Closed':
      return 'This order is closed, so no more deliveries can be recorded against it.';
    case 'Cancelled':
      return 'This order was cancelled, so deliveries can’t be recorded against it.';
    default:
      return null;
  }
}
