import { Permission } from '../../core/session/permissions';
import {
  Subcontract,
  SubcontractClaim,
  SubcontractListItem,
  SubcontractPayment,
} from './subcontracts.models';

export const READER = [Permission.projectsRead, Permission.subcontractsRead];
export const DRAFTER = [...READER, Permission.subcontractsManage];
export const APPROVER = [...READER, Permission.subcontractsApprove];

/** A certified claim 1: 250,000.00 before VAT, the figures the server works out at 12% VAT, 10% retention, 2% withholding. */
export function claim(overrides: Partial<SubcontractClaim> = {}): SubcontractClaim {
  return {
    id: 'c1',
    claimNumber: 1,
    status: 'Certified',
    claimDate: '2026-10-01',
    periodEnd: '2026-09-30',
    cumulativeWorkMinor: 25_000_000,
    previousCumulativeMinor: 0,
    thisClaimBaseMinor: 25_000_000,
    netMinor: 25_000_000,
    vatMinor: 3_000_000,
    grossMinor: 28_000_000,
    retentionMinor: 2_800_000,
    withholdingMinor: 500_000,
    netPayableMinor: 24_700_000,
    paidMinor: 0,
    outstandingMinor: 24_700_000,
    isStale: false,
    notes: null,
    costEntryId: 'cost-1',
    createdByName: 'Carla Cost-Engineer',
    createdAtUtc: '2026-10-01T02:00:00Z',
    certifiedByName: 'Alma Accountant',
    certifiedAtUtc: '2026-10-02T02:00:00Z',
    voidedByName: null,
    voidedAtUtc: null,
    voidReason: null,
    ...overrides,
  };
}

export function draftClaim(overrides: Partial<SubcontractClaim> = {}): SubcontractClaim {
  return claim({
    id: 'c2',
    claimNumber: null,
    status: 'Draft',
    cumulativeWorkMinor: 60_000_000,
    previousCumulativeMinor: 25_000_000,
    thisClaimBaseMinor: 35_000_000,
    netMinor: 35_000_000,
    vatMinor: 4_200_000,
    grossMinor: 39_200_000,
    retentionMinor: 3_920_000,
    withholdingMinor: 700_000,
    netPayableMinor: 34_580_000,
    outstandingMinor: 0,
    costEntryId: null,
    certifiedByName: null,
    certifiedAtUtc: null,
    ...overrides,
  });
}

export function payment(overrides: Partial<SubcontractPayment> = {}): SubcontractPayment {
  return {
    id: 'pay-1',
    kind: 'Claim',
    claimId: 'c1',
    claimNumber: 1,
    amountMinor: 10_000_000,
    paidOn: '2026-10-05',
    method: 'BankTransfer',
    reference: 'BDO-1001',
    note: null,
    status: 'Recorded',
    recordedByName: 'Alma Accountant',
    recordedAtUtc: '2026-10-05T02:00:00Z',
    voidedByName: null,
    voidedAtUtc: null,
    voidReason: null,
    ...overrides,
  };
}

/** An awarded subcontract: 1,000,000.00 before VAT with claim 1 certified and nothing paid yet. */
export function subcontract(overrides: Partial<Subcontract> = {}): Subcontract {
  return {
    id: 's1',
    number: 'SC-0001',
    title: 'Electrical works',
    notes: null,
    subcontractorId: 'f1',
    subcontractorName: 'Dela Cruz Electrical',
    projectId: 'p1',
    projectName: 'Riverside Residences',
    projectArchived: false,
    subProjectId: null,
    subProjectName: null,
    status: 'Active',
    vatTreatment: 'Exclusive',
    vatRateBps: 1_200,
    retentionBps: 1_000,
    withholdingBps: 200,
    wbsCode: '3300',
    workItem: 'Cast-In-Place Concrete',
    contractSumMinor: 100_000_000,
    variationsMinor: 0,
    revisedSumMinor: 100_000_000,
    revisedNetMinor: 100_000_000,
    revisedVatMinor: 12_000_000,
    revisedGrossMinor: 112_000_000,
    certifiedWorkMinor: 25_000_000,
    remainingWorkMinor: 75_000_000,
    percentCertifiedBps: 2_500,
    certifiedNetMinor: 25_000_000,
    certifiedGrossMinor: 28_000_000,
    retentionHeldMinor: 2_800_000,
    retentionReleasedMinor: 0,
    retentionBalanceMinor: 2_800_000,
    withholdingMinor: 500_000,
    netPayableMinor: 24_700_000,
    paidOnClaimsMinor: 0,
    outstandingMinor: 24_700_000,
    createdByName: 'Carla Cost-Engineer',
    createdAtUtc: '2026-09-01T02:00:00Z',
    awardedByName: 'Alma Accountant',
    awardedAtUtc: '2026-09-02T02:00:00Z',
    completedByName: null,
    completedAtUtc: null,
    cancelledByName: null,
    cancelledAtUtc: null,
    cancelReason: null,
    claims: [claim()],
    payments: [],
    variations: [],
    version: 4,
    ...overrides,
  };
}

export function listItem(overrides: Partial<SubcontractListItem> = {}): SubcontractListItem {
  return {
    id: 's1',
    number: 'SC-0001',
    title: 'Electrical works',
    subcontractorId: 'f1',
    subcontractorName: 'Dela Cruz Electrical',
    projectId: 'p1',
    projectName: 'Riverside Residences',
    subProjectName: null,
    status: 'Active',
    vatTreatment: 'Exclusive',
    revisedSumMinor: 100_000_000,
    certifiedWorkMinor: 25_000_000,
    percentCertifiedBps: 2_500,
    retentionHeldMinor: 2_800_000,
    paidMinor: 0,
    outstandingMinor: 24_700_000,
    version: 4,
    ...overrides,
  };
}
