import { NEEDS_ACTION_PERMISSIONS } from './permissions';

describe('NEEDS_ACTION_PERMISSIONS', () => {
  // The server keeps the same list (NeedsActionGroups.ActingPermissions in src/ConstructOPX.Application/Features/NeedsAction/
  // NeedsActionRules.cs) and pins the same array in NeedsActionRulesTests. The list lives in two places because the navigation entry is
  // decided in the browser; the two tests are what keeps them in step. When a group gains or loses an acting permission, change the
  // server list, this list and both arrays together, or one of the two tests fails.
  const PINNED = [
    'purchasing.approve',
    'changeorders.approve',
    'billing.approve',
    'billing.manage',
    'subcontracts.approve',
    'payroll.approve',
    'purchasing.receive',
    'receiving.record',
    'bonds.manage',
    'progress.write',
    'attendance.write',
    'statutory.manage',
    'tasks.use',
    'diary.write',
    'documents.manage',
    'schedule.write',
  ];

  it('is the permissions that put work on the Needs your action page, in the server’s order', () => {
    expect([...NEEDS_ACTION_PERMISSIONS]).toEqual(PINNED);
  });
});
