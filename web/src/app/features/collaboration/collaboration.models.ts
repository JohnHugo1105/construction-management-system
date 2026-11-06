/**
 * What the collaboration endpoints send and take (see docs/api/collaboration.md): tasks, the discussion on a record, the signed-in
 * person's notifications and the colleague lookup. Nothing here carries an amount: a task or a comment points at the record that does.
 */

/** The records people coordinate on. */
export type RecordKind =
  'Project' | 'Task' | 'PurchaseOrder' | 'ChangeOrder' | 'ClientBill' | 'Subcontract' | 'Bond';

/** The records that carry a discussion (every kind but a bond). */
export type DiscussionKind = Exclude<RecordKind, 'Bond'>;

export type TaskStatus = 'Open' | 'Done' | 'Cancelled';

/** Whose tasks a list shows: assigned to me, created by me, or (Administrators only) every task. */
export type TaskScope = 'assigned' | 'created' | 'all';

/** A colleague as the pickers offer them: an id and a name, never a role, email or username. */
export interface Colleague {
  readonly id: string;
  readonly displayName: string;
}

export interface TaskListItem {
  readonly id: string;
  readonly title: string;
  readonly status: TaskStatus;
  readonly dueDate: string | null;
  readonly isOverdue: boolean;
  /** The due date in words while Open ("2 days overdue, due 30 Oct 2026", "Due today", "No due date"); null once done or cancelled. */
  readonly dueDetail: string | null;
  readonly assigneeUserId: string;
  readonly assigneeName: string | null;
  readonly createdByUserId: string;
  readonly createdByName: string | null;
  readonly projectId: string | null;
  readonly projectName: string | null;
  readonly createdAtUtc: string;
  readonly version: number;
}

export interface TaskListParams {
  readonly scope: TaskScope;
  readonly status: '' | TaskStatus;
  readonly projectId: string;
  readonly page: number;
  readonly pageSize: number;
}

/** What the signed-in person may do with the task now (the server decides). */
export interface TaskActions {
  readonly complete: boolean;
  readonly cancel: boolean;
  readonly reassign: boolean;
  readonly reopen: boolean;
}

export interface WorkTask {
  readonly id: string;
  readonly title: string;
  readonly description: string | null;
  readonly status: TaskStatus;
  readonly dueDate: string | null;
  readonly isOverdue: boolean;
  readonly dueDetail: string | null;
  readonly assigneeUserId: string;
  readonly assigneeName: string | null;
  readonly createdByUserId: string;
  readonly createdByName: string | null;
  readonly projectId: string | null;
  readonly projectName: string | null;
  readonly projectArchived: boolean;
  /** The kind of record the task is about, or null. */
  readonly linkKind: RecordKind | null;
  readonly linkId: string | null;
  /** That record in words ("purchase order PO-0012"), or null. */
  readonly linkLabel: string | null;
  readonly createdAtUtc: string;
  readonly completedAtUtc: string | null;
  readonly completedByName: string | null;
  readonly cancelledAtUtc: string | null;
  readonly cancelledByName: string | null;
  readonly cancelReason: string | null;
  readonly version: number;
  readonly actions: TaskActions;
}

export interface NewTaskInput {
  readonly title: string;
  readonly description: string | null;
  readonly dueDate: string | null;
  readonly assigneeUserId: string;
  readonly projectId: string | null;
}

export interface DiscussionComment {
  readonly id: string;
  readonly authorUserId: string;
  readonly authorName: string | null;
  /** The words; null once the comment was removed. */
  readonly text: string | null;
  readonly mentions: readonly Colleague[];
  readonly createdAtUtc: string;
  readonly isVoided: boolean;
  /** Who removed it ("Removed by ..."), or null. */
  readonly voidedByName: string | null;
  readonly voidedAtUtc: string | null;
  /** The signed-in person wrote it or is an Administrator, and the discussion is not read-only. */
  readonly canRemove: boolean;
  readonly version: number;
}

/** A page of a record's discussion, newest first, and whether the signed-in person may add to it. */
export interface CommentPage {
  readonly items: readonly DiscussionComment[];
  readonly total: number;
  readonly page: number;
  readonly pageSize: number;
  readonly canWrite: boolean;
  /** Why nobody may write now (an archived project), or null. */
  readonly readOnlyReason: string | null;
}

export interface NewCommentInput {
  readonly kind: DiscussionKind;
  readonly targetId: string;
  readonly text: string;
  readonly mentionUserIds: readonly string[];
}

export type NotificationKind = 'TaskAssigned' | 'TaskDone' | 'Mention' | 'DraftDecided';

/** One thing that happened that the signed-in person should know about. Its message is already worded by the server. */
export interface AppNotification {
  readonly id: string;
  readonly kind: NotificationKind;
  readonly message: string;
  readonly targetKind: RecordKind;
  readonly targetId: string;
  readonly projectId: string | null;
  readonly createdAtUtc: string;
  readonly isRead: boolean;
}

/** The bell's number. */
export interface NotificationCount {
  readonly unread: number;
}

/** A project the task form and the filter offer (`GET /api/projects/lookup`). */
export interface ProjectChoice {
  readonly id: string;
  readonly name: string;
  readonly status: string;
}
