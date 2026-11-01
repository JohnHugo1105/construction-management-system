import { IconName } from '../../shared/ui/icon/icon';
import { ChipTone } from '../../shared/ui/status-chip/status-chip';
import { InventoryCategory, InventoryItemStatus, StockBalance } from './inventory.models';

export interface CategoryPresentation {
  readonly label: string;
  /** The word for the filter chip ("Tools"). */
  readonly plural: string;
  readonly icon: IconName;
}

/** How each category looks and reads, in one place: the list, the detail page and the forms always agree. */
export const CATEGORY: Record<InventoryCategory, CategoryPresentation> = {
  Tool: { label: 'Tool', plural: 'Tools', icon: 'pencil' },
  Equipment: { label: 'Equipment', plural: 'Equipment', icon: 'sliders' },
  Machinery: { label: 'Machinery', plural: 'Machinery', icon: 'construction' },
};

export const CATEGORIES: readonly InventoryCategory[] = ['Tool', 'Equipment', 'Machinery'];

/** The category chips above the stock list. "All" leaves the category out of the question. */
export const CATEGORY_FILTERS = ['All', 'Tool', 'Equipment', 'Machinery'] as const;
export type CategoryFilter = (typeof CATEGORY_FILTERS)[number];

export function categoryFilterLabel(filter: CategoryFilter): string {
  return filter === 'All' ? 'All' : CATEGORY[filter].plural;
}

/** The one plain sentence that explains the difference, shown where the category is chosen. */
export const CATEGORY_EXPLANATION =
  'Tools are counted. Equipment and machinery are single pieces tracked by serial number.';

/** What a category means for the form: single pieces need a serial number and are always one. */
export interface CategoryRules {
  /** Equipment and machinery: one identifiable piece. */
  readonly singlePiece: boolean;
  readonly serialRequired: boolean;
  /** The quantity when it is not the person's to choose (a single piece is 1); null when they type it. */
  readonly fixedQuantity: number | null;
}

export function categoryRules(category: InventoryCategory | null | undefined): CategoryRules {
  const singlePiece = category === 'Equipment' || category === 'Machinery';
  return { singlePiece, serialRequired: singlePiece, fixedQuantity: singlePiece ? 1 : null };
}

export function isSinglePiece(category: InventoryCategory): boolean {
  return categoryRules(category).singlePiece;
}

export const ITEM_STATUS: Record<
  InventoryItemStatus,
  { readonly label: string; readonly tone: ChipTone; readonly icon: IconName }
> = {
  Active: { label: 'Active', tone: 'success', icon: 'check-circle' },
  Retired: { label: 'Retired', tone: 'neutral', icon: 'archive' },
};

const quantityFormat = new Intl.NumberFormat('en-US', { maximumFractionDigits: 4 });

/** 14 gives "14", 2.5 gives "2.5", 1200 gives "1,200". Quantities have at most four decimals. */
export function formatQuantity(quantity: number): string {
  return quantityFormat.format(quantity);
}

/** "14 pc". The unit is free text the person chose when registering the item. */
export function quantityWithUnit(quantity: number, unit: string): string {
  return unit ? `${formatQuantity(quantity)} ${unit}` : formatQuantity(quantity);
}

export const WAREHOUSE_NAME = 'Warehouse';

/** The name a balance goes by: "Warehouse" or the project's name. */
export function balanceName(balance: StockBalance): string {
  return balance.location === 'Warehouse' ? WAREHOUSE_NAME : (balance.projectName ?? 'A project');
}

/** One compact place chip for a balance: "Warehouse 14". */
export function balanceLabel(balance: StockBalance): string {
  return `${balanceName(balance)} ${formatQuantity(balance.quantity)}`;
}
