import { Project } from '../../projects/projects.models';
import {
  BudgetLine,
  CostEntry,
  CostSummary,
  CostingReference,
  MoneyTotals,
  TotaledPage,
} from '../costing.models';

// Plain data for the specs, shaped as the API returns it (a miniature Riverside Residences). No test framework is used here.

export const PROJECT_ID = '7dbf3734-a33b-481c-a877-fb4c4e002bce';
export const SUB_PROJECT_ID = '3f2504e0-4f89-41d3-9a0c-0305e82c3301';

export function projectFixture(overrides: Partial<Project> = {}): Project {
  return {
    id: PROJECT_ID,
    name: 'Riverside Residences',
    client: 'Riverside Land Corp.',
    location: 'Cebu City',
    contractPriceMinor: 4850000000,
    startDate: '2026-05-01',
    targetEndDate: '2027-07-01',
    status: 'Active',
    archiveReason: null,
    archiveNote: null,
    archivedAtUtc: null,
    subProjects: [
      { id: SUB_PROJECT_ID, name: 'Tower A', startDate: '2026-05-01', status: 'Active' },
      {
        id: '9a1c6b7e-1111-4222-8333-444455556666',
        name: 'Old wing',
        startDate: '2026-05-01',
        status: 'Archived',
      },
    ],
    version: 3,
    ...overrides,
  };
}

export function referenceFixture(): CostingReference {
  return {
    divisions: [
      {
        number: 3,
        mainScope: 'Concrete',
        scopes: [
          { code: '3100', name: 'Concrete Forms and Accessories' },
          { code: '3300', name: 'Cast-In-Place Concrete' },
        ],
      },
      {
        number: 15,
        mainScope: 'Mechanical',
        scopes: [{ code: '15200', name: 'Process Piping' }],
      },
    ],
    units: ['bag', 'kg', 'm³', 'pc'],
    itemTypes: [
      { value: 'Material', label: 'Material' },
      { value: 'Labor', label: 'Labor' },
      { value: 'Equipment', label: 'Equipment' },
      { value: 'Subcontract', label: 'Subcontract work' },
      { value: 'Other', label: 'Other' },
    ],
    vatTreatments: [
      {
        value: 'Inclusive',
        label: 'Unit cost includes VAT',
        hint: 'Choose this when the price on the receipt already has VAT in it.',
      },
      {
        value: 'Exclusive',
        label: 'Unit cost is before VAT',
        hint: 'Choose this when the price is before VAT.',
      },
      { value: 'Exempt', label: 'No VAT', hint: 'Choose this when no VAT is charged on the line.' },
    ],
    vatRateBps: 1200,
  };
}

export function budgetLineFixture(overrides: Partial<BudgetLine> = {}): BudgetLine {
  return {
    id: 'bl-1',
    projectId: PROJECT_ID,
    subProjectId: null,
    subProjectName: null,
    division: 3,
    mainScope: 'Concrete',
    subScope: 'Cast-In-Place Concrete',
    wbsCode: '3300',
    description: 'Ready-mix concrete 4000 psi',
    itemType: 'Material',
    quantity: 600,
    unit: 'm³',
    unitCostMinor: 580000,
    vatTreatment: 'Inclusive',
    vatRateBps: 1200,
    netMinor: 310714286,
    vatMinor: 37285714,
    grossMinor: 348000000,
    entryDate: '2026-05-01',
    remarks: null,
    status: 'Active',
    createdAtUtc: '2026-11-01T12:29:17Z',
    removedAtUtc: null,
    removedReason: null,
    version: 4,
    ...overrides,
  };
}

export function costEntryFixture(overrides: Partial<CostEntry> = {}): CostEntry {
  return {
    id: 'ce-1',
    projectId: PROJECT_ID,
    subProjectId: null,
    subProjectName: null,
    division: 15,
    mainScope: 'Mechanical',
    subScope: 'Process Piping',
    wbsCode: '15200',
    description: 'Pipe rental and fittings',
    itemType: 'Equipment',
    quantity: 1,
    unit: 'lot',
    unitCostMinor: 21500000,
    vatTreatment: 'Inclusive',
    vatRateBps: 1200,
    netMinor: 19196429,
    vatMinor: 2303571,
    grossMinor: 21500000,
    entryDate: '2026-10-23',
    remarks: null,
    origin: 'Manual',
    originReferenceId: null,
    sourceDealer: 'Cebu Plumbing Supply',
    invoiceNumber: 'CPS-7781',
    status: 'Posted',
    createdAtUtc: '2026-11-01T12:29:17Z',
    voidedAtUtc: null,
    voidReason: null,
    canEdit: true,
    version: 2,
    ...overrides,
  };
}

