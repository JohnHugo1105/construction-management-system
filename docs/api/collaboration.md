# Collaboration API

Tasks given to a person, the discussion on the records people already work on (with mentions), the signed-in person's in-app notifications,
and the colleague lookup behind the pickers. **Nothing here carries an amount**: a task or a comment points at the record that does, and a
notification is built from names and document numbers only. Notifications are shown in the app only: no email, text message or push is
sent (email delivery is not set up). There are no direct messages or channels: every comment sits on a record.

All routes are under `/api`, return JSON in camelCase and send `Cache-Control: private, no-store`. Dates are `"2026-11-02"`; instants are
UTC. Writes need the anti-forgery header, like every other write. Errors are RFC 9457 problem details: `422 validation_failed` with field
errors, `409 rule_violation` (a business rule, with a plain message), `409 concurrency_conflict` (a stale `version`: reload), `404
not_found`, `403 forbidden`.

## Endpoints

| Method and route | Permission |
| --- | --- |
| `GET /api/tasks?scope&status&projectId&page&pageSize` | `tasks.use` |
| `GET /api/tasks/{id}` | `tasks.use` |
| `POST /api/tasks` | `tasks.use` |
| `POST /api/tasks/{id}/complete` | `tasks.use` |
| `POST /api/tasks/{id}/cancel` | `tasks.use` |
| `POST /api/tasks/{id}/reopen` | `tasks.use` |
| `POST /api/tasks/{id}/reassign` | `tasks.use` |
| `GET /api/comments?kind&targetId&page&pageSize` | `projects.read` at the door, then the record's own read rule |
| `POST /api/comments` | `tasks.use`, plus the record's own read rule |
| `POST /api/comments/{id}/void` | `tasks.use`, plus the record's own read rule |
| `GET /api/notifications?unread&page&pageSize` | `notifications.read` |
| `GET /api/notifications/count` | `notifications.read` |
| `POST /api/notifications/{id}/read` | `notifications.read` |
| `POST /api/notifications/read-all` | `notifications.read` |
| `GET /api/users/lookup?search&targetKind&targetId` | `tasks.use` |

Roles: `tasks.use` is held by Administrator, Project Manager, Cost Engineer, Accountant, HR Officer and Warehouse Keeper (a Viewer reads
discussions and has notifications, and does nothing else here). `notifications.read` is every role.

## Tasks

A task (`WorkTask`) has a `title` (up to 120 characters), an optional `description` (up to 1,000), a `status` (`Open`, `Done`,
`Cancelled`), an optional `dueDate`, the person it is for (`assigneeUserId`), who created it, an optional project, and an optional link to
one record (`linkKind` `Project`, `PurchaseOrder`, `ChangeOrder`, `ClientBill`, `Subcontract` or `Bond`, with `linkId`).

**Who sees it.** The person who created it, the person it is for, and Administrators. Anyone else gets `404`, even with the id.

**Reads.**

* `GET /api/tasks` returns a page of `{ id, title, status, dueDate, isOverdue, dueDetail, assigneeUserId, assigneeName, createdByUserId,
  createdByName, projectId, projectName, createdAtUtc, version }`. `scope` is `assigned` (the default: tasks for me), `created` (tasks I
  created) or `all` (every task; Administrators only, anyone else `403`). `status` filters by state; `projectId` keeps one project's tasks.
  Order: Open first, then the overdue and soonest due, those without a date last, then the most recently changed.
