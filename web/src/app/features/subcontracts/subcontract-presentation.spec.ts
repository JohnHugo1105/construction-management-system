import { previewLines } from './claim-dialog';
import {
  claimLook,
  nextStep,
  oldestOwed,
  subcontractActions,
  waitingLine,
} from './subcontract-presentation';
import { claim, draftClaim, payment, subcontract } from './subcontracts.fixtures';

const drafter = { manage: true, approve: false };
const approver = { manage: false, approve: true };
const reader = { manage: false, approve: false };

describe('subcontractActions', () => {
  it('lets a drafter edit and cancel a draft, and an approver award it', () => {
    const draft = subcontract({ status: 'Draft', claims: [], certifiedWorkMinor: 0 });

    expect(subcontractActions(draft, drafter)).toMatchObject({ edit: true, award: false, cancel: true, addVariation: true });
    expect(subcontractActions(draft, approver)).toMatchObject({ edit: false, award: true, cancel: false, addVariation: false });
    expect(nextStep(draft, subcontractActions(draft, approver))).toEqual({ kind: 'award', label: 'Award subcontract' });
    expect(nextStep(draft, subcontractActions(draft, drafter))).toBeNull();
  });

  it('offers a new claim to a drafter and payment of the oldest owed claim to an approver', () => {
    const active = subcontract();

    expect(nextStep(active, subcontractActions(active, drafter))).toEqual({ kind: 'new-claim', label: 'New claim' });
    expect(nextStep(active, subcontractActions(active, approver))).toEqual({
      kind: 'pay',
      label: 'Record payment for claim 1',
      claimId: 'c1',
    });
    // Certified work means it can no longer be cancelled.
    expect(subcontractActions(active, approver).cancel).toBe(false);
  });

  it('puts certifying an open draft first, and a stale draft goes back to the drafter', () => {
    const withDraft = subcontract({ claims: [claim(), draftClaim()] });
    expect(nextStep(withDraft, subcontractActions(withDraft, approver))).toEqual({
      kind: 'certify',
      label: 'Certify claim',
      claimId: 'c2',
    });
    expect(subcontractActions(withDraft, drafter).newClaim).toBe(false);

    const stale = subcontract({ claims: [draftClaim({ isStale: true })] });
    expect(subcontractActions(stale, approver).certifyDraft).toBe(false);
    expect(nextStep(stale, subcontractActions(stale, drafter))).toEqual({
      kind: 'update-claim',
      label: 'Update claim',
      claimId: 'c2',
    });
    expect(waitingLine(stale, subcontractActions(stale, reader), null)).toContain('needs updating');
  });

  it('voids only the latest certified claim with nothing paid on it', () => {
    const two = subcontract({ claims: [claim(), claim({ id: 'c3', claimNumber: 2 })] });
    expect(subcontractActions(two, approver).voidableClaimId).toBe('c3');

    const paid = subcontract({ claims: [claim({ paidMinor: 1_00, outstandingMinor: 24_699_900 })] });
    expect(subcontractActions(paid, approver).voidableClaimId).toBeNull();
  });

  it('releases retention only on a completed subcontract that still holds some', () => {
    const settled = { outstandingMinor: 0, claims: [claim({ outstandingMinor: 0, paidMinor: 24_700_000 })] };
    const completed = subcontract({ ...settled, status: 'Completed' });
    expect(nextStep(completed, subcontractActions(completed, approver))).toEqual({
      kind: 'release-retention',
      label: 'Release retention',
    });
    const released = subcontract({ ...settled, status: 'Completed', retentionBalanceMinor: 0 });
    expect(subcontractActions(released, approver).releaseRetention).toBe(false);
  });

  it('offers nothing on an archived project and says why', () => {
    const archived = subcontract({ projectArchived: true, payments: [payment()] });
    const actions = subcontractActions(archived, { manage: true, approve: true });

    expect(Object.values(actions).filter((value) => value === true)).toEqual([]);
    expect(waitingLine(archived, actions, null)).toBe(
      'This project is archived, so its subcontracts can be read but not changed.',
    );
  });
});

describe('claim words', () => {
  it('names each claim state', () => {
    expect(claimLook(claim()).label).toBe('Certified');
    expect(claimLook(draftClaim()).label).toBe('Draft');
    expect(claimLook(draftClaim({ isStale: true })).label).toBe('Needs updating');
    expect(claimLook(draftClaim({ status: 'Voided' })).label).toBe('Discarded');
    expect(claimLook(claim({ status: 'Voided' })).label).toBe('Voided');
  });

  it('finds the earliest claim still owed', () => {
    const owed = subcontract({
      claims: [claim({ id: 'c3', claimNumber: 2 }), claim({ outstandingMinor: 0 }), claim({ id: 'c4', claimNumber: 3 })],
    });
    expect(oldestOwed(owed)?.id).toBe('c3');
  });

  it('lays out the server breakdown as a payment certificate reads', () => {
    const lines = previewLines(draftClaim(), 'PHP', 'before VAT');

    expect(lines.map((line) => `${line.label}: ${line.amount}`)).toEqual([
      'This claim (before VAT): ₱350,000.00',
      'VAT: ₱42,000.00',
      'Total with VAT: ₱392,000.00',
      'Retention held: - ₱39,200.00',
      'Withholding tax: - ₱7,000.00',
      'To pay on this claim: ₱345,800.00',
    ]);
  });
});
