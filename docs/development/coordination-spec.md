# Coordination: the contract for the four parallel slices

ConstructOPX holds the record and the money truth, but until now the people around a project coordinated in a chat group. This closes the
gaps found when asking how staff keep track of the work: no way to assign work to a person, to discuss a record on the record, to be told
something changed, to write the day's site report, to keep drawings and permits, or to plan the work over time. "Needs your action"
(pull: what is waiting on me) is built separately; this adds the rest.

| Slice | Closes the gap | In one line |
| --- | --- | --- |
| **T Collaboration** | no task assignment, no messaging, no notifications | Tasks assigned to a person, comments with mentions on the records people already work on, and in-app notifications |
| **D Site diary** | no site diary or daily report | One structured report per project per day, with attendance and deliveries filled in from what is already recorded |
| **F Documents** | no document storage for drawings, contracts or permits | Project documents with versions, expiry dates, safe downloads, and a capability check for file storage |
| **S Schedule** | no real schedule | Planned activities with dates linked to budget lines, a baseline, a schedule-based planned value and an S-curve |

Read first: `AGENTS.md`, `docs/development/philippine-practice-spec.md` section 1 (ground rules, environment, report format), and
`docs/development/commercial-control-spec.md` sections 1, 3 and 4 (conventions: money, records, tenant, audit, idempotency, voiding, paging,
statement counts). **They apply to every slice here unchanged.** Rules marked **Default** are choices the owner has not confirmed; each
slice lists them in `docs/development/notes/<slice>-rules.md` for the lead to merge.

## 1. What is deliberately not built (state it in the docs and on the screens where it matters)

* **Email, SMS or push delivery of notifications.** In-app only. The capability screen says "Email delivery: not set up". (An outbox with
  retries is the right design when a provider is chosen; it is not built now.)
* **Task dependencies, a critical path, resource levelling.** The schedule is activities with dates, not a network.
* **Virus scanning of uploads.** Files are stored, served only as downloads, and marked "not scanned"; the capability screen says so.
* **Photos inside the diary.** Photos are documents (category Photo) on the project; the diary links to them.
* **Free chat.** Comments live on a record. There are no direct messages and no channels.
* **Editing or deleting a comment.** A comment can be removed (voided with a reason), never edited.

## 2. Ownership ledger

| Slice | Owns |
| --- | --- |
| **T** | `ConstructOPX.Domain/Features/Collaboration`, `.Application/Features/Collaboration`, `.Infrastructure/Features/Collaboration`, `Endpoints/TaskEndpoints.cs`, `CommentEndpoints.cs`, `NotificationEndpoints.cs`, `UserLookupEndpoints.cs`, `web/src/app/features/collaboration` (the tasks pages, notifications page, the reusable `app-discussion` component, the bell state). **Also** one-line hooks in `PurchaseOrderService`, `ChangeOrderService`, `ClientInvoiceService` (bill issued) and `SubcontractService` (claim certified) that tell the drafter their draft was decided (`INotificationWriter`), the embed of `app-discussion` (one element each) in the purchase order page, the change order page, the bill page, the subcontract page and the project Overview tab, and the bell in the shell header. |
| **D** | `.../Features/SiteDiary` (Domain, Application, Infrastructure), `Endpoints/SiteDiaryEndpoints.cs`, `web/src/app/features/site-diary`. |
| **F** | `.../Features/Documents` (Domain, Application, Infrastructure including the file store adapter), `.../Features/Capabilities` (Application and Infrastructure: the capability registry and checks), `Endpoints/DocumentEndpoints.cs`, `Endpoints/CapabilityEndpoints.cs`, `web/src/app/features/documents`, the Admin "System readiness" page in `web/src/app/features/admin`, `deploy/docker-compose.yml` (a files volume), `docs/operations/*` for file storage and backup. |
| **S** | `.../Features/Schedule` (Domain, Application, Infrastructure), `Endpoints/ScheduleEndpoints.cs`, `web/src/app/features/schedule`. **Also**, for the schedule-based planned value only: `ProgressReportBuilder.cs`, `ProgressService.cs`, `ProgressContracts.cs` (one optional field) and the Progress tab in `web/src/app/features/progress` (the "Schedule" view and the S-curve section). |
| **Lead** | `Permissions.cs` and the web `permissions.ts` (already extended in the foundation), `app.routes.ts`, `navigation.ts`, `project-detail-page.ts` tab list, `projects.routes.ts`, the shell files other than the bell, `DevelopmentSeeder.cs` and the demo files, `docs/business-rules.md`, `README.md`, migrations, `CmsDbContext` equivalents, `DependencyInjection.cs`, and the "Needs your action" integration (section 8). |

