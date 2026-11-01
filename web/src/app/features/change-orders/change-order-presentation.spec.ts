import {
  allowedActions,
  baselineView,
  effectSummary,
  signedMoney,
  statusLook,
} from './change-order-presentation';
import { baselineFixture } from './change-orders.fixtures';

describe('change order presentation', () => {
  it('writes a change of money with its sign, and no change in words', () => {
    expect(signedMoney(99_999, 'PHP')).toBe('+₱999.99');
    expect(signedMoney(-56_000, 'PHP')).toBe('-₱560.00');
    expect(signedMoney(0, 'PHP')).toBe('No change');
  });

  it('sums an order up in one phrase, leaving out what does not change', () => {
    expect(effectSummary(99_999, 150_000, 'PHP')).toBe('Budget +₱999.99 · Price +₱1,500.00');
    expect(effectSummary(-56_000, 0, 'PHP')).toBe('Budget -₱560.00');
    expect(effectSummary(0, 0, 'PHP')).toBe('No change to the budget or the price');
  });

  it('tells a discarded order from a voided one', () => {
    expect(statusLook('Submitted').label).toBe('Waiting for approval');
    expect(statusLook('Voided', 'Approved')).toEqual({
      label: 'Voided',
      tone: 'warning',
      icon: 'restore',
    });
    expect(statusLook('Voided', 'Draft').label).toBe('Discarded');
    expect(statusLook('Voided', 'Submitted').label).toBe('Discarded');
  });

  it('shows the server figures as original, changes and revised, for the budget and the price', () => {
    const view = baselineView(baselineFixture(), 'PHP');

    expect(view.rows).toEqual([
      { label: 'Budget', original: '₱7,120.00', changes: '+₱999.99', revised: '₱8,119.99' },
      {
        label: 'Contract price',
        original: '₱500,000.00',
        changes: '+₱1,500.00',
        revised: '₱501,500.00',
      },
    ]);
    expect(view.sentence).toBe(
      '1 approved change order explains the difference from the original.',
    );
    expect(view.pending).toBe('1 change order waiting for approval: Budget -₱560.00 if approved.');
    expect(view.reconciles).toBe(true);
  });

  it('leaves out the price row when the project has no price, and says when nothing is approved yet', () => {
    const view = baselineView(
      baselineFixture({
        originalContractPriceMinor: null,
        revisedContractPriceMinor: null,
        approvedCount: 0,
        approvedBudgetGrossMinor: 0,
        pendingCount: 0,
      }),
      'PHP',
    );

    expect(view.rows.map((row) => row.label)).toEqual(['Budget']);
    expect(view.sentence).toBe(
      'No change order has been approved yet, so the budget is still the original.',
    );
    expect(view.pending).toBeNull();
  });

  it('offers each step only to the people who may take it and never on an archived project', () => {
    const manager = { manage: true, approve: false };
    const approver = { manage: false, approve: true };

    expect(allowedActions('Draft', manager, false)).toEqual({
      edit: true,
      submit: true,
      approve: false,
      reject: false,
      void: false,
      discard: true,
    });
    expect(allowedActions('Submitted', approver, false)).toMatchObject({
      approve: true,
      reject: true,
      discard: false,
    });
    expect(allowedActions('Submitted', manager, false)).toMatchObject({
      approve: false,
      discard: true,
    });
    expect(allowedActions('Approved', approver, false)).toMatchObject({
      void: true,
      discard: false,
    });
    expect(
      Object.values(allowedActions('Approved', { manage: true, approve: true }, true)),
    ).not.toContain(true);
    expect(
      Object.values(allowedActions('Rejected', { manage: true, approve: true }, false)),
    ).not.toContain(true);
  });
});
