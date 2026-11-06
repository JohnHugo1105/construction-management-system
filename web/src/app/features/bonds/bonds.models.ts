/** The bonds API as the server sends it. Dates are business dates ("2026-12-03"); amounts are whole centavos. */

export type BondDirection = 'Posted' | 'Received';
export type BondKind = 'Bid' | 'Performance' | 'AdvancePayment' | 'Warranty' | 'Other';
export type BondStatus = 'Active' | 'Released' | 'Called' | 'Void';

/** How close an Active bond is to its valid-until date. The server judges it on the company's calendar day. */
export type BondExpiry = 'Valid' | 'ExpiringSoon' | 'Expired';

export type BondEventKind = 'Recorded' | 'Edited' | 'Extended' | 'Released' | 'Called' | 'Voided';

/** One row of the register or a project's list. `expiry` and `daysLeft` are present only for an Active bond; `daysLeft` is negative once past. */
export interface BondListItem {
  readonly id: string;
  readonly projectId: string;
  readonly projectName: string;
  readonly direction: BondDirection;
  readonly kind: BondKind;
  readonly issuerName: string;
  readonly bondNumber: string;
  readonly otherPartyName: string | null;
  readonly amountMinor: number;
  readonly currencyCode: string;
  readonly issuedOn: string;
  readonly validUntil: string;
  readonly status: BondStatus;
  readonly expiry: BondExpiry | null;
  readonly daysLeft: number | null;
  readonly version: number;
}

export interface BondEvent {
  readonly id: string;
  readonly kind: BondEventKind;
  readonly byName: string | null;
  readonly atUtc: string;
  readonly previousValidUntil: string | null;
  readonly newValidUntil: string | null;
  readonly eventDate: string | null;
  readonly note: string | null;
}

/** One bond with its history (newest first). */
export interface Bond extends BondListItem {
  readonly projectArchived: boolean;
  readonly note: string | null;
  readonly releasedOn: string | null;
  readonly releaseNote: string | null;
  readonly calledOn: string | null;
  readonly callNote: string | null;
  readonly voidReason: string | null;
  readonly createdByName: string | null;
  readonly createdAtUtc: string;
  readonly history: readonly BondEvent[];
}

export interface BondDirectionTotal {
  readonly activeCount: number;
  readonly activeAmountMinor: number;
}

/** What the company has posted and holds in Active bonds, and how many need attention. */
export interface BondTotals {
  readonly currencyCode: string;
  readonly posted: BondDirectionTotal;
  readonly received: BondDirectionTotal;
  readonly expiredCount: number;
  readonly expiringSoonCount: number;
}

/** What a person types about a bond. The date fields are "2026-12-03", or empty. */
export interface BondDetailsInput {
  readonly kind: BondKind;
  readonly issuerName: string;
  readonly bondNumber: string;
  readonly amountMinor: number | null;
  readonly issuedOn: string;
  readonly validUntil: string;
  readonly otherPartyName: string | null;
  readonly note: string | null;
}

export interface NewBondInput extends BondDetailsInput {
  readonly direction: BondDirection;
}

export interface BondListParams {
  readonly projectId: string;
  readonly direction: '' | BondDirection;
  readonly kind: '' | BondKind;
  readonly status: '' | BondStatus;
  readonly expiry: '' | BondExpiry;
  readonly page: number;
  readonly pageSize: number;
}

/** A project the pickers offer (`GET /api/projects/lookup`). */
export interface ProjectChoice {
  readonly id: string;
  readonly name: string;
  readonly status: string;
}