## 3. Permissions (defined by the lead in the foundation; do not edit `Permissions.cs`)

| Permission | Roles | Used for |
| --- | --- | --- |
| `tasks.use` | Administrator, Project Manager, Cost Engineer, Accountant, HR Officer, Warehouse Keeper | Create, be assigned, complete and discuss tasks; look up colleagues' names |
| `notifications.read` | every role | Read and clear your own notifications |
| `diary.read` | Administrator, Project Manager, Cost Engineer, Accountant, Warehouse Keeper, Viewer | Read daily reports (no money appears in a report) |
| `diary.write` | Administrator, Project Manager, Cost Engineer | Write and correct daily reports |
| `documents.read` | Administrator, Project Manager, Cost Engineer, Accountant, Warehouse Keeper, Viewer | Read the document list and download; **categories Contract and Insurance also need `costs.read`** (they reveal prices) so a Warehouse Keeper never sees them |
| `documents.manage` | Administrator, Project Manager, Cost Engineer, Accountant | Add, replace and void documents |
| `schedule.read` | Administrator, Project Manager, Cost Engineer, Accountant, Viewer (within `costs.read`; a test pins it) | Read the schedule and S-curve (it is derived from budget amounts) |
| `schedule.write` | Administrator, Project Manager, Cost Engineer | Plan activities, set the baseline |
| `settings.manage` (existing) | Administrator | The System readiness page |

## 4. The capability model (slice F builds it; slice T and D read it only where stated)

One registry, `ICapabilityCheck`, one implementation per capability, returning `Available`, `NeedsSetup`, `Unavailable` or `Failing`, a
plain-words message and the fallback. `GET /api/capabilities` (`settings.manage`) lists them for the readiness page. Entries now: **File
storage** (Available when a writable root path is configured, NeedsSetup when none, Failing when it cannot be written; checked with a
short-lived cached probe), **Virus scanning** (always NeedsSetup: "Files are stored and offered only as downloads; they are not scanned"),
**Email delivery** (always NeedsSetup: "Notifications appear in the app; email is not set up"). A feature never hides behind a flag: when
file storage is not Available, uploading is disabled with the reason and **"Add a link instead"** still works, so no work is lost.

## 5. Slice T: collaboration

**Tasks** (`WorkTask`, aggregate): title (up to 120), description (up to 1,000), `Open`/`Done`/`Cancelled`, due date (`DateOnly`, not before today when
created), assignee (an active user of the company who holds `tasks.use`; **assigning to yourself is allowed**), creator, optional project
(not archived), optional link to one record (`Project`, `PurchaseOrder`, `ChangeOrder`, `ClientBill`, `Subcontract`, `Bond`: the id and a
kind), completed at and by, `Version`, `ClientRequestId`. Visible to the creator, the assignee and Administrators only. The assignee, the
creator or an Administrator completes it; the creator or an Administrator cancels, reassigns (a notification goes to the new assignee) or reopens
it. Overdue is derived (due date before today while Open). Reads: `GET /api/tasks?scope=assigned|created|all&status&page` (`all` is
Administrator only), one task with its comments. A task never carries or reveals an amount.

**Comments** (`Comment`, append-only): target kind and id (`Project`, `Task`, `PurchaseOrder`, `ChangeOrder`, `ClientBill`, `Subcontract`),
the project id (copied, for the archived-project check and the tenant filter), text (up to 1,000), the mentioned users (a child table), who and
when. Removing one voids it with a reason (the author or an Administrator); a voided comment shows "Removed by X". **Who may read or write a
comment is the target's own rule**: reading needs the permission that reads the target (`projects.read`, `purchasing.read`,
`changeorders.read`, `billing.read`, `subcontracts.read`; a task: its participants and Administrators); writing needs `tasks.use` as well
(Viewers read only). A mention is refused ("Pia can't see this record") when the mentioned person could not read the target. A comment on an archived
project is refused. Reads: `GET /api/comments?kind&targetId&page`, `POST /api/comments`, `POST /api/comments/{id}/void`.

