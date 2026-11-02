# Collaboration rules (slice T) — for the lead to merge into `docs/business-rules.md`

Proposed as a new section "Tasks, discussion and notifications", plus three rows for the access table in section 2. Rules marked
**Default** are choices the owner has not confirmed (listed again at the end). The API is in `docs/api/collaboration.md`.

## Access rows (section 2)

| Permission | Administrator | Project Manager | Cost Engineer | Accountant | Warehouse Keeper | HR Officer | Viewer |
| --- | :-: | :-: | :-: | :-: | :-: | :-: | :-: |
| Give, take, complete and discuss tasks; write comments; look up colleagues' names | yes | yes | yes | yes | yes | yes | |
| See every task in the company | yes | | | | | | |
| See your own notifications | yes | yes | yes | yes | yes | yes | yes |

Reading a record's discussion follows the record: whoever may see the record sees its discussion (a Viewer reads, and does not write).

## Tasks

* A task is a piece of work one person gives another, or themselves: a title (up to 120 characters), an optional description (up to
  1,000), an optional due date, optionally a project and optionally one record it is about (a project, a purchase order, a change order, a
  bill, a subcontract or a bond). **A task never carries an amount**: it points at the record that does.
* The person it is for must be an active colleague who can work on tasks. **Giving a task to yourself is allowed.** A task about a record
  can only be given to someone who can see that record, and only by someone who can see it; it takes the record's project.
* The due date cannot be in the past when the task is created (the company's calendar day, Asia/Manila unless configured). **Overdue** is
  worked out, never stored: an Open task whose due date is before today. A reopened task keeps its due date, so it may be overdue at once.
* **Only the person who created it, the person it is for and Administrators see a task.** Anyone else is told it does not exist, even
  with its link.
* The person it is for, the person who created it or an Administrator marks it done. The person who created it or an Administrator
  cancels it (an optional reason stays on it), reassigns it to someone else, or reopens a done or cancelled task.
* On an archived project a task can be read but not changed.
* Each step checks the version the person opened; if someone changed the task meanwhile, the step is refused and the person reloads. A
  repeated "Save task" (double tap, retry) makes one task.

## The discussion on a record

* Comments sit on a project, a task, a purchase order, a change order, a bill or a subcontract. There are no direct messages, channels or
  free chat. A bond has no discussion.
* **Who may read a record's discussion is the record's own rule**: a project's needs "See projects", a purchase order's, change order's,
  subcontract's or bill's needs "See purchase orders, progress, change orders, subcontracts and bills", and a task's is for the people in
  the task and Administrators. So a Warehouse Keeper or an HR Officer never reads the discussion on a purchase order, a bill, a change order
  or a subcontract (those records carry the company's amounts). Writing also needs the task permission.
* A comment is up to 1,000 characters. **It is never edited or deleted**: its author or an Administrator removes it with a reason (up to
  300 characters); the words stay in the database and the discussion shows "Removed by <name>, <when>" in their place.
* A comment can mention up to 10 people. **Someone can be mentioned only if they are an active colleague of the same company who could
  see the record** ("Pia Santos can't see this record, so they can't be mentioned here."). Each person mentioned is told in the app.
* An archived project's discussion, and the discussion on its records, is read-only and says so.
* A repeated "Post comment" makes one comment.

## Notifications

* A notification tells one person, in the app only, that something happened: a task was given (or reassigned) to them, a task they created
  was marked done, they were mentioned, or a draft they prepared was decided (a purchase order approved or cancelled, a change order approved
  or rejected, a bill issued, a subcontract claim certified).
* **Nobody is told about what they did themselves** (marking your own task done, approving your own draft, mentioning yourself).
* The message is at most 200 characters and is built only from a person's name, what they did and the record's kind and number ("Ana Reyes
  approved purchase order PO-0012."). **It never carries an amount, a rate, a pay figure or words someone typed** (not a task title, not a
  comment). A long name is cut with "…" rather than refused, because the action already happened.
* A notification is written in the same save as the action that causes it, so the two exist together or not at all.
* A person sees, counts and marks read **only their own** notifications. Marking one read twice changes nothing; "Mark all as read" marks
  them all at once.
* **Email, text message and push delivery are not built.** The capability check reports "Email delivery: not set up".
* The bell in the header shows the unread count: nothing at 0, "99+" above 99. It is read when the app opens, when the Notifications page
  opens and when the browser tab comes back to the front; there is no timer.

## "Needs your action"

* Group `tasks-assigned` ("Tasks assigned to you"), for people who can work on tasks: your own Open tasks, not on an archived project,
  overdue first (marked urgent), then the soonest due, then those with no due date. Each row shows the title, the project and the due date
  in words; never an amount. The bell says what happened; "Needs your action" says what waits on you.

## Defaults chosen (confirm with the owner)

1. **Default:** Administrators see and may change every task (spec: "Visible to the creator, the assignee and Administrators").
2. **Default:** a reopened task keeps its old due date even when it is now in the past.
3. **Default:** cancelling a task does not notify the person it was for; reopening does not notify anyone. Only the four triggers above notify.
4. **Default:** at most 10 people per comment; a comment's mentions are hidden once it is removed.
5. **Default:** the mention picker and the assignee picker show at most 20 names per search; the signed-in person is not offered in the
   mention picker (mentioning yourself tells nobody).
6. **Default:** the Tasks page opens on "Assigned to me", Open tasks; "Created by me", the state and the project are one tap away, and
   Administrators also get "Every task". The task list can be filtered by project (`projectId`), an addition to the spec's list filters.
7. **Default:** the discussion shows the newest 20 comments first, with "Show older comments" for the rest.
8. **Default:** a project's discussion is open to everyone who can see the project (all roles), including Warehouse Keepers and HR
   Officers. People are trusted not to type amounts into a comment on a project; amounts in the record itself stay behind its own permission.
