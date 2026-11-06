import { Permission } from '../../core/session/permissions';
import { Bond, BondEvent, BondListItem, BondTotals } from './bonds.models';

export const READER = [Permission.projectsRead, Permission.bondsRead];
export const MANAGER = [...READER, Permission.bondsManage];

/** A posted performance bond of 5,000,000.00 that is valid for another 100 days (the shape the API sends for an Active bond). */
export function listItem(overrides: Partial<BondListItem> = {}): BondListItem {
  return {
    id: 'b1',
    projectId: 'p1',
    projectName: 'Riverside Residences',
    direction: 'Posted',
    kind: 'Performance',
    issuerName: 'Metro Surety',
    bondNumber: 'PB-2026-014',
    otherPartyName: null,
    amountMinor: 500_000_000,
    currencyCode: 'PHP',
    issuedOn: '2026-06-01',
    validUntil: '2027-02-10',
    status: 'Active',
    expiry: 'Valid',
    daysLeft: 100,
    version: 0,
    ...overrides,
  };
}

export function event(overrides: Partial<BondEvent> = {}): BondEvent {
  return {
    id: 'e1',
    kind: 'Recorded',
    byName: 'Alma Accountant',
    atUtc: '2026-11-02T02:15:00Z',
    previousValidUntil: null,
    newValidUntil: null,
    eventDate: null,
    note: null,
    ...overrides,
  };
}

export function bond(overrides: Partial<Bond> = {}): Bond {
  return {
    ...listItem(),
    projectArchived: false,
    note: 'Original is in the safe',
    releasedOn: null,
    releaseNote: null,
    calledOn: null,
    callNote: null,
    voidReason: null,
    createdByName: 'Alma Accountant',
    createdAtUtc: '2026-11-02T02:15:00Z',
    history: [event()],
    ...overrides,
  };
}

export function totals(overrides: Partial<BondTotals> = {}): BondTotals {
  return {
    currencyCode: 'PHP',
    posted: { activeCount: 2, activeAmountMinor: 625_000_050 },
    received: { activeCount: 1, activeAmountMinor: 30_000_000 },
    expiredCount: 0,
    expiringSoonCount: 0,
    ...overrides,
  };
}
