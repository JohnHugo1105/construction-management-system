import { formatDate, formatDateTime } from '../../shared/presentation/business-time';
import { ChipTone } from '../../shared/ui/status-chip/status-chip';
import { IconName } from '../../shared/ui/icon/icon';
import {
  Bond,
  BondDirection,
  BondEvent,
  BondExpiry,
  BondKind,
  BondListItem,
  BondStatus,
} from './bonds.models';

// How bonds are put into words. Pure functions, so the register, the bond page, the project card and the dialogs say the same thing.
// The server decides the expiry state and the days left; nothing here works a date out.

export interface ChipLook {
  readonly label: string;
  readonly tone: ChipTone;
  readonly icon: IconName;
}

export const KIND_WORDS: Record<BondKind, string> = {
  Bid: 'Bid bond',
  Performance: 'Performance bond',
  AdvancePayment: 'Advance payment bond',
  Warranty: 'Warranty bond',
  Other: 'Bond',
};

export const KIND_CHOICES: readonly { value: BondKind; label: string }[] = [
  { value: 'Performance', label: 'Performance bond' },
  { value: 'AdvancePayment', label: 'Advance payment bond' },
  { value: 'Warranty', label: 'Warranty bond' },
  { value: 'Bid', label: 'Bid bond' },
  { value: 'Other', label: 'Other kind of bond' },
];

export const DIRECTION_CHOICES: readonly { value: BondDirection; label: string; hint: string }[] = [
  {
    value: 'Posted',
    label: 'Posted by us',
    hint: 'A bank or surety guarantees our work to the client.',
  },
  {
    value: 'Received',
    label: 'Received from a firm',
    hint: 'A subcontractor or supplier gave it to us.',
  },
];

/** "Performance bond PB-2026-014". */
export function bondTitle(bond: Pick<BondListItem, 'kind' | 'bondNumber'>): string {
  return `${KIND_WORDS[bond.kind]} ${bond.bondNumber}`;
}

/** Who gave it to whom, in one phrase. */
export function directionWords(bond: Pick<BondListItem, 'direction' | 'otherPartyName'>): string {
  if (bond.direction === 'Posted') return 'Posted to the client';
  return bond.otherPartyName ? `Received from ${bond.otherPartyName}` : 'Received from a firm';
}

/** "Expires in 12 days", "Expires tomorrow", "Expires today". */
export function expiresIn(daysLeft: number): string {
  if (daysLeft === 0) return 'Expires today';
  if (daysLeft === 1) return 'Expires tomorrow';
  return `Expires in ${daysLeft} days`;
}

/** "Expired 3 days ago", "Expired yesterday". */
export function expiredAgo(daysLeft: number): string {
  const days = Math.abs(daysLeft);
  return days === 1 ? 'Expired yesterday' : `Expired ${days} days ago`;
}

/** The chip for a bond: its expiry while Active (Valid, "Expires in N days", Expired), else how it ended. */
export function bondLook(bond: Pick<BondListItem, 'status' | 'expiry' | 'daysLeft'>): ChipLook {
  switch (bond.status) {
    case 'Released':
      return { label: 'Released', tone: 'neutral', icon: 'check' };
    case 'Called':
      return { label: 'Called', tone: 'warning', icon: 'warning' };
    case 'Void':
      return { label: 'Voided', tone: 'neutral', icon: 'minus-circle' };
    default:
      return activeLook(bond.expiry, bond.daysLeft);
  }
}

function activeLook(expiry: BondExpiry | null, daysLeft: number | null): ChipLook {
  if (expiry === 'Expired') return { label: 'Expired', tone: 'danger', icon: 'error' };
  if (expiry === 'ExpiringSoon' && daysLeft !== null)
    return { label: expiresIn(daysLeft), tone: 'warning', icon: 'clock' };
  return { label: 'Valid', tone: 'success', icon: 'check-circle' };
}

