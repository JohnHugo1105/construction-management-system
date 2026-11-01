# Business rules

This is the authoritative statement of how the Construction Management System behaves. When a rule changes, change the code, this
page and a test together. Section 9 lists what was wrong in the retired desktop application and what replaced it; section 10 lists
the decisions that rest on assumptions and should be confirmed by the business owner.

Amounts are in the company's currency (Philippine peso by default) and are stored in **minor units** (centavos) as whole numbers.
Dates that belong to the business (an invoice date, a work day) are calendar dates in the company's time zone
(`Asia/Manila` by default), never the UTC day.

## 1. Principles that hold everywhere

| Rule | What it means |
| --- | --- |
| Exact money | Money is a whole number of centavos. It is never a floating-point number and never text. A quantity times a unit cost is rounded once, half away from zero, to a whole centavo. |
| Net + VAT = gross | On every line, `net + VAT = gross` to the centavo, so totals of any set of lines add up exactly. |
| Nothing is deleted | Financial and history records are **voided or reversed with a reason**, never deleted. Master data (projects, workers, items) is **archived or retired**, never deleted. Voided and removed records stay visible and never count in totals. |
| Every change is traceable | State changes write a line to the append-only activity log (who, when, what). It never contains passwords or pay amounts. |
| One company per record | Every record belongs to one company. A person can never read or change another company's records, even by guessing an id (they receive "not found"). |
| No silent overwrites | Editing needs the version the person opened. If someone else saved first, the edit is refused and the person is asked to reload. |
| One answer in one place | A total shown on the dashboard, the project page and an export comes from the same calculation. |
| All problems at once | A form that is wrong reports every wrong field in one response. |
| Repeated clicks are safe | Creating something twice from a double-click returns the first result; it never makes a duplicate. |

## 2. Access

People hold one or more roles. **A person with no role can do nothing** (the retired app treated an empty selection as full access).
Pay amounts are visible only to those who can run payroll (section 6).

| Permission | Administrator | Project Manager | Cost Engineer | Accountant | Warehouse Keeper | HR Officer | Viewer |
| --- | :-: | :-: | :-: | :-: | :-: | :-: | :-: |
| See projects | yes | yes | yes | yes | yes | yes | yes |
| Create and change projects | yes | yes | | | | | |
| See budget and costs, reports, the contract price | yes | yes | yes | yes | | | yes |
| Enter budget and costs | yes | | yes | yes | | | |
| See workers (not their pay) | yes | yes | | yes | | yes | yes |
| Hire, change pay, assign workers | yes | | | | | yes | |
| Record attendance | yes | yes | | | | yes | |
| See payroll and pay rates | yes | | | yes | | yes | |
| Prepare payroll | yes | | | yes | | yes | |
| Approve or void approved payroll | yes | | | yes | | | |
| See stock | yes | yes | yes | | yes | | yes |
| Move and count stock | yes | | | | yes | | |
| Manage users and company settings | yes | | | | | | |
| Read the activity log | yes | | | yes | | | |

Sign-in: five wrong passwords lock the account for 15 minutes; an unknown username, a wrong password and a locked account all get the
same message, so the sign-in page can't be used to find out which accounts exist.
Passwords need 10 or more characters with a letter and a number, and are stored only as a salted slow hash. An administrator sets a
temporary password and the person must choose their own before doing anything else. Turning an account off ends its session within
seconds. **A session can be ended from the server:** changing a password (yours, or a reset by an administrator), turning an account off,
and **signing out** all end the person's sessions on every device, so a copied cookie stops working at once (within 15 seconds if the
server is not the one that handled the change). There must always be at least one active administrator, and you can't remove your own
administrator role or turn off your own account. Changes that could remove the last administrator run one at a time, so two administrators can't remove each other in the same instant
(that guarantee holds per server process; with several servers, check the user list after changing administrators).

## 3. Projects

* A project has a unique name within the company (case and spacing ignored), an optional client, location, contract price (including
  VAT), start date and target end date. Sub-projects have a unique name within their project. **The contract price is shown only to
  people who can see costs**: a warehouse keeper or HR officer opens the project without seeing what the company will be paid.
* Lifecycle: **Planning → Active ⇄ On hold → Completed → Active (reopen)**. Other moves are refused with an explanation.
* **Archive** needs a reason: "Finished project", or "Other" with a note. An archived project and its sub-projects are read-only (no
  new budget lines, costs, postings, attendance or stock moves) but stay fully readable. **Restore** returns it to the status it had,
  and brings back only the sub-projects that were archived *with* it: one archived on its own earlier stays archived.
  Nothing moves or is deleted when archiving; the retired app copied rows to archive tables and could lose actual costs.
