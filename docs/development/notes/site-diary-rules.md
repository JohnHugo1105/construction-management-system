# Site diary: rules for `docs/business-rules.md`

Written by the site diary slice (D). The lead merges this into `docs/business-rules.md` (a new section, the access table, and section 16
"Needs your action") and deletes this note. Plain, exact, with the figures; **Default** marks a choice the owner has not confirmed.

## The site diary

A **site report** is the day's record of one project: one structured report per project per day, replacing the chat group where site
engineers used to post "what we did today". **A report carries no money.** It also cannot reveal any: the workers with attendance that day and
the deliveries received that day are read next to it, and neither read selects a pay figure, an hours figure, a rate, a price or an amount.
A test walks the name of every property of every response and fails on a money word.

### 1. What a report records

| Field | Rule |
| --- | --- |
| Project | Required. The project must not be archived. Fixed once written. |
| Day (`reportDate`) | Required. A business date on the company's calendar (`Asia/Manila` by default), **never in the future**, never before 1 Jan 2000. Fixed once written: a report written for the wrong day is voided and written again. A report may be written for any earlier day (a late report is still better than none). |
| Weather | Required: Fair, Cloudy, Rain, Heavy rain or Storm. |
| Weather note | Optional, up to 200 characters. |
| Work done | **Required** (Default: a report with nothing in it is not a report; on a day with no work, say why), up to 2,000 characters. |
| Issues and delays | Optional, up to 1,000 characters. |
| Visitors and inspections | Optional, up to 500 characters. |
| Safety notes | Optional, up to 1,000 characters. |
| Subcontractor workers | A typed whole number, 0 to 2,000: those crews are not on the company's payroll, so they cannot be read from attendance. |
| Author | The signed-in person who wrote it. Fixed. |
| State | Posted, or Void. |

Text is trimmed; optional text left empty is not stored. Every problem with what was typed comes back at once.

### 2. One live report per day

A project has **at most one live (not voided) report for a day**, enforced by a unique index filtered to reports that are not void, so two
people writing the same day at the same moment cannot both succeed: the second is told, in plain words, that the day already has a report and
how to open it. A voided report frees its day. A repeated create with the same request id returns the first report; a request id reused for a
different project or day is refused.

### 3. Who may write, correct and void

* **Reading** needs `diary.read` (Administrator, Project Manager, Cost Engineer, Accountant, Warehouse Keeper, Viewer). **Writing, correcting
  and voiding** need `diary.write` (Administrator, Project Manager, Cost Engineer). The HR Officer has no access.
* **The author window.** The author of a report may correct or void it until **the end of the next day after the report date** on the company's
  calendar (a report for Monday can be changed by its author until midnight at the end of Tuesday, Manila time; the day changes at midnight in the
  company's zone, not in UTC). **Default: the window runs from the report's day, not from when it was typed**, so a late report for an earlier day
  may already be closed to its author when it is written.
* **A person who manages projects** (holds `projects.manage`: Project Manager, Administrator) may correct or void any report at any time.
* Anyone else who writes reports (a Cost Engineer who is not the author) is refused with a message that says who can.
* A **void** needs a reason (up to 300 characters) and the same who-and-when. The report stays on record with the reason, who and when, leaves the
  project's list and frees its day.
* An **archived** project's diary is read-only: no new report, correction or void ("Restore it first"). It can still be read.
* A stale version is refused (the person is asked to reload); another company's project or report is "not found".

### 4. Corrections and the audit line

Saving a correction that changes nothing records nothing and does not change the version. A correction that changes something bumps the version and
writes one audit line that names **the fields that changed** ("changed weather, work done") and **never the text a person typed**. A void's reason
stays on the report, not in the audit line.

### 5. Filled in, not typed

The read of a report (and `GET .../diary/{date}/filled-in`, for a day with no report yet, used by the form) returns:

* **On site**: the workers with attendance recorded that day on this project (a worker counts once; a voided attendance entry does not count; another
  project's or another day's attendance does not). **Every reader gets the count. The names (sorted by last name, at most 300) come only for a reader
  who holds `workforce.read`** (Administrator, Project Manager, HR Officer, Accountant, Viewer), so a Warehouse Keeper or a Cost Engineer sees the
  headcount and never a worker's name. Hours, rates and pay are never read.
* **Delivered**: what posted deliveries received that day on this project's purchase orders, grouped by supplier, item and unit with the quantity
  summed: the supplier name, the item as ordered, its unit and the quantity, and nothing else (the same columns as the receiving screen; a voided
  delivery does not count). It carries no unit cost, VAT or total.
* The panel for documents added that day is **not built** (it needs the documents slice); the port is not added, so there is nothing to fall back
  from.

### 6. Needs your action: "Site diary not written yesterday"

Key `diary-not-written`, for people who hold `diary.write` (Administrator, Project Manager, Cost Engineer). One row per **Active** project whose
report for **yesterday** (on the company's calendar) is missing while **a Posted report exists for the project within the seven days before
yesterday**, so a project that has never kept a diary is never nagged. **Default: yesterday is judged simply; a Sunday is not skipped** (a
project that never works Sundays will be listed on Mondays, until the company's holiday list says so); **nothing is listed when yesterday is a
company holiday.** A voided report does not count as written, nor as proof the project keeps a diary. The row is the project's name, with the
detail "No report for Sun 1 Nov"; it never carries an amount. The page shows the group's true count and at most eight rows.

### 7. Reads and statement counts

The project's list (newest first, paged) is 3 SQL statements (the project, the count, the page, with each day's headcount counted inside the
page statement); one report is 6 (the project, the company's calendar day, the report, the workers' count, their names, the deliveries; 5 for a
reader who may not see the names); the filled-in read is 4, however many reports, workers or deliveries exist. The needs-action group is its
count plus its rows when the count is above zero.

## Decisions that rest on assumptions

1. A report's **work done is required**.
2. The author window is anchored on **the report's day** (the end of the next day), not on when it was written.
3. A report may be written for **any earlier day**, not only for today and yesterday.
4. **Void** is open to the author inside the window and to Project Managers and Administrators, not to every writer.
5. **A Sunday is not skipped** by the "not written yesterday" rule; only a company holiday is.
6. The lookback that shows a project keeps a diary is **seven days before yesterday**.
7. A voided report is **not listed** anywhere in the product; it stays in the database and the audit trail.
8. The worker names are shown to anyone who holds `workforce.read`, and only to them.
