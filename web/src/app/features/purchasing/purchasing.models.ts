import { ItemType, VatTreatment } from '../costing/costing.models';

// The shapes of the purchasing and supplier APIs (docs/api/purchasing.md). Money is whole minor units (centavos); quantities are
// decimals; dates are business dates ("2026-11-02"). The client displays what arrives and never works a total out itself.

export type PurchaseOrderStatus = 'Draft' | 'Approved' | 'Closed' | 'Cancelled';

/** How much of an order has arrived, as the server worked it out from the lines and the deliveries that stand. */
export type DeliveryState = 'NotReceived' | 'PartlyReceived' | 'FullyReceived';

export interface PurchaseOrderListItem {
  readonly id: string;
  readonly number: string;
  readonly supplierId: string;
  readonly supplierName: string;
  readonly projectId: string;
  readonly projectName: string;
  readonly orderDate: string;
  readonly expectedDate: string | null;
  readonly status: PurchaseOrderStatus;
  readonly lineCount: number;
  readonly netMinor: number;
  readonly vatMinor: number;
  readonly grossMinor: number;
  /** Only for an approved order: the server's answer to "has it arrived?". Null for every other status. */
  readonly deliveryState: DeliveryState | null;
}

export interface PurchaseOrderLine {
  readonly id: string;
  readonly lineNumber: number;
  readonly division: number | null;
  readonly mainScope: string | null;
  readonly subScope: string | null;
  readonly wbsCode: string | null;
  readonly description: string;
  readonly itemType: ItemType;
  readonly quantity: number;
  readonly unit: string;
  readonly unitCostMinor: number;
  readonly vatTreatment: VatTreatment;
  readonly vatRateBps: number;
  readonly netMinor: number;
  readonly vatMinor: number;
  readonly grossMinor: number;
  readonly invoicedNetMinor: number;
  readonly invoicedGrossMinor: number;
  readonly remainingNetMinor: number;
  readonly remainingGrossMinor: number;
  readonly overByGrossMinor: number;
  /** What deliveries that stand say arrived for this line, and what a new delivery may still name. */
  readonly receivedQuantity: number;
  readonly toReceiveQuantity: number;
  /** The received quantity at the line's unit cost, on the line's own VAT basis. */
  readonly receivedValueNetMinor: number;
  readonly receivedValueGrossMinor: number;
  /** Advisory: more was invoiced to this line than what has arrived is worth. The invoice is still valid. */
  readonly invoicedAheadOfDelivery: boolean;
}

export interface MatchedInvoice {
  readonly costEntryId: string;
  readonly purchaseOrderLineId: string;
  readonly lineNumber: number;
  readonly entryDate: string;
  readonly sourceDealer: string;
  readonly invoiceNumber: string | null;
  readonly description: string;
  readonly netMinor: number;
  readonly grossMinor: number;
}

export type GoodsReceiptStatus = 'Posted' | 'Void';

export interface GoodsReceiptLine {
  readonly id: string;
  readonly purchaseOrderLineId: string;
  readonly lineNumber: number;
  readonly description: string;
  readonly unit: string;
  readonly quantity: number;
}

/** One delivery against an order: what the supplier brought on a day, under their delivery receipt. Voided with a reason, never edited. */
export interface GoodsReceipt {
  readonly id: string;
  readonly receivedDate: string;
  readonly deliveryReceiptNumber: string | null;
  readonly note: string | null;
  readonly status: GoodsReceiptStatus;
  readonly recordedByName: string;
  readonly recordedAtUtc: string;
  readonly voidedByName: string | null;
  readonly voidedAtUtc: string | null;
  readonly voidReason: string | null;
  readonly canVoid: boolean;
  readonly lines: readonly GoodsReceiptLine[];
  readonly version: number;
}