* New budget lines, costs, assignments, attendance and stock moves may be recorded only against a project, and a sub-project, that is
  not archived.

## 4. Budget and actual cost

### 4.1 Lines
A **budget line** is a planned cost; a **cost entry** is money actually spent. Both carry: project, optional sub-project,
classification, description, item type (Material, Labor, Equipment, Subcontract, Other), quantity (up to 4 decimals, more than
zero), unit, unit cost, VAT treatment, date and remarks. A cost entry also carries a **supplier** and **invoice or receipt number**,
both required (a cost nobody can trace is not recorded); system-made entries (payroll, charged stock transfers) have no invoice.

### 4.2 Classification (work breakdown)
Lines are classified by division, main scope, sub-scope and work-breakdown code, using the standard 16-division catalogue (CSI
MasterFormat 1995) the retired app generated in code. A standard code decides its own division and names; giving a different division
is an error that names the right one. A **custom** code is allowed and must state its division and sub-scope. A line may be left
unclassified (reports group it under "Unclassified"). The old app kept these as loose text.

### 4.3 Units
Units are free text with suggestions, folded to one spelling so reports group correctly: `pcs → pc`, `bags → bag`, `sets → set`,
`sht → sheet`, `m3 → m³`, `m2 → m²`, `lump sum → l.s`, and similar.

### 4.4 Money on a line
Let *base* = quantity × unit cost, rounded to a whole centavo.

| VAT treatment | net | VAT | gross |
| --- | --- | --- | --- |
| Unit cost **includes** VAT | round(base × 10000 ÷ (10000 + rate)) | base − net | base |
| Unit cost is **before** VAT | base | round(base × rate ÷ 10000) | base + VAT |
| **No** VAT | base | 0 | base |

The rate is the company's VAT rate (12% by default, set in company settings) **at the moment of entry**, and the line keeps it:
changing the company rate later never alters recorded lines.

**Limits.** Every amount is bounded so no total can overflow: a line (with VAT), a unit cost, a contract price and a deduction can each
be at most ₱100,000,000,000.00, and a pay rate at most ₱10,000,000.00 a day or an hour. Anything larger is refused with a message.

### 4.5 Duplicates and dates
* A supplier invoice can be recorded **once per project**: the same supplier and invoice number (ignoring case and punctuation)
  is refused while the first entry is posted. It can be entered again after the first is voided.
* Supplier names are matched after trimming spaces and ignoring case only (punctuation in a *supplier* name still counts); only the
  *invoice number* ignores punctuation and spacing. Enter the same supplier the same way each time.
* A cost can't be dated in the future (judged in the company time zone).
* A repeated create with the same request id returns the original, including when two copies of the same submission arrive at the
  very same moment. A request id reused for something else (another project, worker or day) is refused instead.

### 4.6 Changing and removing
Manual cost entries and budget lines can be edited (the audit trail records it). **Editing keeps the line's own VAT rate**; only new
lines use the company's current rate, so a change of company rate never alters history. Removing or voiding on an archived project is
refused, like every other change. A budget line is **removed** with a reason, a cost
entry **voided** with a reason; neither is deleted and neither counts afterwards. **System-made cost entries (payroll and stock
transfers) can be neither edited nor voided at the entry**: they are reversed where they came from (void the payroll run, reverse the
transfer), so the source and the project never disagree. That reversal is itself refused while a project it would change is archived
(restore it, reverse, archive again), including a payroll void, a charged stock reversal and removing attendance on an archived project.

### 4.7 Budget against actual
For any slice (a project, sub-project, division, code, item type):

* **Remaining = budget − actual** (negative means over budget). **Used = actual ÷ budget**, shown as a percentage.
* Health: **On track** under 90% used; **Near limit** from 90% up to and including 100%; **Over budget** above 100%;
  **Unbudgeted** when money was spent where nothing was budgeted; **No activity** when both are zero. The comparison uses exact
  amounts, so 89.999% is still on track.
* The comparison uses amounts **including VAT** by default, or **excluding VAT** when the company recovers the VAT it pays (a
  company setting). Both are available per line.
