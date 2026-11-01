import { PROJECT_ID } from '../costing/testing/costing-fixtures';
import {
  BudgetBaseline,
  ChangeOrder,
  ChangeOrderLine,
  ChangeOrderListItem,
} from './change-orders.models';

/** A locked budget of ₱7,120.00 with one approved order (+₱999.99, price +₱1,500.00) and one waiting (-₱560.00). */
export function baselineFixture(overrides: Partial<BudgetBaseline> = {}): BudgetBaseline {
  return {
    projectId: PROJECT_ID,
    locked: true,
    lockedAtUtc: '2026-10-01T02:00:00Z',
    lockedBy: 'Paolo Project-Manager',
    originalBudgetNetMinor: 646_429,
    originalBudgetGrossMinor: 712_000,
    originalContractPriceMinor: 50_000_000,
    approvedCount: 1,
    approvedBudgetNetMinor: 78_570,
    approvedBudgetGrossMinor: 99_999,
    approvedContractPriceDeltaMinor: 150_000,
    revisedBudgetNetMinor: 724_999,
    revisedBudgetGrossMinor: 811_999,
    revisedContractPriceMinor: 50_150_000,
    activeLineCount: 3,
    currentBudgetNetMinor: 724_999,
    currentBudgetGrossMinor: 811_999,
    currentContractPriceMinor: 50_150_000,
    pendingCount: 1,
    pendingBudgetNetMinor: -50_000,
    pendingBudgetGrossMinor: -56_000,
    pendingContractPriceDeltaMinor: 0,
    reconciles: true,
    lastUnlockReason: null,
    lastUnlockedAtUtc: null,
    projectVersion: 4,
    ...overrides,
  };
}

/** The same budget before it was locked. */
export function openBaselineFixture(): BudgetBaseline {
  return baselineFixture({
    locked: false,
    lockedAtUtc: null,
    lockedBy: null,
    originalBudgetNetMinor: null,
    originalBudgetGrossMinor: null,
    originalContractPriceMinor: null,
    approvedCount: 0,
    approvedBudgetNetMinor: 0,
    approvedBudgetGrossMinor: 0,
    approvedContractPriceDeltaMinor: 0,
    revisedBudgetGrossMinor: 712_000,
    currentBudgetGrossMinor: 712_000,
    currentContractPriceMinor: 50_000_000,
    pendingCount: 0,
  });
}

export function listItemFixture(overrides: Partial<ChangeOrderListItem> = {}): ChangeOrderListItem {
  return {
    id: 'co-1',
    projectId: PROJECT_ID,
    number: 'CO-001',
    title: 'Basement waterproofing',
    reason: 'ClientRequest',
    status: 'Approved',
    voidedFromStatus: null,
    lineCount: 3,
    budgetEffectNetMinor: 78_570,
    budgetEffectGrossMinor: 99_999,
    contractPriceDeltaMinor: 150_000,
    createdAtUtc: '2026-10-02T02:00:00Z',
    submittedAtUtc: '2026-10-02T03:00:00Z',
    decidedAtUtc: '2026-10-03T03:00:00Z',
    voidedAtUtc: null,
    version: 3,
    ...overrides,
  };
}

export function lineFixture(overrides: Partial<ChangeOrderLine> = {}): ChangeOrderLine {
  return {
    id: 'col-1',
    lineNumber: 1,
    kind: 'Revise',
    budgetLineId: 'bl-1',
    subProjectId: null,
    subProjectName: null,
    division: 3,
    mainScope: 'Concrete',
    subScope: 'Cast-In-Place Concrete',
    wbsCode: '3300',
    description: 'Ready-mix concrete',
    itemType: 'Material',
    quantity: 12,
    unit: 'm³',
    unitCostMinor: 50_000,
    vatTreatment: 'Inclusive',
    vatRateBps: 1200,
    entryDate: '2026-02-01',
    remarks: null,
    netMinor: 535_714,
    vatMinor: 64_286,
    grossMinor: 600_000,
    previousQuantity: 10,
    previousUnitCostMinor: 50_000,
    previousNetMinor: 446_429,
    previousGrossMinor: 500_000,
    effectNetMinor: 89_285,
    effectVatMinor: 10_715,
    effectGrossMinor: 100_000,
    ...overrides,
  };
}

export function changeOrderFixture(overrides: Partial<ChangeOrder> = {}): ChangeOrder {
  return {
    id: 'co-1',
    projectId: PROJECT_ID,
    number: 'CO-001',
    title: 'Basement waterproofing',
    description: 'The client asked for a membrane.',
    reason: 'ClientRequest',
    status: 'Submitted',
    voidedFromStatus: null,
    contractPriceDeltaMinor: 150_000,
    budgetEffectNetMinor: 89_285,
    budgetEffectVatMinor: 10_715,
    budgetEffectGrossMinor: 100_000,
    createdBy: 'Carla Cost-Engineer',
    createdAtUtc: '2026-10-02T02:00:00Z',
    submittedBy: 'Carla Cost-Engineer',
    submittedAtUtc: '2026-10-02T03:00:00Z',
    decidedBy: null,
    decidedAtUtc: null,
    decisionReason: null,
    voidedBy: null,
    voidedAtUtc: null,
    voidReason: null,
    voidBlockedBy: null,
    lines: [lineFixture()],
    version: 2,
    ...overrides,
  };
}
