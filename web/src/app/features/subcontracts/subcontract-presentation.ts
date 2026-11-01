import { IconName } from '../../shared/ui/icon/icon';
import { ChipTone } from '../../shared/ui/status-chip/status-chip';
import { VatTreatment } from '../costing/costing.models';
import {
  PaymentMethod,
  Subcontract,
  SubcontractClaim,
  SubcontractStatus,
} from './subcontracts.models';

// How subcontracts are put into words. Pure functions, so the list, the page, the project tab and the dialogs say the same thing.

export interface ChipLook {
  readonly label: string;
  readonly tone: ChipTone;
  readonly icon: IconName;
}

export const SUBCONTRACT_STATUS: Record<SubcontractStatus, ChipLook> = {
  Draft: { label: 'Draft', tone: 'neutral', icon: 'pencil' },
  Active: { label: 'Awarded', tone: 'info', icon: 'play' },
  Completed: { label: 'Completed', tone: 'success', icon: 'check-circle' },
  Cancelled: { label: 'Cancelled', tone: 'neutral', icon: 'close' },
};

export const STATUS_FILTERS: readonly { value: '' | SubcontractStatus; label: string }[] = [
  { value: '', label: 'All' },
  { value: 'Draft', label: 'Draft' },
  { value: 'Active', label: 'Awarded' },
  { value: 'Completed', label: 'Completed' },
  { value: 'Cancelled', label: 'Cancelled' },
];

export function claimLook(claim: Pick<SubcontractClaim, 'status' | 'claimNumber' | 'isStale'>): ChipLook {
  if (claim.status === 'Certified') return { label: 'Certified', tone: 'success', icon: 'check-circle' };
  if (claim.status === 'Voided')
    return { label: claim.claimNumber === null ? 'Discarded' : 'Voided', tone: 'neutral', icon: 'minus-circle' };
  return claim.isStale
    ? { label: 'Needs updating', tone: 'warning', icon: 'warning' }
    : { label: 'Draft', tone: 'neutral', icon: 'pencil' };
}

/** "Claim 2", or "Draft claim" before it is certified. */
export function claimName(claim: Pick<SubcontractClaim, 'claimNumber'>): string {
  return claim.claimNumber === null ? 'Draft claim' : `Claim ${claim.claimNumber}`;
}

/** What the contract sum's amount means: the words beside it. */
export const VAT_WORDS: Record<VatTreatment, string> = {
  Inclusive: 'VAT included',
  Exclusive: 'before VAT',
  Exempt: 'no VAT',
};

export const VAT_CHOICES: readonly { value: VatTreatment; label: string }[] = [
  { value: 'Exclusive', label: 'Before VAT (VAT is added)' },
  { value: 'Inclusive', label: 'Includes VAT' },
  { value: 'Exempt', label: 'No VAT' },
];

export const PAYMENT_METHODS: readonly { value: PaymentMethod; label: string }[] = [
  { value: 'BankTransfer', label: 'Bank transfer' },
  { value: 'Cheque', label: 'Cheque' },
  { value: 'Cash', label: 'Cash' },
  { value: 'Other', label: 'Other' },
];

export function methodLabel(method: string): string {
  return PAYMENT_METHODS.find((item) => item.value === method)?.label ?? method;
}

export interface Abilities {
  /** subcontracts.manage: drafts subcontracts and claims. */
  readonly manage: boolean;
  /** subcontracts.approve: awards, certifies, voids, records payments, completes. */
  readonly approve: boolean;
}

/** Which actions a person may take on this subcontract now. Absent actions are not shown at all. */
export interface SubcontractActions {
  readonly edit: boolean;
  readonly award: boolean;
  readonly cancel: boolean;
  readonly addVariation: boolean;
  readonly newClaim: boolean;
  readonly complete: boolean;
  readonly releaseRetention: boolean;
  /** The open draft claim, when there is one. */
  readonly draft: SubcontractClaim | null;
  readonly editDraft: boolean;
  readonly certifyDraft: boolean;
  /** The certified claim that may be voided (the latest, with nothing paid on it). */
  readonly voidableClaimId: string | null;
  readonly pay: boolean;
  readonly voidPayment: boolean;
}