* Removed budget lines and voided cost entries never count.
* **Contract price** (including VAT) minus actual cost (including VAT) is the margin so far; contract price minus budget (including
  VAT) is the planned margin. They are shown only when a contract price exists.

## 5. Workforce

* A worker has an employee number (unique in the company), names, hire date and personal details. Statuses: **Active, On leave,
  Suspended, Resigned, Retired, Terminated**. Suspending or terminating needs a reason. A worker who has left can't be changed except
  by **reinstating** (with a reason); the retired app let any status replace any other and could not record a dismissal.
* **Pay history is append-only.** A rate has a unit (per day or per hour), a designation and an effective date. A change is a
  promotion, a demotion or a rate adjustment: a promotion or demotion changes the designation; a rate adjustment changes only the pay.
  A change must start after the latest one and not before the hire date, so earlier pay is never rewritten. **It also can't start on or
  before a day a payroll run (draft or approved) has already paid**: that pay was worked out at the old rate, and the history would
  disagree with it. Void the run first, or start the new rate after the last paid day. "What was this worker's rate on 3 November?"
  always has one answer. The retired app let the rate be typed over in a grid with no record.
* **Postings** (assignments) never overlap: assigning a worker ends the previous posting on the new start date, and a worker who was
  released or left can't be posted again from a day before the old posting ended. Only an active worker on a project that can receive
  records can be posted. A bulk assignment is all or nothing and names every worker that blocked it.
* **Status history** is kept: from which day a worker was on leave, suspended, back at work or gone. It decides which days attendance
  may be recorded for (section 6.1) long after the status changed again.
* **Cases** (incidents or disciplinary notes) are added and never edited or deleted. A person's profile details and cases are visible
  to everyone who can see workers; restrict that further if your policy needs it.
* **Pay is confidential.** Rates and pay amounts reach only people who can run payroll; project managers and viewers see who works
  where but not what they earn. Pay amounts never appear in the activity log or in logs.

## 6. Attendance and payroll

The retired app had no attendance and no payroll; wages never reached a project's cost.

### 6.1 Attendance
Attendance is the hours one worker put in on one project on one day: regular hours (up to the company's paid hours per day, 8 by
default) and overtime hours. A worker's hours across all projects on a date may not exceed 24. One entry per worker, project,
sub-project and date (enforced by the database). The 24-hour check is made when an entry is saved: two entries for the same worker
and date saved at the very same instant on different projects could both pass it, and payroll review would show the excess hours.
A worker can be recorded from the hire date on days they were **active**; one who left only up to the last day they worked; days of
leave or suspension stay closed **even after the worker is back** (they are not paid time), because the status history is kept. Attendance
can't be in the future. Removing an entry is refused on an archived project and for attendance a payroll run has taken.

### 6.2 Pay calculation
Each attendance entry is paid at the rate **in force on its work date**, so a raise part-way through a period applies from its date.

* A daily rate is spread over the company's paid hours per day; an hourly rate is used as is.
* **Regular pay** = rate × regular hours (÷ paid hours per day for a daily rate). **Overtime pay** = the same hourly pay × overtime
  hours × the overtime multiple (125% by default). The division happens **once, last**, so no rounded hourly figure is ever multiplied
  (600.25 a day over 7.5 hours for 0.75 hours is 60.025, which is 60.03, not 60.02).
* Each figure is rounded once per entry; a worker's pay and each project's share are sums of those rounded figures, so project shares
  always add up to the worker's pay exactly.

Not computed, on purpose: holiday and rest-day premiums, night differential, and statutory contributions and taxes (SSS, PhilHealth,
Pag-IBIG, withholding tax). They change by law and company policy. Premium hours are entered as overtime hours; contributions and
advances are entered by a person as **deductions**.

### 6.3 Payroll runs
1. **Draft:** for a pay period (at most 31 days counting both ends, so 1 to 31 October but not 1 October to 1 November; not ending in
   the future) the system takes all recorded attendance not yet paid,
   calculates every worker's pay, and **locks** that attendance. Locked attendance can't be edited or removed; the same attendance can
   never be in two runs, so nobody is paid twice.
2. **Deductions** are entered per worker while the run is a draft. Deductions can't exceed pay.
3. **Approve** (accountants and administrators): the run is approved and the labor cost is **posted to each project**: one cost entry
   per project and sub-project for the period, equal to the gross pay of the attendance on it, with no VAT. The posted entries add up
   to the run's gross pay exactly. Two simultaneous approvals produce one approval.
