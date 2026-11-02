import { Permission } from '../../core/session/permissions';
import {
  AppNotification,
  CommentPage,
  DiscussionComment,
  TaskListItem,
  WorkTask,
} from './collaboration.models';

/** A Project Manager's permissions as far as collaboration goes (the signed-in user is `user-1`, Ana Reyes). */
export const WORKER = [
  Permission.projectsRead,
  Permission.purchasingRead,
  Permission.tasksUse,
  Permission.notificationsRead,
];

/** A Viewer: reads projects and their discussion, has notifications, works on no tasks. */
export const READER = [Permission.projectsRead, Permission.notificationsRead];

export function taskItem(overrides: Partial<TaskListItem> = {}): TaskListItem {
  return {
    id: 't1',
    title: 'Check the rebar quantities',
    status: 'Open',
    dueDate: '2026-11-04',
    isOverdue: false,
    dueDetail: 'Due 4 Nov 2026',
    assigneeUserId: 'user-1',
    assigneeName: 'Ana Reyes',
    createdByUserId: 'user-2',
    createdByName: 'Pia Santos',
    projectId: 'p1',
    projectName: 'Riverside Residences',
    createdAtUtc: '2026-11-01T02:00:00Z',
    version: 1,
    ...overrides,
  };
}

export function task(overrides: Partial<WorkTask> = {}): WorkTask {
  return {
    id: 't1',
    title: 'Check the rebar quantities',
    description: 'Compare the bars on the purchase orders with revision C.',
    status: 'Open',
    dueDate: '2026-11-04',
    isOverdue: false,
    dueDetail: 'Due 4 Nov 2026',
    assigneeUserId: 'user-1',
    assigneeName: 'Ana Reyes',
    createdByUserId: 'user-2',
    createdByName: 'Pia Santos',
    projectId: 'p1',
    projectName: 'Riverside Residences',
    projectArchived: false,
    linkKind: 'PurchaseOrder',
    linkId: 'po1',
    linkLabel: 'purchase order PO-0012',
    createdAtUtc: '2026-11-01T02:00:00Z',
    completedAtUtc: null,
    completedByName: null,
    cancelledAtUtc: null,
    cancelledByName: null,
    cancelReason: null,
    version: 1,
    actions: { complete: true, cancel: false, reassign: false, reopen: false },
    ...overrides,
  };
}

export function comment(overrides: Partial<DiscussionComment> = {}): DiscussionComment {
  return {
    id: 'c1',
    authorUserId: 'user-2',
    authorName: 'Pia Santos',
    text: 'Can the pour move to Monday?',
    mentions: [],
    createdAtUtc: '2026-11-02T02:15:00Z',
    isVoided: false,
    voidedByName: null,
    voidedAtUtc: null,
    canRemove: false,
    version: 0,
    ...overrides,
  };
}

export function commentPage(
  items: readonly DiscussionComment[],
  overrides: Partial<CommentPage> = {},
): CommentPage {
  return {
    items,
    total: items.length,
    page: 1,
    pageSize: 20,
    canWrite: true,
    readOnlyReason: null,
    ...overrides,
  };
}

export function notification(overrides: Partial<AppNotification> = {}): AppNotification {
  return {
    id: 'n1',
    kind: 'TaskAssigned',
    message: 'Pia Santos assigned you a task on Riverside Residences.',
    targetKind: 'Task',
    targetId: 't1',
    projectId: 'p1',
    createdAtUtc: '2026-11-02T02:15:00Z',
    isRead: false,
    ...overrides,
  };
}
