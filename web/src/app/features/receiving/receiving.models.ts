// What the receiving screen reads and sends: quantities only. Nothing here names a price, a rate or a total, because the person who
// receives goods is never shown the company's amounts (see the server's ReceivingContracts). Keep it that way.

export type DeliveryState = 'NotReceived' | 'PartlyReceived' | 'FullyReceived';
export type OrderStatus = 'Draft' | 'Approved' | 'Closed' | 'Cancelled';
export type DeliveryStatus = 'Posted' | 'Void';

/** One order still waiting for goods, as the list shows it. */
export interface ReceivingOrderListItem {
  readonly id: string;
  readonly number: string;
  readonly supplierName: string;
  readonly projectName: string;
  readonly subProjectName: string | null;
  readonly expectedDate: string | null;
  /** NotReceived or PartlyReceived: an order with nothing left to receive is not listed. */
  readonly deliveryState: DeliveryState;
  readonly version: number;
}

/** A page of the orders awaiting delivery. `orderCount` is how many wait in all, across every page. */
export interface ReceivingOrderPage {
  readonly items: readonly ReceivingOrderListItem[];
  readonly orderCount: number;
  readonly page: number;
  readonly pageSize: number;
}

export interface ReceivingListParams {
  readonly search: string;
  readonly page: number;
  readonly pageSize: number;
}

export interface ReceivingLine {
  readonly id: string;
  readonly lineNumber: number;
  readonly description: string;
  readonly unit: string;
  readonly orderedQuantity: number;
  readonly receivedQuantity: number;
  /** Ordered minus received, never below zero: what a new delivery may still name. */
  readonly remainingQuantity: number;
}

export interface ReceivingDeliveryLine {
  readonly id: string;
  readonly orderLineId: string;
  readonly lineNumber: number;
  readonly description: string;
  readonly unit: string;
  readonly quantity: number;
}

export interface ReceivingDelivery {
  readonly id: string;
  readonly receivedDate: string;
  readonly deliveryReceiptNumber: string | null;
  readonly note: string | null;
  readonly status: DeliveryStatus;
  readonly recordedByName: string;
  readonly recordedAtUtc: string;
  readonly voidedByName: string | null;
  readonly voidedAtUtc: string | null;
  readonly canVoid: boolean;
  readonly lines: readonly ReceivingDeliveryLine[];
  readonly version: number;
}

export interface ReceivingOrder {
  readonly id: string;
  readonly number: string;
  readonly status: OrderStatus;
  readonly supplierName: string;
  readonly projectName: string;
  readonly subProjectName: string | null;
  readonly expectedDate: string | null;
  readonly supplierReference: string | null;
  readonly deliveryState: DeliveryState;
  /** An approved order of a project that is not archived with something still to receive. */
  readonly canRecord: boolean;
  readonly lines: readonly ReceivingLine[];
  readonly deliveries: readonly ReceivingDelivery[];
  readonly deliveryCount: number;
  readonly version: number;
}

/** What is typed for a delivery. The same body the purchasing screen sends, so the server applies the same rules. */
export interface RecordDeliveryFields {
  readonly receivedDate: string;
  readonly deliveryReceiptNumber: string | null;
  readonly note: string | null;
  readonly lines: readonly { readonly purchaseOrderLineId: string; readonly quantity: number }[];
}