export interface PurchaseOrder {
  readonly id: string;
  readonly number: string;
  readonly status: PurchaseOrderStatus;
  readonly supplierId: string;
  readonly supplierName: string;
  readonly projectId: string;
  readonly projectName: string;
  readonly projectArchived: boolean;
  readonly subProjectId: string | null;
  readonly subProjectName: string | null;
  readonly orderDate: string;
  readonly expectedDate: string | null;
  readonly supplierReference: string | null;
  readonly notes: string | null;
  readonly netMinor: number;
  readonly vatMinor: number;
  readonly grossMinor: number;
  readonly invoicedNetMinor: number;
  readonly invoicedGrossMinor: number;
  readonly committedNetMinor: number;
  readonly committedGrossMinor: number;
  readonly lines: readonly PurchaseOrderLine[];
  readonly invoices: readonly MatchedInvoice[];
  readonly invoiceCount: number;
  readonly createdByName: string;
  readonly createdAtUtc: string;
  readonly approvedByName: string | null;
  readonly approvedAtUtc: string | null;
  readonly closedByName: string | null;
  readonly closedAtUtc: string | null;
  readonly closeReason: string | null;
  readonly cancelledByName: string | null;
  readonly cancelledAtUtc: string | null;
  readonly cancelReason: string | null;
  readonly canEdit: boolean;
  readonly canApprove: boolean;
  readonly canCancel: boolean;
  readonly canClose: boolean;
  readonly deliveryState: DeliveryState;
  /** The newest deliveries, voided ones too; `receiptCount` is the true number. */
  readonly receipts: readonly GoodsReceipt[];
  readonly receiptCount: number;
  /** An approved order of an open project with something still to receive. */
  readonly canReceive: boolean;
  readonly version: number;
}

/** A line of an approved order that a supplier invoice can be recorded against. */
export interface OpenPurchaseOrderLine {
  readonly lineId: string;
  readonly orderId: string;
  readonly orderNumber: string;
  readonly lineNumber: number;
  readonly supplierName: string;
  readonly description: string;
  readonly grossMinor: number;
  readonly remainingNetMinor: number;
  readonly remainingGrossMinor: number;
}

export interface PurchaseOrderListParams {
  readonly projectId: string;
  readonly supplierId: string;
  readonly status: string;
  readonly search: string;
  readonly page: number;
  readonly pageSize: number;
}

/** One line as the form sends it. The VAT rate is not a field: the server keeps the company's rate of the day. */
export interface PurchaseOrderLineFields {
  readonly division: number | null;
  readonly mainScope: string | null;
  readonly subScope: string | null;
  readonly wbsCode: string | null;
  readonly description: string;
  readonly itemType: ItemType;
  readonly quantity: number;
  readonly unit: string;
  readonly unitCostMinor: number;
  readonly vatTreatment: VatTreatment;
}

export interface PurchaseOrderFields {
  readonly projectId: string;
  readonly subProjectId: string | null;
  readonly supplierId: string;
  readonly orderDate: string;
  readonly expectedDate: string | null;
  readonly supplierReference: string | null;
  readonly notes: string | null;
  readonly lines: readonly PurchaseOrderLineFields[];
}

/** One line of a delivery as the form sends it: which order line, and how much of it arrived. */
export interface DeliveryLineFields {
  readonly purchaseOrderLineId: string;
  readonly quantity: number;
}

export interface RecordDeliveryFields {
  readonly receivedDate: string;
  readonly deliveryReceiptNumber: string | null;
  readonly note: string | null;
  readonly lines: readonly DeliveryLineFields[];
}

// ---- Firms ---------------------------------------------------------------------------------------------------------------

export interface SupplierListItem {
  readonly id: string;
  readonly name: string;
  readonly isSupplier: boolean;
  readonly isSubcontractor: boolean;
  readonly contactPerson: string | null;
  readonly phone: string | null;
  readonly email: string | null;
  readonly isArchived: boolean;
  readonly version: number;
}

export interface SupplierLookup {
  readonly id: string;
  readonly name: string;
  readonly isSupplier: boolean;
  readonly isSubcontractor: boolean;
}

export interface Supplier {
  readonly id: string;
  readonly name: string;
  readonly isSupplier: boolean;
  readonly isSubcontractor: boolean;
  readonly tin: string | null;
  readonly contactPerson: string | null;
  readonly phone: string | null;
  readonly email: string | null;
  readonly address: string | null;
  readonly notes: string | null;
  readonly isArchived: boolean;
  readonly archivedAtUtc: string | null;
  readonly version: number;
}

export interface SupplierFields {
  readonly name: string;
  readonly isSupplier: boolean;
  readonly isSubcontractor: boolean;
  readonly tin: string | null;
  readonly contactPerson: string | null;
  readonly phone: string | null;
  readonly email: string | null;
  readonly address: string | null;
  readonly notes: string | null;
}

export interface SupplierListParams {
  readonly search: string;
  readonly kind: string;
  readonly includeArchived: boolean;
  readonly page: number;
  readonly pageSize: number;
}

/** A project (and its sub-projects) as a picker offers it. */
export interface ProjectChoice {
  readonly id: string;
  readonly name: string;
  readonly status: string;
  readonly subProjects: readonly { readonly id: string; readonly name: string; readonly status: string }[];
}
