// Sample stock for the specs: the demo company's claw hammers and generator, shaped as the API returns them.
import { ApiError } from '../../core/api/api-error';
import {
  InventoryItem,
  InventoryListItem,
  ProjectStock,
  StockBalance,
  StockMovement,
} from './inventory.models';

export const RIVERSIDE_ID = '11111111-1111-4111-8111-111111111111';
export const HARBOR_ID = '22222222-2222-4222-8222-222222222222';
export const ANNEX_ID = '33333333-3333-4333-8333-333333333333';

export const PROJECT_LOOKUP = [
  { id: HARBOR_ID, name: 'Harbor Warehouse', status: 'Planning' },
  { id: ANNEX_ID, name: 'Old School Annex', status: 'Archived' },
  { id: RIVERSIDE_ID, name: 'Riverside Residences', status: 'Active' },
];

export const warehouse = (quantity: number): StockBalance => ({
  location: 'Warehouse',
  projectId: null,
  projectName: null,
  quantity,
});

export const onProject = (
  quantity: number,
  projectId = RIVERSIDE_ID,
  projectName = 'Riverside Residences',
): StockBalance => ({ location: 'Project', projectId, projectName, quantity });

/** 24 claw hammers: 14 in the warehouse, 10 on Riverside, each costing 450.00. */
export function hammers(overrides: Partial<InventoryItem> = {}): InventoryItem {
  return {
    id: 'item-hammer',
    description: 'Claw hammer 16 oz',
    category: 'Tool',
    brand: 'Stanley',
    serialNumber: null,
    unit: 'pc',
    sourceDealer: 'Hardware Hub',
    invoiceNumber: 'HH-1001',
    purchaseDate: '2026-06-01',
    unitCostMinor: 45000,
    remarks: null,
    status: 'Active',
    retiredReason: null,
    totalQuantity: 24,
    balances: [warehouse(14), onProject(10)],
    version: 3,
    ...overrides,
  };
}

/** A single piece on Riverside with a serial number and a cost of 4,850,000.00. */
export function excavator(overrides: Partial<InventoryItem> = {}): InventoryItem {
  return {
    id: 'item-excavator',
    description: 'Excavator PC200',
    category: 'Machinery',
    brand: 'Komatsu',
    serialNumber: 'PC200-8-A41127',
    unit: 'unit',
    sourceDealer: 'Heavy Machinery Sales',
    invoiceNumber: 'HMS-5509',
    purchaseDate: '2026-06-01',
    unitCostMinor: 485000000,
    remarks: null,
    status: 'Active',
    retiredReason: null,
    totalQuantity: 1,
    balances: [onProject(1)],
    version: 5,
    ...overrides,
  };
}

export function listItemOf(item: InventoryItem): InventoryListItem {
  return {
    id: item.id,
    description: item.description,
    category: item.category,
    brand: item.brand,
    serialNumber: item.serialNumber,
    unit: item.unit,
    unitCostMinor: item.unitCostMinor,
    status: item.status,
    totalQuantity: item.totalQuantity,
    balances: item.balances,
  };
}

export function projectStockOf(item: InventoryItem, quantity: number): ProjectStock {
  return {
    itemId: item.id,
    description: item.description,
    category: item.category,
    brand: item.brand,
    serialNumber: item.serialNumber,
    unit: item.unit,
    quantity,
  };
}

export function movement(overrides: Partial<StockMovement> = {}): StockMovement {
  return {
    id: 'move-1',
    type: 'Transfer',
    fromName: 'Warehouse',
    fromProjectId: null,
    toName: 'Riverside Residences',
    toProjectId: RIVERSIDE_ID,
    quantity: 10,
    date: '2026-08-01',
    chargedAmountMinor: 0,
    note: null,
    recordedBy: 'Wilson Warehouse-Keeper',
    recordedAtUtc: '2026-08-01T02:00:00Z',
    isReversed: false,
    reversalOfMovementId: null,
    ...overrides,
  };
}

/** The history of the hammers, newest first: a move to Riverside and the first purchase. */
export const HAMMER_HISTORY: readonly StockMovement[] = [
  movement({ id: 'move-transfer', note: 'For the formwork crew' }),
  movement({
    id: 'move-purchase',
    type: 'Purchase',
    fromName: null,
    toName: 'Warehouse',
    toProjectId: null,
    quantity: 24,
    date: '2026-06-01',
    note: 'First stock received',
  }),
];

export const ruleViolation = (detail: string) =>
  new ApiError({ status: 409, title: "That can't be done", detail, code: 'rule_violation' });

export const conflict = () =>
  new ApiError({
    status: 409,
    title: 'This record changed',
    detail:
      'Someone else changed this record while you were editing. Reload it to see the latest version, then try again.',
    code: 'concurrency_conflict',
  });

export const fieldProblem = (errors: Record<string, string[]>) =>
  new ApiError({
    status: 422,
    title: 'Some details need your attention',
    detail: 'Check the highlighted fields and try again.',
    code: 'validation_failed',
    errors,
  });