const NONE: SubcontractActions = {
  edit: false,
  award: false,
  cancel: false,
  addVariation: false,
  newClaim: false,
  complete: false,
  releaseRetention: false,
  draft: null,
  editDraft: false,
  certifyDraft: false,
  voidableClaimId: null,
  pay: false,
  voidPayment: false,
};

export function subcontractActions(subcontract: Subcontract, can: Abilities): SubcontractActions {
  if (subcontract.projectArchived) return NONE;
  const { status } = subcontract;
  const draft = subcontract.claims.find((claim) => claim.status === 'Draft') ?? null;
  const certified = subcontract.claims.filter((claim) => claim.status === 'Certified');
  const latest = certified.reduce<SubcontractClaim | null>(
    (best, claim) => (best === null || (claim.claimNumber ?? 0) > (best.claimNumber ?? 0) ? claim : best),
    null,
  );
  const active = status === 'Active';
  const open = active || status === 'Completed';
  const stateCan = status === 'Draft' ? can.manage : can.approve;
  return {
    edit: status === 'Draft' && can.manage,
    award: status === 'Draft' && can.approve,
    cancel: (status === 'Draft' || (active && certified.length === 0)) && stateCan,
    addVariation: (status === 'Draft' || active) && stateCan,
    newClaim: active && can.manage && draft === null,
    complete: active && can.approve && draft === null,
    releaseRetention: status === 'Completed' && can.approve && subcontract.retentionBalanceMinor > 0,
    draft,
    editDraft: active && can.manage && draft !== null,
    certifyDraft: active && can.approve && draft !== null && !draft.isStale,
    voidableClaimId:
      open && can.approve && latest !== null && latest.paidMinor === 0 ? latest.id : null,
    pay: open && can.approve && subcontract.outstandingMinor > 0,
    voidPayment: open && can.approve,
  };
}

/** The one thing to do next, as the page's primary button, or null when there is nothing for this person to do. */
export type NextStep =
  | { readonly kind: 'award'; readonly label: string }
  | { readonly kind: 'certify'; readonly label: string; readonly claimId: string }
  | { readonly kind: 'update-claim'; readonly label: string; readonly claimId: string }
  | { readonly kind: 'pay'; readonly label: string; readonly claimId: string }
  | { readonly kind: 'new-claim'; readonly label: string }
  | { readonly kind: 'release-retention'; readonly label: string };

export function nextStep(subcontract: Subcontract, actions: SubcontractActions): NextStep | null {
  if (actions.award) return { kind: 'award', label: 'Award subcontract' };
  const draft = actions.draft;
  if (draft && actions.certifyDraft) return { kind: 'certify', label: 'Certify claim', claimId: draft.id };
  if (draft && draft.isStale && actions.editDraft)
    return { kind: 'update-claim', label: 'Update claim', claimId: draft.id };
  if (actions.pay) {
    const owed = oldestOwed(subcontract);
    if (owed) return { kind: 'pay', label: `Record payment for ${claimName(owed).toLowerCase()}`, claimId: owed.id };
  }
  if (actions.newClaim) return { kind: 'new-claim', label: 'New claim' };
  if (actions.releaseRetention) return { kind: 'release-retention', label: 'Release retention' };
  return null;
}

/** The earliest certified claim that still has something owed on it. */
export function oldestOwed(subcontract: Subcontract): SubcontractClaim | null {
  return (
    subcontract.claims
      .filter((claim) => claim.status === 'Certified' && claim.outstandingMinor > 0)
      .sort((a, b) => (a.claimNumber ?? 0) - (b.claimNumber ?? 0))[0] ?? null
  );
}

/** A plain line saying who must act when this person has nothing to do. Empty when they have a next step or nobody must act. */
export function waitingLine(subcontract: Subcontract, actions: SubcontractActions, step: NextStep | null): string {
  if (subcontract.projectArchived) return 'This project is archived, so its subcontracts can be read but not changed.';
  if (step) return '';
  if (subcontract.status === 'Draft') return 'An accountant or administrator awards this subcontract once its terms are right.';
  if (subcontract.status === 'Active' && actions.draft)
    return actions.draft.isStale
      ? 'The draft claim needs updating because certified work changed. A project manager or cost engineer saves it again.'
      : 'The draft claim is waiting for an accountant or administrator to certify it.';
  return '';
}