export const TOTALS: MoneyTotals = {
  netMinor: 1624821428,
  vatMinor: 166178572,
  grossMinor: 1791000000,
};

export function pageWithTotals<T>(
  items: readonly T[],
  totals: MoneyTotals = TOTALS,
  total = items.length,
): TotaledPage<T> {
  return { items, total, page: 1, pageSize: 25, totals };
}

/** Riverside: concrete over budget at 105%, rebar near its limit, a pipe rental nobody budgeted. */
export function summaryFixture(overrides: Partial<CostSummary> = {}): CostSummary {
  return {
    projectId: PROJECT_ID,
    projectName: 'Riverside Residences',
    projectStatus: 'Active',
    basis: 'IncludingVat',
    contractPriceMinor: 4850000000,
    position: {
      budgetMinor: 1791000000,
      actualMinor: 721000000,
      remainingMinor: 1070000000,
      usedBps: 4026,
      health: 'OnTrack',
    },
    marginAfterActualMinor: 4129000000,
    budgetMarginMinor: 3059000000,
    byDivision: [
      {
        division: 15,
        name: 'Mechanical',
        budgetMinor: 185000000,
        actualMinor: 21500000,
        remainingMinor: 163500000,
        usedBps: 1162,
        health: 'OnTrack',
        wbs: [
          {
            wbsCode: '15200',
            name: 'Process Piping',
            budgetMinor: 0,
            actualMinor: 21500000,
            remainingMinor: -21500000,
            usedBps: null,
            health: 'Unbudgeted',
          },
          {
            wbsCode: '15400',
            name: 'Plumbing Fixtures and Equipment',
            budgetMinor: 185000000,
            actualMinor: 0,
            remainingMinor: 185000000,
            usedBps: 0,
            health: 'OnTrack',
          },
        ],
      },
      {
        division: 3,
        name: 'Concrete',
        budgetMinor: 806400000,
        actualMinor: 577800000,
        remainingMinor: 228600000,
        usedBps: 7165,
        health: 'OnTrack',
        wbs: [
          {
            wbsCode: '3300',
            name: 'Cast-In-Place Concrete',
            budgetMinor: 348000000,
            actualMinor: 366075000,
            remainingMinor: -18075000,
            usedBps: 10519,
            health: 'OverBudget',
          },
          {
            wbsCode: '3200',
            name: 'Concrete Reinforcement',
            budgetMinor: 218400000,
            actualMinor: 211725000,
            remainingMinor: 6675000,
            usedBps: 9694,
            health: 'NearLimit',
          },
        ],
      },
      {
        division: 9,
        name: 'Finishes',
        budgetMinor: 405600000,
        actualMinor: 86900000,
        remainingMinor: 318700000,
        usedBps: 2143,
        health: 'OnTrack',
        wbs: [
          {
            wbsCode: '9300',
            name: 'Tile',
            budgetMinor: 405600000,
            actualMinor: 86900000,
            remainingMinor: 318700000,
            usedBps: 2143,
            health: 'OnTrack',
          },
        ],
      },
    ],
    bySubProject: [
      {
        subProjectId: null,
        name: 'Whole project',
        budgetMinor: 1791000000,
        actualMinor: 721000000,
        remainingMinor: 1070000000,
        usedBps: 4026,
        health: 'OnTrack',
      },
    ],
    byItemType: [
      {
        itemType: 'Equipment',
        label: 'Equipment',
        budgetMinor: 0,
        actualMinor: 21500000,
        remainingMinor: -21500000,
        usedBps: null,
        health: 'Unbudgeted',
      },
      {
        itemType: 'Material',
        label: 'Material',
        budgetMinor: 1241000000,
        actualMinor: 699500000,
        remainingMinor: 541500000,
        usedBps: 5637,
        health: 'OnTrack',
      },
    ],
    ...overrides,
  };
}
