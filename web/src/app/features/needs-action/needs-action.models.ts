/**
 * What `GET /api/needs-action` returns (see docs/api/needs-action.md). The server decides which groups a person receives from their
 * own permissions, words every row, and says which rows are urgent; the page only maps a group and its ids to the record's page.
 */

export type NeedsActionGroupKey =
  | 'purchase-orders-to-approve'
  | 'change-orders-to-approve'
  | 'bills-to-issue'
  | 'claims-to-certify'
  | 'payroll-to-approve'
  | 'bills-to-chase'
  | 'deliveries-expected'
  | 'bonds-to-attend'
  | 'progress-not-updated'
  | 'attendance-not-recorded'
  | 'statutory-needs-setup'
  | 'tasks-assigned'
  | 'diary-not-written'
  | 'documents-expiring'
  | 'activities-behind'
  | 'time-clock-to-review';

export interface NeedsActionRow {
  /** The record the row opens (a project for progress and attendance rows; empty for the statutory tables). */
  readonly id: string;
  /** The subcontract of a claim: a claim is certified on its subcontract's page. */
  readonly parentId: string | null;
  /** The document number and who it concerns, already worded: "PO-0012, Visayas Steel". */
  readonly label: string;
  readonly projectId: string | null;
  readonly projectName: string | null;
  /** Why it is waiting, already worded: "Waiting 3 days", "5 days overdue, due 28 Oct 2026". */
  readonly detail: string;
  readonly sinceUtc: string | null;
  readonly dueDate: string | null;
  /** Overdue, late or expired. */
  readonly isUrgent: boolean;
  /** Present only for groups that show an amount; left out entirely otherwise (payroll, deliveries, progress, attendance, tables). */
  readonly amountMinor?: number;
}

export interface NeedsActionGroup {
  readonly key: NeedsActionGroupKey;
  /** How many records wait in all; `rows` holds at most the eight most urgent. */
  readonly count: number;
  readonly rows: readonly NeedsActionRow[];
}

export interface NeedsActionResult {
  /** The person's groups, urgent groups first. A group they can act on is present even when it holds nothing. */
  readonly groups: readonly NeedsActionGroup[];
}

/** What `GET /api/needs-action/count` returns: the badge's number. */
export interface NeedsActionCount {
  readonly total: number;
}
