/** What kind of thing an item is. Tools are counted; equipment and machinery are single pieces with a serial number. */
export type InventoryCategory = 'Tool' | 'Equipment' | 'Machinery';

export type InventoryItemStatus = 'Active' | 'Retired';

/** The kinds of line in an item's history, as the API writes them. */
export type MovementType = 'Purchase' | 'Transfer' | 'Adjustment' | 'WriteOff' | 'Reversal';

/** How much of an item is in one place. Only places that hold some are listed: warehouse first, then projects by name. */
export interface StockBalance {
  readonly location: 'Warehouse' | 'Project';
  readonly projectId: string | null;
  readonly projectName: string | null;
  readonly quantity: number;
}

/** One row of the stock list. */
export interface InventoryListItem {
  readonly id: string;
  readonly description: string;
  readonly category: InventoryCategory;
  readonly brand: string | null;
  readonly serialNumber: string | null;
  readonly unit: string;
  readonly unitCostMinor: number;
  readonly status: InventoryItemStatus;
  readonly totalQuantity: number;
  readonly balances: readonly StockBalance[];
}

/** A whole item. Every change sends back the `version` it was loaded with. */
export interface InventoryItem {
  readonly id: string;
  readonly description: string;
  readonly category: InventoryCategory;
  readonly brand: string | null;
  readonly serialNumber: string | null;
  readonly unit: string;
  readonly sourceDealer: string | null;
  readonly invoiceNumber: string | null;
  /** A business date, "2026-12-03". */
  readonly purchaseDate: string;
  readonly unitCostMinor: number;
  readonly remarks: string | null;
  readonly status: InventoryItemStatus;
  readonly retiredReason: string | null;
  readonly totalQuantity: number;
  readonly balances: readonly StockBalance[];
  readonly version: number;
}

/** One line of an item's history. A missing from or to name means that side is outside the system (the type says which). */
export interface StockMovement {
  readonly id: string;
  readonly type: MovementType;
  readonly fromName: string | null;
  readonly fromProjectId: string | null;
  readonly toName: string | null;
  readonly toProjectId: string | null;
  readonly quantity: number;
  readonly date: string;
  /** Above zero only on a transfer that was charged to a project. Minor units. */
  readonly chargedAmountMinor: number;
  readonly note: string | null;
  readonly recordedBy: string;
  readonly recordedAtUtc: string;
  readonly isReversed: boolean;
  readonly reversalOfMovementId: string | null;
}

/** One row of "what is on this project now". */
export interface ProjectStock {
  readonly itemId: string;
  readonly description: string;
  readonly category: InventoryCategory;
  readonly brand: string | null;
  readonly serialNumber: string | null;
  readonly unit: string;
  readonly quantity: number;
}

/** A project as the pickers need it (`GET /api/projects/lookup`). */
export interface ProjectLookup {
  readonly id: string;
  readonly name: string;
  readonly status: string;
}

/** Where stock is, as a request writes it. */
export type StockLocation =
  | { readonly kind: 'Warehouse' }
  | { readonly kind: 'Project'; readonly projectId: string };

export interface InventoryListParams {
  readonly search: string;
  /** Empty for every category. */
  readonly category: string;
  /** "Active" or "Retired". */
  readonly status: string;
  /** Keep items that have stock on this project. */
  readonly projectId: string;
  /** Keep items that have stock in the warehouse. */
  readonly inWarehouse: boolean;
  readonly page: number;
  readonly pageSize: number;
}

/** The details of an item, as the register and edit forms send them. */
export interface ItemDetails {
  readonly description: string;
  readonly brand: string | null;
  readonly serialNumber: string | null;
  readonly unit: string;
  readonly sourceDealer: string | null;
  readonly invoiceNumber: string | null;
  readonly purchaseDate: string;
  readonly unitCostMinor: number;
  readonly remarks: string | null;
}

export interface RegisterItemRequest extends ItemDetails {
  readonly category: InventoryCategory;
  readonly quantity: number;
  readonly clientRequestId: string;
}

export interface UpdateItemRequest extends ItemDetails {
  readonly version: number;
}

export interface ReceiveRequest {
  readonly quantity: number;
  readonly date: string;
  readonly note: string | null;
  readonly clientRequestId: string;
  readonly version: number;
}

export interface TransferRequest {
  readonly fromLocation: StockLocation;
  readonly toLocation: StockLocation;
  readonly quantity: number;
  readonly date: string;
  readonly chargeToProject: boolean;
  readonly note: string | null;
  readonly clientRequestId: string;
  readonly version: number;
}

export interface AdjustRequest {
  readonly location: StockLocation;
  readonly countedQuantity: number;
  readonly reason: string;
  readonly clientRequestId: string;
  readonly version: number;
}

export interface WriteOffRequest {
  readonly location: StockLocation;
  readonly quantity: number;
  readonly reason: string;
  readonly clientRequestId: string;
  readonly version: number;
}

export interface ReverseRequest {
  readonly reason: string;
  readonly clientRequestId: string;
  readonly version: number;
}

export interface RetireRequest {
  readonly reason: string;
  readonly version: number;
}

/** The units the API suggests (the same list the cost screens use). Free text is still allowed. */
export const UNIT_SUGGESTIONS: readonly string[] = [
  'pc',
  'unit',
  'set',
  'bag',
  'sheet',
  'roll',
  'lot',
  'no.',
  'kg',
  'm',
  'm²',
  'm³',
  'l.m.',
  'l.s',
  'gal',
  'liter',
  'tube',
  'pail',
  'can',
  'yard',
  'bd.ft.',
];
