import {
  NeedsActionGroup,
  NeedsActionGroupKey,
  NeedsActionRow,
  NeedsActionResult,
} from './needs-action.models';

/** One waiting row; override what the case needs. */
export function actionRow(overrides: Partial<NeedsActionRow> = {}): NeedsActionRow {
  return {
    id: 'record-1',
    parentId: null,
    label: 'PO-0012, Visayas Steel',
    projectId: 'project-1',
    projectName: 'Riverside Residences',
    detail: 'Waiting 3 days',
    sinceUtc: '2026-10-30T02:00:00Z',
    dueDate: null,
    isUrgent: false,
    amountMinor: 125_000_000,
    ...overrides,
  };
}

export function actionGroup(
  key: NeedsActionGroupKey,
  rows: readonly NeedsActionRow[],
  count = rows.length,
): NeedsActionGroup {
  return { key, count, rows };
}

/** What an administrator might see: one of every group, the urgent ones first as the server orders them. */
export function everyGroup(): NeedsActionResult {
  return {
    groups: [
      actionGroup('bills-to-chase', [
        actionRow({
          id: 'bill-3',
          label: 'BILL-0003, ACME Realty',
          detail: '5 days overdue, due 28 Oct 2026',
          dueDate: '2026-10-28',
          isUrgent: true,
          amountMinor: 250_000_00,
        }),
      ]),
      actionGroup('bonds-to-attend', [
        actionRow({
          id: 'bond-1',
          label: 'Performance bond PB-14, Metro Surety',
          detail: 'Expired 3 days ago, on 30 Oct 2026',
          dueDate: '2026-10-30',
          isUrgent: true,
          amountMinor: 5_000_000_00,
        }),
      ]),
      actionGroup('purchase-orders-to-approve', [actionRow({ id: 'po-12' })], 12),
      actionGroup('change-orders-to-approve', [
        actionRow({ id: 'co-2', label: 'CO-002 Extra footing', amountMinor: -15_000_00 }),
      ]),
      actionGroup('bills-to-issue', [
        actionRow({
          id: 'bill-4',
          label: 'Progress bill for ACME Realty',
          amountMinor: 900_000_00,
        }),
      ]),
      actionGroup('claims-to-certify', [
        actionRow({
          id: 'claim-1',
          parentId: 'subcontract-4',
          label: 'Claim on SC-0004, Masonry Crew',
          amountMinor: 112_000_00,
        }),
      ]),
      actionGroup('payroll-to-approve', [
        noAmount({
          id: 'run-1',
          label: 'Payroll for 16 Oct - 31 Oct 2026',
          detail: '12 workers, waiting 1 day',
          projectId: null,
          projectName: null,
        }),
      ]),
      actionGroup(
        'deliveries-expected',
        [noAmount({ id: 'po-7', label: 'PO-0007, Cebu Hardware', detail: 'Due today' })],
        11,
      ),
      actionGroup('progress-not-updated', [
        noAmount({
          id: 'project-1',
          label: 'Riverside Residences',
          detail: 'Progress not updated for 20 days',
        }),
      ]),
      actionGroup('attendance-not-recorded', [
        noAmount({
          id: 'project-2',
          label: 'Harbor Warehouse',
          projectId: 'project-2',
          projectName: 'Harbor Warehouse',
          detail: 'No attendance recorded for Sun 1 Nov',
        }),
      ]),
      actionGroup('statutory-needs-setup', [
        noAmount({
          id: '00000000-0000-0000-0000-000000000000',
          label: 'Contribution and tax tables',
          projectId: null,
          projectName: null,
          detail: "No table in force for SSS, so payroll can't work them out.",
        }),
      ]),
    ],
  };
}

/** A Warehouse Keeper's queue: deliveries only, and the server leaves the amount property out of every row. */
export function warehouseKeeperResult(): NeedsActionResult {
  return {
    groups: [
      actionGroup('deliveries-expected', [
        noAmount({
          id: 'po-7',
          label: 'PO-0007, Cebu Hardware',
          detail: '2 days late, expected 31 Oct 2026',
          dueDate: '2026-10-31',
          isUrgent: true,
        }),
        noAmount({ id: 'po-8', label: 'PO-0008, Visayas Steel', detail: 'Due today' }),
      ]),
    ],
  };
}

/** A row as the server sends it for a group without amounts: the property is absent, not null. */
function noAmount(overrides: Partial<NeedsActionRow>): NeedsActionRow {
  const { amountMinor, ...row } = actionRow(overrides);
  void amountMinor;
  return row;
}
