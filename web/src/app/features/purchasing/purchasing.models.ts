import { ItemType, VatTreatment } from '../costing/costing.models';

// The shapes of the purchasing and supplier APIs (docs/api/purchasing.md). Money is whole minor units (centavos); quantities are
// decimals; dates are business dates ("2026-11-02"). The client displays what arrives and never works a total out itself.

export type PurchaseOrderStatus = 'Draft' | 'Approved' | 'Closed' | 'Cancelled';

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