**Notifications** (`Notification`): user, kind (`TaskAssigned`, `TaskDone`, `Mention`, `DraftDecided`), a plain message (up to 200: names and
document numbers only; **never an amount, a rate or a pay figure**), target kind and id, project id, created, read. Written in the same save as the
action that causes them (`INotificationWriter`), never to the person who did the action. Triggers: a task assigned to you, a task you created marked done,
a mention, and your draft decided (purchase order approved or cancelled, change order approved or rejected, bill issued, subcontract claim certified).
Reads and writes, all for **your own** notifications only (a request for another user's id is a 404): `GET /api/notifications?unread&page`,
`GET /api/notifications/count`, `POST /api/notifications/{id}/read`, `POST /api/notifications/read-all`.

**Lookup**: `GET /api/users/lookup?search&targetKind&targetId` (`tasks.use`) returns `{ id, displayName }` of active colleagues only
(never roles, email or usernames), and when a target is given only those who could read it (for the mention picker).

**UI.** A bell with the unread count in the shell header (hidden at 0, "99+" over 99; refreshed on load, when you open the page and when the
tab becomes visible; no timers), a Notifications page (newest first, unread marked with words and weight, tapping goes to the record and
marks it read, "Mark all as read"), a Tasks page (`/tasks`: Assigned to me, Created by me, an "Add task" primary action; a task page with its
discussion), and the reusable **Discussion** section (comments, a composer with "@" mention picker) placed on the pages listed in the ledger.
The nav gets a "Tasks" entry for `tasks.use` holders.

## 6. Slice D: site diary

`SiteDiaryEntry` (aggregate): project, report date (`DateOnly`, company time zone, **not in the future**), weather (`Fair`, `Cloudy`, `Rain`,
`HeavyRain`, `Storm`) with an optional note, work done (up to 2,000), issues and delays (up to 1,000), visitors and inspections (up to 500),
safety notes (up to 1,000), subcontractor workers on site (0 to 2,000, a typed number: those crews are not on our payroll), `Posted`/`Void`,
author, `Version`, `ClientRequestId`. **One live entry per project per date** (a filtered unique index). Created and corrected with
`diary.write`: the author may correct it until the end of the next day (company time zone); a Project Manager or Administrator may correct
it any time (every correction is audited, with the field names changed and no free text in the audit summary); a void needs a reason (Project Manager
or Administrator). An archived project's diary is read-only.

**Filled in, not typed**: the read of an entry returns, read-only, the **workers with attendance that day on the project** (a count and the
names are not pay: names only for roles that already see the worker list; others get the count), the **deliveries received that day** on the project's orders
(quantities and the supplier name only, never an amount: reuse the receiving read model rules), and the **documents added that day**
(titles) once slice F exists (an optional port; nothing when absent). Reads: `GET /api/projects/{id}/diary?page` (newest first),
`GET /api/projects/{id}/diary/{date}`, `POST /api/projects/{id}/diary`, `PUT .../diary/{date}`, `POST .../diary/{date}/void`. Fixed statement counts.
**UI**: a project **Diary** tab (a list by date with weather, headcount and the first line of "Work done"; "Write today's report" as the one
primary action when there is none; a form with weather chips and the read-only "On site today" and "Delivered today" panels).

## 7. Slice F: documents and capabilities

`ProjectDocument` (aggregate): project, title (up to 120), description (up to 300), category (`Contract`, `Drawing`, `Permit`, `Insurance`,
`Certificate`, `Photo`, `Report`, `Other`), valid until (optional; for permits, insurance and certificates; **expiring soon at 30 days**, expired
after the date, derived, never stored, the same rule as bonds: reuse its code if it is shared, otherwise mirror it), `Active`/`Void`, current version
number, `Version`, `ClientRequestId`; **`DocumentVersion`** (child, append-only): version number, kind `File` or `Link`, for a file the original file
name (up to 200), content type, size in bytes, SHA-256, a store key (never returned to a client); for a link an `https://` URL (up to 500; other
schemes refused), a note (up to 300), who and when. A new version never replaces the old: older versions stay downloadable.

**File store** (`IFileStore` in Application; the adapter in Infrastructure writes under a configured root `FileStorage:RootPath`; keys are
`<tenantId>/<projectId>/<guid>` so no file name ever reaches a path). Limits (configurable, defaults): **25 MB** per file; allowed extensions pdf, jpg, jpeg, png, webp,
doc, docx, xls, xlsx, ppt, pptx, csv, txt, dwg, dxf; **refused** html, svg, js, exe and anything else; for pdf and the image types the first bytes
must match the type. The size cap is enforced while streaming; the SHA-256 is computed while streaming; a failed save leaves no partial file and no row.
**Downloads** go through an authenticated route only: `Content-Disposition: attachment`, `X-Content-Type-Options: nosniff`, `Cache-Control: private,
no-store`, the original file name sanitized; authorization is `documents.read` plus the category rule (Contract and Insurance also need `costs.read`) plus the
company filter; another company's id is a 404. Routes: `GET /api/projects/{id}/documents?category&page`, `GET /api/documents/{id}`, `POST
/api/projects/{id}/documents` (multipart or a link), `POST /api/documents/{id}/versions`, `GET /api/documents/{id}/versions/{n}/file`, `PUT
/api/documents/{id}` (details and valid until), `POST /api/documents/{id}/void`; uploads need `documents.manage`. Audit entries carry the
title and category, never the file name or note. The capability check (section 4) decides whether an upload is offered; a Link is always
offered. **UI**: a project **Documents** tab (grouped by category, each row: title, category, version, a valid-until chip with words, who and when,
Download, Add a new version, Void), "Add document" as the one primary action (a disabled upload with the reason line and a working "Add a link
instead" when storage is not Available), and the Admin **System readiness** page. Ops docs: the files volume, the backup of files with the
database, and the configuration name.

## 8. Slice S: schedule

`ScheduleActivity` (aggregate, per project): name (up to 120), planned start and finish (`DateOnly`, finish not before start), the linked
budget lines (a child table; **at least one link to count toward the plan**: an activity with none is listed and flagged "Link budget lines to
include it in the plan"), baseline start and finish (null until the baseline is set), sort order, `Version`. **Weight** = the sum of the
linked lines' budget on the company's comparison basis (the same basis the Progress tab uses); a line may belong to more than one activity
only if you split it: **Default: a budget line belongs to at most one activity** (refuse a second link with the activity's name).
**Planned value at date d** = for each activity, weight times the share of its planned days elapsed by d (linear within the activity,
clamped to 0 and 1, both ends counted as days), summed; **baseline planned value** uses the baseline dates. An activity's percent complete is
the weighted average of its linked lines' current progress (read-only, from the existing progress data). An activity is **behind** when its
planned finish is before today and it is below 100%. **Set baseline** (`schedule.write`, a confirmation, repeatable with an audit entry) copies the planned
dates to the baseline; after it, the timeline shows the baseline and the slip in days.

**Where the numbers land**: when a project has at least one activity with weight, the Progress tab's planned value, schedule index and
"behind schedule" line use the **schedule-based planned value** (the existing straight-line value over the project's start and end dates stays
for projects with no schedule, and its tests keep passing). Cost-based forecasts (the cost index method) do not use planned value and do not change.
`GET /api/projects/{id}/schedule` (activities with weight, percent complete, behind flag, slip), `POST/PUT .../schedule/activities`, `POST
.../schedule/activities/{id}/remove` (with a reason: it is planning data, not a financial record, but the removal is audited), `POST
.../schedule/baseline`, and `GET /api/projects/{id}/schedule/s-curve`: weekly points from the project's first activity start to its last
finish (at most 120 points) with `plannedMinor`, `baselinePlannedMinor?`, `earnedMinor` (to today only) and `actualMinor` (posted cost, to today only). **A test proves
the S-curve's planned value at today equals the Progress tab's planned value** (the anti-disagreement assertion), and that the points are a fixed
number of statements however many activities and entries exist. **UI**: on the Progress tab a view toggle "Update progress / Schedule": the
Schedule view has a timeline (a bar per activity on a date axis with a today line and the percent filled; a plain list on phones), the activity form
with a budget line picker grouped by division, "Set baseline", and an **S-curve** chart drawn in SVG (planned, baseline, earned, actual; no library; colors never the
only signal). Money is shown only to roles that already see the Progress tab.

## 9. "Needs your action" integration (the lead, after the page lands)

Each slice adds one group, **built by that slice** through the page's group-source extension point (the lead defines it from what the page builder
delivered, and puts it in the foundation): `tasks-assigned` (T; `tasks.use`; Open tasks assigned to you, overdue first; no amounts),
`diary-not-written` (D; `diary.write`; active projects whose previous business day has no report while one exists in the previous 7 days),
`documents-expiring` (F; `documents.manage`; active documents expired or expiring within 30 days), `activities-behind` (S; `schedule.write`; one row per project
with the number of activities past their planned finish). The notification bell is separate from the Needs-action badge: one says what is
waiting on you, the other says what happened.

## 10. After the slices (lead)

One regenerated migration; demo data (two tasks and comments with a mention, notifications, a week of diary entries, a few documents including a permit
expiring soon and a link, a schedule with a baseline for Riverside); business-rules sections for each slice and the access table;
README rows; the project tab list (Diary, Documents; Schedule lives on the Progress tab); full .NET and Angular runs; an independent
development-lead review and security reviews (uploads, mentions and the comment permission map are the sharp edges); fixes in one batch; PR update.