4. **Void** (with a reason): attendance is unlocked and the posted cost entries are voided. A draft is voided by whoever prepares it;
   an approved run only by an accountant or administrator, and only while none of the projects it charged is archived.

## 7. Inventory (tools, equipment, machinery)

* **Tools** are counted by quantity; **equipment and machinery** are single pieces with a unique serial number and a quantity of 1.
* Stock sits in the **warehouse** or on a **project**. The system keeps the balance per place and an **append-only ledger** of every
  movement: purchase, transfer, stock-count adjustment, write-off, reversal. Balances are changed only together with the ledger line.
* A move can never take more than the source holds (so stock can't go negative), and two people moving the last units at once can't
  both succeed. History is **never edited**; a mistake is undone by a **reversal** (not twice, not of a reversal, and only while the
  stock is still where the original put it). The retired app let the history grid be edited in place so stock and history could
  disagree.
* A transfer may be **charged to the destination project** at unit cost × quantity (an explicit choice, off by default). The charge
  posts a cost entry to the project in the same save and is voided when the transfer is reversed. Moving stock back to the warehouse
  is never charged.
* Writing off a single piece retires it; a reversal restores it. An item can be retired only when it holds no stock. A retired item
  accepts no movements.
* Moves can't be dated before the purchase or in the future.
* **Archived projects:** stock can't be moved *onto* an archived project. Moving stock *off* one, counting it and writing it off stay
  allowed, so a finished site can be emptied by a warehouse keeper who can't restore the project.
* A single serial-numbered piece can't be counted into a second place while it is recorded in another; set the other place to 0 first.

## 8. Reports and exports

* **Activity log:** routine successful sign-ins are left out unless "Show sign-ins" is on, because they are the most frequent entries.
  Failed sign-ins are always shown.
* **Dashboard:** every project's budget, actual, remaining, percentage used, health and margin, with company totals, from one grouped
  query per measure (the number of database statements does not grow with the number of projects).
* **Project cost summary:** the project's position broken down by division (with its codes), sub-project and item type; unbudgeted
  and unclassified money is shown, worst overrun first.
* **CSV exports** (budget lines, cost entries, cost summary, payroll register): UTF-8 with a byte-order mark so Excel opens them
  correctly, plain decimal amounts, ISO dates, and cells that start with `=`, `+`, `-` or `@` are neutralised so a pasted formula can't
  run in a spreadsheet.

## 9. Corrections from the retired desktop application

Found by reading its source and database model. File references are to the last commit that contained it (`93da838`).

| # | Retired behaviour | Why it was wrong | Now |
| --- | --- | --- | --- |
| 1 | Money was stored as comma-formatted text and summed as `double`; a null threw and an empty string crashed | Rounding drift; wrong totals; crashes | Exact whole-centavo integers; one rounding rule; totals computed in SQL |
| 2 | Unit cost was displayed rounded to 2 decimals but the total used the unrounded value | Stored unit cost and total disagreed (1000 × 0.125 stored 125.00 and "0.13") | One rounding step; unit costs are whole centavos |
| 3 | VAT was `total / 1.12` hard-coded, VAT flag not saved, no rounding | Net + VAT could differ from the total by a centavo; "no VAT" indistinguishable from zero VAT | Configurable rate kept per line; explicit treatment; net + VAT = gross always |
| 4 | Dates stored as text (`MMM. d, yyy`) with a second text column for sorting | Culture-dependent, unsortable, wrong comparisons | Real dates; business dates in the company time zone |
| 5 | Totals were summed by project **name**; names copied into every row next to the ids | Same-named projects merged; a rename orphaned lines | Records point at ids; names are looked up |
| 6 | Dynamic SQL built from text in many places (and a stored procedure that executed it) | SQL injection; restore ran arbitrary SQL from a file | Parameterized queries only; no in-app restore (see operations docs) |
| 7 | Passwords stored and compared as plain text; capped at 15 characters | Anyone with the database had every password | Salted PBKDF2 hashes; length-based policy; lockout; constant-time check |
| 8 | The app opened with no sign-in; "admin control" asked for no password; anyone at the login screen could create an administrator | No access control | Sign-in required; roles; only administrators manage users |
| 9 | Empty or mismatched access codes enabled every module | Failing open | No role means no access |
| 10 | "Archive" copied budget rows to archive tables and deleted the originals; journal rows were not archived; no restore | Data loss and orphans | Archive is a status with a reason; nothing moves; restore exists |
| 11 | Delete of a project had empty branches; sub-project could be created unlinked from its project | Orphans, broken hierarchy | No delete; sub-projects belong to a project |
| 12 | Save validation was advisory: the form listed empty fields and let the person save anyway | Incomplete records | Required fields enforced; every problem reported at once |
| 13 | The same supplier invoice could be entered repeatedly | Double-counted cost | One posted invoice per supplier and project |
| 14 | Variance and remaining budget had no formula in code (only in unreadable report files); an old join matched budget to actual by typed description | No reliable comparison | Defined and tested: remaining, percentage used, health; by classification |
| 15 | "Project price" was a free-text box, never saved, and a static that leaked between projects | No margin possible | Saved contract price per project; margin shown |
| 16 | Worker rate and designation could be typed over in a grid; promotion/demotion had no checks; no termination status | Silent, unaudited pay changes | Append-only effective-dated pay history with category rules; all statuses constrained |
| 17 | Worker delete cascaded by first and last **name**; history rows copied the worker's name and could go stale | Workers with the same name lost each other's history | Records link by id; history is never deleted |
| 18 | Warehouse stock was counted by typed description; the "Warehouse" place was a typed project name; the transfer history was editable | Typos created new stores; stock and history could diverge | Items have ids and balances per place; append-only ledger; reversals |
| 19 | No check that a transfer didn't exceed stock; negative and fractional quantities accepted | Impossible stock | Never more than held; tools counted, machines single |
| 20 | Search filters mixed `OR` and `AND` without brackets and escaped only the apostrophe | Wrong results; wildcard characters misbehaved | Server-side search with literal text matching |
| 21 | Paging loaded the whole table and cut it client-side | Slow and unbounded | Server-side paging, at most 100 per page |
| 22 | Connection to a database named after the user's PC; commented-out connection strings with credentials; a signing key and a database file committed | Leaked secrets; unportable | Environment-supplied configuration; nothing secret committed (rotate anything the old history contained) |
| 23 | Reports were Crystal Reports binaries; the dashboard form was debug code showing a message box per row | No working reports | Dashboard and cost summary from SQL; CSV export |
| 24 | Backup exported one table as a script and restore dropped the table first, inside no transaction | Easy data loss | Whole-database verified backups and tested restores (`docs/operations/backup-and-restore.md`) |
| 25 | No payroll, no attendance, no audit trail, no concurrency control, no health checks | Gaps | Added (sections 1, 6) |

## 10. Decisions that rest on assumptions (confirm with the business owner)

1. **Payroll scope.** The retired app had no payroll, so the calculation (section 6) is new. It covers regular and overtime pay only.
   Confirm the overtime multiple (125%), the paid hours per day (8), whether holiday, rest-day and night premiums are needed, and who
   enters statutory contributions.
2. **Pay unit.** The old rate was free text with no unit. Rates are now per day or per hour; construction crews are normally paid daily.
3. **VAT basis for comparison.** Budget against actual defaults to amounts **including VAT** (as the old totals were). Switch to
   excluding VAT in company settings if VAT paid is recovered.
4. **Contract price** is treated as **including VAT** (the old report label said "Total project price (Vat)").
5. **Charging stock to a project.** The old app posted a charge when the person chose "with amount". Here it is the same explicit
   choice, off by default, never applied to a return to the warehouse. Confirm whether tools should be charged at full cost on transfer
   or by rental.
6. **Health thresholds** (near limit at 90%) are conventional; confirm or change `BudgetPosition.NearLimitBps`.
7. **Worker project** is the main project with an optional sub-project; the old HR screen knew only the main project.
8. **Retention.** Nothing is purged. A retention policy for pay and personal data needs an explicit decision before any purge job is
   added.
9. **Pay can be inferred from cost.** A payroll cost entry for a sub-project that only one worker was posted to equals that worker's gross
   pay, and anyone who can see costs can see it (attendance shows the hours). Pay is confidential in the worker and payroll screens, but
   labor cost is part of project cost. If that matters, post workers to the whole project rather than to one-person sub-projects, or
   decide that labor cost should be hidden from some cost roles. Confirm.
10. **Away days and recorded attendance.** Marking a worker on leave or suspended from a past day closes those days for new attendance but
    does not remove attendance already recorded for them: HR removes it (with a reason) if it should not be paid.
11. **Safe limits.** The largest amounts (section 4.4) are generous for a construction company; change `MoneyLimits` if the business needs more.
