import { Bill, BillListItem, BillPreview, ProjectBilling, Receivables } from './billing.models';

// Figures as the API returns them, written out by hand: a ₱11,200,000.00 contract, 10% retention, 2% withholding, 30 days.

export function listBill(overrides: Partial<BillListItem> = {}): BillListItem {
  return {
    id: 'b1',
    number: 'BILL-0001',
    projectId: 'p1',
    projectName: 'Riverside Residences',
    kind: 'Progress',
    status: 'Issued',
    billingDate: '2026-09-30',
    dueDate: '2026-10-30',
    description: 'Progress billing no. 1',
    grossMinor: 224_000_000,
    netReceivableMinor: 197_600_000,
    paidMinor: 100_000_000,
    outstandingMinor: 97_600_000,
    isOverdue: true,
    version: 3,
    ...overrides,
  };
}

export function projectBilling(overrides: Partial<ProjectBilling> = {}): ProjectBilling {
  return {
    projectId: 'p1',
    projectName: 'Riverside Residences',
    projectStatus: 'Active',
    contractPriceMinor: 1_120_000_000,
    billedToDateMinor: 224_000_000,
    percentBilledBps: 2_000,
    remainingToBillMinor: 896_000_000,
    retentionWithheldMinor: 22_400_000,
    retentionReleasedMinor: 0,
    retentionHeldMinor: 22_400_000,
    collectedMinor: 100_000_000,
    outstandingMinor: 97_600_000,
    overdueMinor: 97_600_000,
    overdueCount: 1,
    canReleaseRetention: false,
    advanceBilledMinor: 0,
    advanceRecoveredMinor: 0,
    advanceToRecoverMinor: 0,
    advanceBillId: null,
    advanceToBillMinor: null,
    terms: {
      retentionBps: 1_000,
      creditableWithholdingBps: 200,
      paymentTermsDays: 30,
      advanceBps: 0,
      vatWithholdingBps: 0,
      isGovernmentClient: false,
      isDefault: false,
      version: 1,
    },
    vatRateBps: 1_200,
    bills: { items: [listBill()], total: 1, page: 1, pageSize: 25 },
    ...overrides,
  };
}

export function bill(overrides: Partial<Bill> = {}): Bill {
  return {
    id: 'b2',
    number: null,
    projectId: 'p1',
    projectName: 'Riverside Residences',
    projectStatus: 'Active',
    contractPriceMinor: 1_120_000_000,
    kind: 'Progress',
    status: 'Draft',
    billingDate: '2026-10-31',
    dueDate: '2026-11-30',
    description: 'Progress billing no. 2',
    cumulativeWorkMinor: 560_000_000,
    previousCumulativeWorkMinor: 224_000_000,
    grossMinor: 336_000_000,
    netMinor: 300_000_000,
    vatMinor: 36_000_000,
    vatRateBps: 1_200,
    retentionBps: 1_000,
    retentionMinor: 33_600_000,
    withholdingBps: 200,
    withholdingMinor: 6_000_000,
    vatWithholdingBps: 0,
    vatWithholdingMinor: 0,
    advanceBps: 0,
    advanceRecoveryMinor: 0,
    netReceivableMinor: 296_400_000,
    paymentTermsDays: 30,
    paidMinor: 0,
    outstandingMinor: 0,
    isOverdue: false,
    isLatestIssued: false,
    isStale: false,
    createdByName: 'Paolo Project-Manager',
    createdAtUtc: '2026-10-31T02:00:00Z',
    issuedByName: null,
    issuedAtUtc: null,
    voidedByName: null,
    voidedAtUtc: null,
    voidReason: null,
    payments: [],
    version: 1,
    ...overrides,
  };
}

/** The draft above once issued, with one payment recorded. */
export function issuedBill(overrides: Partial<Bill> = {}): Bill {
  return bill({
    number: 'BILL-0002',
    status: 'Issued',
    issuedByName: 'Alma Accountant',
    issuedAtUtc: '2026-10-31T03:00:00Z',
    isLatestIssued: true,
    paidMinor: 100_000_00,
    outstandingMinor: 286_400_000,
    payments: [
      {
        id: 'pay1',
        amountMinor: 100_000_00,
        paidOn: '2026-11-01',
        method: 'Cheque',
        reference: 'Cheque 004512',
        note: null,
        recordedByName: 'Alma Accountant',
        recordedAtUtc: '2026-11-01T03:00:00Z',
        isVoided: false,
        voidedByName: null,
        voidedAtUtc: null,
        voidReason: null,
      },
    ],
    version: 4,
    ...overrides,
  });
}

export function preview(overrides: Partial<BillPreview> = {}): BillPreview {
  return {
    contractPriceMinor: 1_120_000_000,
    previousCumulativeWorkMinor: 224_000_000,
    cumulativeWorkMinor: 560_000_000,
    grossMinor: 336_000_000,
    netMinor: 300_000_000,
    vatMinor: 36_000_000,
    vatRateBps: 1_200,
    retentionBps: 1_000,
    retentionMinor: 33_600_000,
    withholdingBps: 200,
    withholdingMinor: 6_000_000,
    vatWithholdingBps: 0,
    vatWithholdingMinor: 0,
    advanceRecoveryMinor: 0,
    netReceivableMinor: 296_400_000,
    paymentTermsDays: 30,
    dueDate: '2026-11-30',
    ...overrides,
  };
}

/**
 * The second bill above on a project that took a 15% down payment (₱1,680,000.00) and has 5% VAT withheld: it recovers 15% of
 * ₱5,600,000.00 less the ₱336,000.00 the first bill recovered, and 5% of the ₱3,000,000.00 before VAT is withheld.
 */
export const WITH_ADVANCE: Partial<Bill> & Partial<BillPreview> = {
  vatWithholdingBps: 500,
  vatWithholdingMinor: 15_000_000,
  advanceBps: 1_500,
  advanceRecoveryMinor: 50_400_000,
  netReceivableMinor: 231_000_000,
};

/** A 15% down payment bill on the ₱11,200,000.00 contract: a receivable only, nothing withheld. */
export function advanceBill(overrides: Partial<Bill> = {}): Bill {
  return bill({
    id: 'b0',
    kind: 'Advance',
    description: 'Down payment',
    cumulativeWorkMinor: null,
    previousCumulativeWorkMinor: 0,
    grossMinor: 168_000_000,
    netMinor: 168_000_000,
    vatMinor: 0,
    vatRateBps: 0,
    retentionBps: 0,
    retentionMinor: 0,
    withholdingBps: 0,
    withholdingMinor: 0,
    advanceBps: 1_500,
    netReceivableMinor: 168_000_000,
    ...overrides,
  });
}

export function receivables(overrides: Partial<Receivables> = {}): Receivables {
  return {
    outstandingMinor: 394_000_000,
    overdueMinor: 97_600_000,
    retentionHeldMinor: 56_000_000,
    rows: {
      items: [
        {
          projectId: 'p1',
          projectName: 'Riverside Residences',
          projectStatus: 'Active',
          billedToDateMinor: 560_000_000,
          collectedMinor: 100_000_000,
          outstandingMinor: 394_000_000,
          overdueMinor: 97_600_000,
          retentionHeldMinor: 56_000_000,
        },
      ],
      total: 1,
      page: 1,
      pageSize: 25,
    },
    ...overrides,
  };
}