* `isOverdue` is worked out when read and never stored: Open with a due date before today in the company's time zone. `dueDetail` is the
  due date in words while the task is Open, the same words the "Needs your action" page uses ("2 days overdue, due 31 Oct 2026", "Due
  today", "Due tomorrow", "Due 5 Nov 2026", "No due date"); it is `null` once the task is done or cancelled.
* `GET /api/tasks/{id}` adds `description`, `projectArchived`, `linkKind`, `linkId`, `linkLabel` (the record in words, "purchase order
  PO-0012"), `completedAtUtc`, `completedByName`, `cancelledAtUtc`, `cancelledByName`, `cancelReason` and `actions` `{ complete, cancel,
  reassign, reopen }`: what the caller may do now.

**Writes.** Each answers with the whole task.

* `POST /api/tasks` takes `title`, `description`, `dueDate` (today or later), `assigneeUserId`, `projectId`, `linkKind`, `linkId` and an
  optional `clientRequestId`. The person it is for must be an active colleague who holds `tasks.use`; giving a task to yourself is allowed.
  With a link, the creator must be able to read the record, the person it is for must be able to read it too, and the task takes the
  record's project (a different `projectId` is refused). A task on an archived project is refused. A repeated `clientRequestId` from the same
  person answers with the first task (`201`); from someone else it is refused (`409`). Answers `201` with `Location`.
* `complete` takes `{ version }`: the assignee, the creator or an Administrator, while Open.
* `cancel` takes `{ reason, version }` (the reason is optional, up to 300): the creator or an Administrator, while Open.
* `reopen` takes `{ version }`: the creator or an Administrator, once Done or Cancelled. The due date stays, so a past one makes it overdue.
* `reassign` takes `{ assigneeUserId, version }`: the creator or an Administrator, while Open, to someone else who could take it.
* On an archived project every step is refused (`409`). A stale `version` is `409 concurrency_conflict`.

## The discussion on a record

A comment sits on one record: `kind` `Project`, `Task`, `PurchaseOrder`, `ChangeOrder`, `ClientBill` or `Subcontract` (a bond has no
discussion), with `targetId`. **Who may read it is the record's own rule:** a project needs `projects.read`, a purchase order
`purchasing.read`, a change order `changeorders.read`, a bill `billing.read`, a subcontract `subcontracts.read`, and a task is read by its
creator, the person it is for and Administrators. Someone without the record's permission gets `403` before anything is looked up, so they
cannot learn whether a comment exists; a task they take no part in, or a record of another company, is `404`. Writing and removing also need
`tasks.use`.

* `GET /api/comments?kind&targetId` returns `{ items, total, page, pageSize, canWrite, readOnlyReason }`, newest first. Each item is `{ id,
  authorUserId, authorName, text, mentions: [{ id, displayName }], createdAtUtc, isVoided, voidedByName, voidedAtUtc, canRemove, version
  }`. A removed comment has `text: null` and no mentions. `canWrite` is false for someone without `tasks.use` and on an archived project,
  where `readOnlyReason` says why.
* `POST /api/comments` takes `{ kind, targetId, text, mentionUserIds, clientRequestId }`. `text` is required, up to 1,000 characters. At
  most 10 people can be mentioned; each must be an active colleague of the same company who could read the record, or the comment is
  refused with a field error on `mentionUserIds` ("Pia Santos can't see this record, so they can't be mentioned here."). A comment on an
  archived project is refused (`409`). A repeated `clientRequestId` from the same person on the same record answers with the first comment.
  Answers `201`.
* `POST /api/comments/{id}/void` takes `{ reason, version }` (required, up to 300). Only its author or an Administrator removes a comment;
  it is never edited or deleted, and the discussion shows "Removed by ..." in its place.

## Notifications

A notification is `{ id, kind, message, targetKind, targetId, projectId, createdAtUtc, isRead }`. `kind` is `TaskAssigned` (a task given or
reassigned to you), `TaskDone` (a task you created marked done by someone else), `Mention` (you were mentioned in a comment) or
`DraftDecided` (a purchase order you drafted approved or cancelled, a change order approved or rejected, a bill issued, a subcontract claim
certified). The `message` is up to 200 characters: a name, what they did and the record by its kind and document number ("Ana Reyes approved
purchase order PO-0012."), never an amount, a rate, a pay figure or words someone typed. It is written in the same save as the action that
causes it, and never to the person who did the action.

Every read and write is for **your own** notifications only: another person's notification id is `404`.

* `GET /api/notifications?unread=true` returns a page, newest first; without `unread`, all of them.
* `GET /api/notifications/count` returns `{ unread }` for the bell.
* `POST /api/notifications/{id}/read` marks one read (again changes nothing) and answers `{ unread }`.
* `POST /api/notifications/read-all` marks every unread one read in one statement and answers `{ unread: 0 }`.

## Colleague lookup

`GET /api/users/lookup?search` returns at most 20 `{ id, displayName }` of active colleagues of the same company, by name; never a role,
email or username. Without a record, only the people who hold `tasks.use` (the task assignee picker). With `targetKind` and `targetId`, only
the people who could read that record (the "@" mention picker); the caller must be able to read it too. `search` is up to 120 characters.

## The "Needs your action" group

`tasks-assigned` (for `tasks.use`): the caller's own Open tasks, not on an archived project, overdue first (urgent), then the soonest due,
then those without a date. A row is `{ id, label: the title, projectId, projectName, detail: dueDetail, dueDate, isUrgent }` and never
carries an amount. The web opens `/tasks/{id}`.

## Statement counts

Every read is a fixed number of SQL statements however many rows exist (asserted in the API tests): task list 3 (the company's today, count,
page; the same with a project filter), one task 2, a discussion page 4 (the record, count, page, the page's mentions), notification list 2,
unread count 1. The `tasks-assigned` group is 1 statement when empty and 2 otherwise.