/** An Active bond that has lapsed or is about to: the one that needs its person's attention. */
export function needsAttention(bond: Pick<BondListItem, 'status' | 'expiry'>): boolean {
  return bond.status === 'Active' && bond.expiry !== null && bond.expiry !== 'Valid';
}

// ---- The register's state filter ----------------------------------------------------------------------------------------------

/** What the register's "Show" choice means. Active is the default: the question the page answers is what is in force and what lapses. */
export type BondState =
  'Active' | 'All' | 'Expired' | 'ExpiringSoon' | 'Released' | 'Called' | 'Void';

export const BOND_STATES: readonly BondState[] = [
  'Active',
  'All',
  'Expired',
  'ExpiringSoon',
  'Released',
  'Called',
  'Void',
];

export const STATE_CHOICES: readonly { value: BondState; label: string }[] = [
  { value: 'Active', label: 'Active bonds' },
  { value: 'Expired', label: 'Expired, still active' },
  { value: 'ExpiringSoon', label: 'Expiring within 30 days' },
  { value: 'Released', label: 'Released' },
  { value: 'Called', label: 'Called' },
  { value: 'Void', label: 'Voided' },
  { value: 'All', label: 'Every bond' },
];

/** The API filters a state stands for. Expired and expiring bonds are Active by definition, so the expiry filter alone says it. */
export function stateFilter(state: BondState): {
  status: '' | BondStatus;
  expiry: '' | BondExpiry;
} {
  switch (state) {
    case 'Active':
    case 'Released':
    case 'Called':
    case 'Void':
      return { status: state, expiry: '' };
    case 'Expired':
    case 'ExpiringSoon':
      return { status: '', expiry: state };
    default:
      return { status: '', expiry: '' };
  }
}

// ---- What a person can do ------------------------------------------------------------------------------------------------------

export interface BondActions {
  /** Change the details of an Active bond. */
  readonly edit: boolean;
  /** Move the valid-until date later. */
  readonly extend: boolean;
  /** The bond was returned or is no longer needed. */
  readonly release: boolean;
  /** The beneficiary claimed it. */
  readonly markCalled: boolean;
  /** Entered by mistake: any bond that is not already voided. */
  readonly voidBond: boolean;
}

/** Which steps are offered. Absent ones are not shown at all: a reader, an archived project and a closed bond get no buttons they cannot use. */
export function bondActions(
  bond: Pick<Bond, 'status' | 'projectArchived'>,
  canManage: boolean,
): BondActions {
  const open = canManage && !bond.projectArchived;
  const active = open && bond.status === 'Active';
  return {
    edit: active,
    extend: active,
    release: active,
    markCalled: active,
    voidBond: open && bond.status !== 'Void',
  };
}

// ---- History ---------------------------------------------------------------------------------------------------------------------

export interface HistoryLine {
  readonly id: string;
  readonly title: string;
  readonly meta: string;
  readonly note: string | null;
}

/** One step of a bond's history in words: what happened, then who and when. */
export function historyLine(event: BondEvent, timeZone: string): HistoryLine {
  const moved =
    event.previousValidUntil && event.newValidUntil
      ? `from ${formatDate(event.previousValidUntil)} to ${formatDate(event.newValidUntil)}`
      : '';
  const title = ((): string => {
    switch (event.kind) {
      case 'Recorded':
        return 'Recorded';
      case 'Edited':
        return moved ? `Details changed, valid-until date moved ${moved}` : 'Details changed';
      case 'Extended':
        return `Extended ${moved}`.trim();
      case 'Released':
        return `Released on ${formatDate(event.eventDate)}`;
      case 'Called':
        return `Marked as called on ${formatDate(event.eventDate)}`;
      default:
        return 'Voided';
    }
  })();
  const who = event.byName ?? 'Someone';
  return {
    id: event.id,
    title,
    meta: `${who}, ${formatDateTime(event.atUtc, timeZone)}`,
    note: event.note,
  };
}
