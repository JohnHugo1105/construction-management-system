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
| The system never pays or collects | Recording a payment (to a subcontractor, from a client) is a person saying it happened. Nothing is sent, charged, confirmed or collected automatically, and no automatic effect spends or confirms money. |
| What is not ready says so | A feature that depends on setup (the statutory tables) says in plain words what is missing and who can fix it, and falls back to the manual way without losing anyone's work. |

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
| See firms (suppliers and subcontractors) | yes | yes | yes | yes | yes | | yes |
| Add and change firms | yes | | yes | yes | | | |
| See purchase orders, progress, change orders, subcontracts and bills | yes | yes | yes | yes | | | yes |
| Raise and change purchase orders | yes | | yes | yes | | | |
| Approve, cancel or close purchase orders | yes | yes | | yes | | | |
| Record progress | yes | yes | yes | | | | |
| Draft and submit change orders; draft subcontracts and claims | yes | yes | yes | | | | |
| Approve, reject or void change orders; lock the budget | yes | yes | | | | | |
| Draft bills, set billing terms, record client payments | yes | yes | | yes | | | |
| Issue or void bills and payments; release retention | yes | | | yes | | | |
| Award, certify, pay and complete subcontracts | yes | | | yes | | | |
| See the statutory tables | yes | | | yes | | yes | |
| Enter the statutory tables | yes | | | yes | | | |

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

### 4.8 The budget baseline

* Once the budget is agreed, a project manager or administrator **locks** it. The sums of the active budget lines (before VAT and with
  VAT) and the contract price at that moment are recorded as the **original**. Locking needs at least one active budget line, and an
  archived project can't be locked or unlocked.
* While the budget is locked, budget lines can't be added, changed or removed directly ("The budget is locked. Record a change order
  to change it."), and the contract price can't be changed on the project. A project locked before it had a contract price may still
  record its first price, which becomes the original price.
* The **revised** budget and price are the original plus the approved change orders. The active budget lines always add up to the
  revised budget exactly, to the centavo; the baseline page checks it every time it is shown.
* The budget can be **unlocked** (with a reason, kept on the project) only while no change order is approved. Unlocking clears the
  original; locking again records the budget as it is then.

### 4.9 Change orders

* A change order belongs to one project and has a title, a reason (client request, design change, site condition, error or omission,
  other), an optional description, an optional change to the contract price (with VAT, up or down; the price must exist, and stays
  between zero and ₱100,000,000,000.00), and up to 100 lines. Change orders can be recorded only on a locked budget.
* A line **adds** a budget line (with every field a budget line has, checked by the same rules, at the company VAT rate when the line is
  entered), **revises** an active budget line (a new quantity and unit cost; its description, VAT treatment and rate stay), or **omits**
  an active budget line. A budget line is changed at most once per change order.
* Each line's effect on the budget is worked out by the shared line rules: an addition adds its amount, a revision adds the new amount
  minus the old, an omission subtracts the line. The order's effect is the sum of its lines, before VAT, VAT and with VAT.
* **Workflow.** Draft (editable) → **Submitted** (numbered CO-001, CO-002 ... per project, at submission; a submission that is refused
  uses no number) → **Approved** or **Rejected** (with a reason). A draft or submitted order can be **discarded** with a reason. Nothing
  is deleted.
* **Approving** applies everything in one save: added lines become budget lines marked with the change order; revised lines take the
  new quantity and unit cost, and the old ones are kept on the change order; omitted lines are removed with the reason "Change order
  CO-001"; the contract price moves by the change. Every target must still be active, or approval is refused naming the line.
* **Voiding** an approved change order (with a reason) reverses it exactly in one save: added lines are removed, revised lines get
  their old quantity and unit cost back (and therefore their old amounts, to the centavo), omitted lines are reinstated, and the price
  moves back. Change orders are voided **newest first**: a void is refused while a change order approved after it still stands, and
  the message names it. A void is refused on an archived project.
* Drafting, submitting and discarding are for project managers, cost engineers and administrators; locking, unlocking, approving,
  rejecting and voiding are for project managers and administrators. Accountants and viewers read. The activity log records each
  step without the reasons (they stay on the record).
* The dashboard, the project cost summary and the Budget tab need no change of their own: they add up the active budget lines, so an
  approval moves them by exactly the approved effect and a void returns them.

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

### 6.4 Contribution and tax tables

* SSS, PhilHealth and Pag-IBIG contributions and withholding tax are worked out from **tables the company enters from the official
  schedules**. Rates and brackets change by law, so none is built into the system; until a table is entered, the agency's amounts are
  entered by hand as deductions, as before, and payroll says so in one quiet line ("SSS contributions were not calculated because no SSS
  table is set up. Enter them as deductions, or ask an accountant to set the table up.").
* A table has an agency, a name, the date it takes effect, a status (active or retired), a basis (**per month** or **per pay run**) and a
  method:
  * **Brackets**: pay ranges, each with a fixed employee and employer amount.
  * **Percentage**: an employee and an employer rate of the pay, with the pay held between a lowest and a highest figure, and optionally
    a smallest and largest amount for each share.
  * **Tax brackets** (withholding tax only): pay ranges, each with a base tax and a rate on the amount above the start of the range.
* A range runs from its start up to, but not including, its end; the first starts at ₱0.00, each starts where the one before ends, and
  only the last has no end, so every amount falls in exactly one range. A rate is at most 100%. Every problem is reported at once,
  naming its row.
* The table that applies to a payroll is the **active table of the agency with the latest date on or before the last day of the pay
  period**. Only one active table of an agency may take effect on a given date.
* Accountants and administrators add, change and retire tables; HR officers read them. **A table an approved payroll used can't be
  changed** (the payroll must stay explainable): a change in the law is a new table with the date it takes effect. Retiring a table stops
  it applying to payrolls worked out afterwards; payrolls already worked out keep their amounts.

### 6.5 Workers

* Each worker is a member of SSS, PhilHealth and Pag-IBIG, and taxed, unless recorded otherwise (for example a minimum-wage earner is
  exempt from withholding tax). These choices are seen only by people who can see pay and changed by people who manage workers.

### 6.6 Calculation

For each worker in a draft payroll, on the worker's gross pay in the run:

1. A **per-run** table is applied to the run's gross pay.
2. A **monthly** table is applied to an **estimated monthly pay = the run's gross pay × the company's pay runs a month** (1 to 4, set in
   company settings, 2 by default). The month's amount is divided by the runs a month and rounded half away from zero to the centavo, each
   run on its own (the rounding difference between runs is not carried over).
3. Percentage: the pay is held between the lowest and highest figures, each share is the pay times its rate rounded once, then held between
   that share's smallest and largest amounts. Brackets: the shares of the range the pay falls in.
4. Tax: the taxable pay is the pay less the employee's SSS, PhilHealth and Pag-IBIG contributions on the same basis (month or run); the tax
   is the range's base tax plus its rate on the amount above the range start, rounded once; never negative; nothing for an exempt worker.
5. A worker who isn't a member of a fund pays nothing to it, and the employer adds nothing.

The employee's amounts are deductions marked with their agency and table and reduce net pay. **The employer's shares are recorded on the
payroll, shown and exported, but not posted to a project**: the labor cost a payroll posts is still exactly its gross pay.

### 6.7 Limits, adjustments and recalculation

* Deductions (calculated and typed) together can never be more than the pay. When the calculated amounts would be, the payroll is not
  refused: they are taken in agency order (SSS, PhilHealth, Pag-IBIG, tax) until the pay left after typed deductions is used up, and the
  worker's line says "Deductions were limited to the pay".
* While a payroll is a draft, the person preparing it can **adjust** a calculated amount (0 or more, with a reason that stays on the
  payroll, never in the activity log) and **recalculate** the contributions and tax, which applies the current tables, worker details and
  runs a month and resets adjustments. Typed deductions are kept by both, and editing typed deductions never touches the calculated ones.
* An approved or voided payroll is final: its amounts never change, whatever happens to the tables later.
* The payroll's remittance summary shows, per agency, what was taken from the workers, what the employer adds, and the sum to remit. The
  payroll register gains a column for each employee and employer share. **Remitting is done by a person; the system pays nothing.**

* **Approval needs current tables.** The tables each statutory deduction was worked out from are recorded on the payroll. If a table is added,
  corrected or retired after the payroll was worked out, approving is refused with "Recalculate the contributions and tax, then approve", so an
  approved payroll can always be explained from its tables.
* **"Needs setup" is judged on the day the tables are picked from**, the last day of the pay period (not today): a payroll for last December
  processed in January is told about a table that only started in January.
* **To remit counts only what was calculated.** When an agency has no table, a contribution typed in by hand is a deduction like any other but is
  not in the remittance totals; the card says so.

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

**Owner call, 1 Nov 2026:** go with the recommended defaults in this section and in 10.1 as they stand, and revisit one when the
business needs it. Three open points were settled that way: (a) item 9 stays as documented, so labor cost is project cost, and a company
that does not want one worker's pay to be inferable posts workers to the whole project instead of a one-person sub-project; (b) a
per-project count of problem parts on the dashboard is not built, because the project page already shows which division is over
budget; (c) rotating the credentials that remain in the desktop program's git history is deferred by the owner, and the risk is
still listed in `docs/operations/secrets-and-access.md`.

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

### 10.1 Commercial controls

These rest on defaults chosen where the owner had not yet decided. Each is reversible; confirm or change them.

**Progress and earned value**

1. **The date can't go backwards for a line** (an update can't be dated before the line's latest entry). The alternative, allowing
   backfilled history, makes "the latest entry" ambiguous. Entering history for a project that started before the system is done in date
   order.
2. **Planned value is linear in time** from the start date to the target end date. Real plans are rarely linear (concrete before finishes);
   there is no schedule or baseline in the system to measure against. Confirm before using the schedule figures for decisions.
3. **The forecast uses the cost index method** (EAC = BAC x AC / EV), which assumes the rest of the work costs what the work so far did.
   Other methods (the budget for the rest, or a combined cost and schedule index) give different forecasts; the choice is one function.
4. **Spending is not matched to lines.** Actual cost is the project's posted cost, not the cost of each line, so earned value is judged
   for the project and per division of the budget, not line by line against spending.
5. **Committed cost is not in the forecast.** The forecast is earned value based; money promised in purchase orders and subcontracts
   appears on the dashboard and the Budget tab as committed cost, not here.
6. **Percent complete is whatever the person says.** There is no measurement from quantities installed; a project manager's judgement
   is the input, with the history of who said what and when.
7. **A project on hold or completed still accepts progress** (a completed project may be corrected to 100%); only archiving closes it.
8. **Accountants and viewers read progress** (as they read costs); they cannot record it.

**Purchasing and committed cost**

* **Default:** the number is taken when the draft is first saved, not at approval, and the company has one sequence of purchase orders
  (the specification's rule). A save that fails after the number was taken leaves a gap.
* **Default:** a closed order releases its whole unspent remainder; there is no partial close and no reopening. A mistaken close is
  corrected by raising a new order.
* **Default:** an order can be cancelled by anyone who may approve, as well as closed or approved; there is no separate permission for
  cancelling.
* **Default:** only **posted** invoices count as matched, so an order whose matched invoices have all been voided can be cancelled again.
* **Default:** the order total is stated with VAT as the headline figure; the comparison basis only decides which amount feeds the
  forecast.
* **Default:** the match between an invoice and an order line is chosen by a person; the system never matches automatically, and never
  pays or approves anything by itself.
* A cancel racing an invoice being matched to the same order at the same instant could leave a cancelled order with a matched invoice. The
  invoice is still a valid actual cost and the order commits nothing, so no figure is wrong; the order page lists the invoice.

**Change orders and the budget baseline**

1. Change orders need a locked budget; before locking, the budget is changed directly.
2. Approval and void figures use the amounts with VAT for the contract price (it includes VAT) and keep both net and gross for the budget.
3. "Later approved" means approved at a later moment (ties broken by number); voiding is strictly newest first.
4. Unlocking is allowed while change orders are submitted but not approved; they can't be approved until the budget is locked again.
5. A revision keeps the line's own VAT rate; an added line uses the company rate when the line is entered, not when it is approved.
6. Approving takes an optional note; rejecting, discarding and voiding need a reason (up to 500 characters).
7. The contract price can't be typed over on a locked budget; it changes only through change orders (first price excepted).

**Subcontracts**

1. **Default**: retention 10% and withholding 0% when not given (spec).
2. Retention is held on the **gross** (VAT-included) amount of each claim; withholding is on the **net**.
3. Only **one draft claim** is open at a time per subcontract, so the claim chain stays a single line.
4. A claim's number is given **at certification**, not when drafted, so discarded drafts leave no gap in certified numbers (a save that
   fails after taking the number can still leave one).
5. A payment can't be dated **before the claim date**; the claim's period can't end after its claim date.
6. Cancelling an awarded subcontract with no certified work **discards its open draft claim** with the cancel reason.
7. The posted subcontract cost is counted under the subcontract's **work item** (a standard work-breakdown code chosen while drafting); a
   subcontract with no work item posts unclassified cost, which the cost summary shows as "Unclassified".
8. A completed subcontract can still have its latest claim voided (as a correction) while retention released stays covered.

**Client billing and retention**

1. **Retention is released only once the project is Completed** (not, for example, at substantial completion or in instalments before).
2. **Standard terms**: 10% retention, no withholding, 30 days, until the project's terms are saved.
3. **Withholding is on the amount before VAT** of each progress bill, and none is taken on a retention release.
4. **A retention release carries no VAT**, because the VAT was billed with the work.
5. **Only the latest bill can be voided.** Correcting an older bill means voiding the later ones first, then billing again.
6. **People who prepare bills (project managers included) record payments**; only accountants and administrators issue bills and void
   bills or payments.
7. **No billing-date order is enforced** between successive bills; the chain is by issue order.
8. **Overdue starts the day after the due date.**

**Statutory deductions**

1. **Estimated monthly pay** for monthly tables is one run's gross pay times the pay runs a month, not the worker's actual pay for the
   month. A worker paid unevenly across runs is over- or under-deducted in a run; the month is not trued up. Confirm, or ask for a
   month-end true-up.
2. **Rounding per run**: each run's share of a monthly amount is rounded on its own, so the runs of a month may differ from the month's
   amount by a centavo or two.
3. **Employer share not charged to projects**: the employer's contributions are recorded and exported but not added to project labor cost.
   Confirm whether they should be part of project cost.
4. **Limit order**: when deductions would pass the pay, SSS is protected first, then PhilHealth, Pag-IBIG and tax.
5. **One active table per agency and date**: a retired table steps aside, so a corrected table can take its date.
6. **A table used by a payroll that was approved and later voided** still counts as used and can't be changed.
7. **Pay runs a month** defaults to 2 (semi-monthly).
8. **Tax exemption** is a yes/no on the worker; partial exemptions and annual tax reconciliation are not modelled.

**Narrow races that are accepted** (money stays correct; each is detected or harmless)

* Locking the budget at the very moment someone saves a budget line can leave that line out of the baseline: the baseline's reconciliation shows
  it (and the Changes page warns), and unlocking and locking again fixes it. Touching the project on every budget edit would make every other
  project action ask for a reload, so it is not done.
* Correcting a statutory table at the very moment a payroll is approved can let the approval through; the approved payroll keeps its own
  amounts, so no pay changes.

## 11. Progress and earned value

The retired desktop app had no progress at all: cost was only compared with the budget ("how much have we spent"), never with the work
done ("what did the money buy"). A project could be 90% spent and 40% built and nothing said so. This section adds progress per budget
line and the earned-value figures built on it. Amounts are in minor units; every percentage and index is in basis points (10,000 is
100% or 1.00).

### 11.1 Recording progress

* **Progress is a statement, and statements are only added.** A progress entry says how far along one budget line is (0% to 100%) on
  a business date, who said so, when it was saved, and an optional note. An entry is never edited or deleted; a correction is a newer
  entry. A line's **current percent** is its latest entry: the latest date, then the latest time saved. A line never updated is at 0%.
* **Dates.** An entry can't be dated in the future (company time zone), and an update can't be dated before the latest entry of a line
  it changes, so the latest entry is always the one that counts. The same day is allowed: the later save wins.
* **Going down needs a reason.** A percent lower than the line's current one needs a note (rework after an inspection, a part redone).
  Without one the update is refused on that line.
* **A batch, all or nothing.** People record several lines in one go (up to 200). Every problem is reported at once and nothing is
  recorded unless the whole batch is accepted. A line whose percent did not change is skipped, and a batch with nothing that changed is
  an error rather than an empty save.
* **Only active budget lines of the project.** A removed line, another project's line and another company's line are refused. A project
  that is archived takes no progress (restore it first); one that is on hold or completed does.
* **Repeated clicks are safe** (rule in section 1): a repeated request id returns the first result and records nothing more, even when
  two copies arrive at the same moment.
* **Stale figures.** When the person tells the system which percent they were looking at and someone saved a different one meanwhile,
  the update is refused and they are asked to reload, so two people updating the same line never overwrite each other unnoticed.
* **Access.** Administrators, project managers and cost engineers record progress. Those and accountants and viewers read it. Warehouse
  keepers and HR officers have no access. Progress changes nothing on the dashboard or the project cost summary.

### 11.2 Earned value

All figures use the company's comparison basis (section 4.7: with or without VAT), except the forecast margin, which always uses amounts
with VAT. Every division is done last and rounded once, half away from zero. A line's earned value is rounded on its own and the project
adds the rounded figures, so a roll-up always equals the sum of what is shown.

| Figure | Meaning | Rule |
| --- | --- | --- |
| Budget at completion (BAC) | What the work was planned to cost | The sum of the active budget lines |
| Earned value (EV) | What the work done is worth | Per line: budget x percent complete. The sum over lines |
| Actual cost (AC) | What was spent | Posted cost dated on or before the date: the same figure as the dashboard when the date is today |
| Planned value (PV) | What should be done by the date | BAC x the share of time from the project's start date to its target end date that has passed (0 before the start, 100% on or after the end). None without a target end date |
| Cost variance (CV) | Work done minus money spent | EV - AC. Negative: the work cost more than it was worth |
| Schedule variance (SV) | Work done minus work planned | EV - PV. Negative: behind. None without PV |
| Cost index (CPI) | Work earned for each peso spent | EV / AC. None while nothing is spent |
| Schedule index (SPI) | Work done against the plan | EV / PV. None without PV, or while nothing is planned yet |
| Forecast at completion (EAC) | What the project is expected to cost | BAC while nothing is earned or spent; otherwise BAC x AC / EV (the cost index method: the rest of the work costs what the work so far did) |
| To complete (ETC) | What the rest is expected to cost | EAC - AC, never below zero |
| Variance at completion (VAC) | Budget minus forecast | BAC - EAC. Negative: a forecast overrun |
| Percent complete | How much of the budget is earned | EV / BAC. None when nothing is budgeted |
| Forecast margin | What the company expects to keep | Contract price (with VAT) minus EAC worked out on amounts with VAT. None without a contract price |

* On the "excluding VAT" basis the forecast margin is computed from a second, with-VAT calculation (budget, earned value and cost with
  VAT), never by adding VAT to the net forecast.
* **Date.** The report can be read as of an earlier date: each line's percent is its latest entry on or before it and the cost is what
  was dated on or before it. The budget is always the budget as it stands now, because budget lines keep no history.
* **Ceiling.** A forecast above 10^18 centavos (a project with a great deal spent and almost nothing earned) is shown as that ceiling
  instead of failing the report; an index above the largest whole number is shown as that number.
* The Progress tab says it in words: "Spent ₱x for work worth ₱y", "Each peso spent is earning 93 centavos of work", "Behind schedule:
  the work done is 79% of what was planned by today". The server sends the figures; the screen only words them.

## 12. Purchasing and committed cost

### 12.1 Purchase orders
A **purchase order** is the company's promise to buy from one supplier for one project (and optionally one of its sub-projects). It has a
number ("PO-0001", one sequence per company, taken only once the order is valid), an order date (not in the future), an optional expected
date (not before the order date), the supplier's own reference and notes, and **1 to 100 lines**. A line is a cost line: classification,
description, item type, quantity, unit, unit cost and VAT treatment, with the company VAT rate **at the moment of entry** kept on it. Its
net, VAT and gross come from the same rules as budget lines and cost entries (section 4.4), and the order's total is the sum of its lines,
so `net + VAT = gross` on every line and on the order.

The firm must be an **active firm marked as a supplier**: an archived firm, or one that is only a subcontractor, is refused with the way
out ("Restore it" or "Edit the firm and tick Supplier"). A new order needs a project, and sub-project, that can receive records.

### 12.2 Steps and who may take them
| State | Meaning | Who |
| --- | --- | --- |
| Draft | Being prepared. The lines can be replaced. Nothing is committed. | Draft and edit: Administrator, Cost Engineer, Accountant |
| Approved | Lines locked. What is not yet invoiced is committed cost. | Approve: Administrator, Project Manager, Accountant |
| Closed | The supplier delivered less; the unspent remainder is released. Final. | Close: as approve |
| Cancelled | Called off with a reason. Final. | Cancel: as approve |

Nothing stops one person holding both roles; the person is recorded on every step. A draft can be cancelled. An approved order can be
cancelled only while **no posted invoice is matched to it**; once one is, the order is closed instead. An approved order is never edited:
cancel or close it and raise another. On an **archived project** nothing new is raised, and nothing is approved, cancelled or closed until
the project is restored. Approving checks the firm and the project again.

### 12.3 Matching an invoice
A supplier invoice (a manual cost entry) may name the **order line** it is for. The line must belong to an **approved** order of the
**same project**. An invoice larger than the line is allowed: the line then shows as "over by" the excess. Correcting an invoice keeps
its match even if the order has been closed since; a new match to a closed, cancelled or draft order is refused. The match can be changed
or cleared while the invoice is posted. Costs made by the system (payroll, stock) cannot be matched.

### 12.4 Committed cost and the forecast
For each line of an **approved** order, the open remainder is `line amount - posted invoices matched to it`, never below zero, worked out
on the amount before VAT and the amount with VAT separately. A project's **committed cost** is the sum of those remainders over its approved
orders (one line's overrun never reduces what another line still owes). A voided invoice no longer counts as matched, so its remainder
returns; a closed or cancelled order, and a draft, commit nothing.

**Forecast = actual cost + committed cost**, on the company's comparison basis (with or without VAT, section 4.7), and is judged against the
budget with the same thresholds as actual cost. The dashboard row, the dashboard total and the project's cost summary show the same
committed and forecast figures, and show nothing when nothing is committed. A forecast over the budget is shown as a warning, not as spent.

## 13. Subcontracts

The company gives work to subcontractors, certifies their progress claims (which become project cost), holds retention, and records what
it has paid. The system never pays anyone: a payment is a person recording that it was paid.

### 13.1 The subcontract

* A subcontract names a **firm set up as a subcontractor** (an archived firm, or one that is only a supplier, is refused), a project and
  optionally a sub-project that can receive records, the scope of work (up to 200 characters) and notes. It is numbered `SC-0001` in the
  company's sequence when it is drafted.
* The **contract sum** is a base amount with a VAT treatment, exactly like a cost line (section 4.4): it includes VAT, is before VAT, or
  has no VAT. The company VAT rate **at the moment of drafting is kept** on the subcontract, so a later change of the company rate never
  changes it or its claims.
* **Retention** is 0% to 30% (10% when not given). **Withholding tax** is 0% to 10% (0% when not given): the tax the company withholds from
  the subcontractor's payment, worked out on the amount before VAT.
* Lifecycle: **Draft -> Awarded (Active) -> Completed**, or **Cancelled**. A draft can be edited freely by people who draft subcontracts.
  **Awarding** (an approver) commits the sum to the project. **Completing** (an approver) needs no open draft claim and ends claims.
  **Cancelling**, with a reason, is allowed for a draft (by a drafter) or an awarded subcontract with nothing certified (by an approver).
* A **variation** adds or takes off work as a signed base amount with a description and date. While the subcontract is a draft a drafter
  records it; once awarded, only an approver. The **revised sum** (contract sum plus variations) must stay above zero and never fall below
  the work already certified.
* An archived project's subcontracts are read-only (restore the project first).

### 13.2 Progress claims

* The subcontractor's claim states the **work done to date**: the base amount of all work so far, including this claim. It must be more
  than the work already certified and not more than the revised sum. The claim date is not in the future; the period it covers ends on or
  before the claim date. Only one draft claim is open at a time.
* Worked out once, each figure rounded once half away from zero:

  | Figure | Rule |
  | --- | --- |
  | This claim | work to date - work already certified |
  | Net, VAT, gross | this claim on the subcontract's VAT treatment and kept rate (section 4.4) |
  | Retention | round(gross x retention rate) |
  | Withholding tax | round(net x withholding rate) |
  | **To pay (net payable)** | gross - retention - withholding |

  Example, 1,000,000.00 before VAT at 12%, 10% retention, 2% withholding: work to date 250,000.00 gives VAT 30,000.00, gross 280,000.00,
  retention 28,000.00, withholding 5,000.00, to pay 247,000.00. Claim 2 at 600,000.00 to date adds 350,000.00: gross 392,000.00, retention
  39,200.00, withholding 7,000.00, to pay 345,800.00. With VAT included, 250,000.00 splits into net 223,214.29 and VAT 26,785.71;
  withholding is 4,464.29 (2% of the net, 4,464.2858) and to pay 220,535.71.
* **Certifying** (an approver) re-checks the chain: a draft worked out on certified work that has changed since (for example the claim
  before it was voided) must be saved again first. Certifying numbers the claim (1, 2, 3 within the subcontract; numbers are never reused)
  and, **in the same save, posts the work as project cost**: one cost entry naming the subcontractor, item type Subcontract, dated the claim
  date, equal to the claim's net, VAT and gross to the centavo. **Retention is part of the cost** (it is still owed).
* A posted subcontract cost can't be edited or voided at the cost entry. It is corrected by **voiding the claim** (an approver, with a
  reason), which voids the cost in the same save. Only the latest certified claim can be voided, only while no payment is recorded against
  it, and never so that more retention would have been released than is then held. A draft claim is discarded with a reason by a drafter.
  Nothing is deleted.

### 13.3 Payments and retention

* A payment (an approver) is against a certified claim, more than zero and not more than what is still owed on it; dated not in the future
  and not before the claim. Its method is bank transfer, cheque, cash or other, with an optional reference and note.
* Retention held is the retention on certified claims. Once the subcontract is **completed**, an approver records retention releases, up to
  the retention still held.
* A payment is voided with a reason; what it paid becomes owed again. Outstanding is never negative.

### 13.4 Commitments

* An awarded subcontract commits the project to its revised sum not yet certified: `max(0, revised - certified)`, with and without VAT.
  It is added to actual cost to give the **forecast** on the dashboard and the project cost summary. As claims are certified, actual cost
  rises and the commitment falls by the same amount; a completed or cancelled subcontract commits nothing more; a draft commits nothing.

### 13.5 Access

Read: Administrator, Project Manager, Cost Engineer, Accountant, Viewer. Draft subcontracts and claims: Administrator, Project Manager, Cost
Engineer. Award, certify, void, record payments, release retention, complete: Administrator, Accountant. One person may hold both (an
administrator can do every step); the activity log records who did each.

## 14. Client billing and retention

The accounts side bills the client against the contract, holds retention, records what the client pays, and sees what is still to
collect. The system never sends a bill or collects money: **recording a payment is a person saying it was received.**

### 14.1 Billing terms

Each project has billing terms, saved the first time someone changes them; until then the standard terms apply. Changing them affects
only bills prepared afterwards: every bill keeps the terms it was prepared with.

| Term | Range | Standard |
| --- | --- | --- |
| Retention: the share of each progress bill the client holds until the project is completed | 0% to 30% | 10% |
| Creditable withholding tax: the share of the amount before VAT the client withholds and covers with a certificate | 0% to 10% | 0% |
| Payment terms: days from the billing date to the due date | 0 to 180 | 30 |

### 14.2 Progress bills

A progress bill states the **work completed to date**: the value, VAT included like the contract price, of all work billed so far
including this bill. It must be more than what the issued bills already billed, and no more than the project's contract price (a
project needs a contract price to be billed). Then, each figure rounded once, half away from zero:

* **This bill** = work completed to date − work billed before.
* **Net** = round(this bill × 10,000 ÷ (10,000 + VAT rate)); **VAT** = this bill − net (the company's VAT rate when the bill is prepared, kept on it).
* **Retention** = round(this bill × retention rate).
* **Withholding** = round(net × withholding rate).
* **The client pays** = this bill − retention − withholding.

Example (₱11,200,000.00 contract, 12% VAT, 10% retention, 2% withholding): work to date ₱2,240,000.00 gives net ₱2,000,000.00, VAT
₱240,000.00, retention ₱224,000.00, withholding ₱40,000.00, and the client pays ₱1,976,000.00. The next bill at ₱5,600,000.00 to date
bills ₱3,360,000.00: retention ₱336,000.00, withholding ₱60,000.00, the client pays ₱2,964,000.00.

### 14.3 Retention release

Once the project is **Completed**, a retention release bill gives back retention: any amount up to what is still held (withheld on
progress bills less earlier releases). It adds no VAT (the VAT was billed with the work) and nothing is retained or withheld again: the
client pays the amount.

### 14.4 Draft, issue, void

1. **Draft** (people who prepare bills): no number and nothing counts. A draft can be changed, which works its figures out again from
   what is billed now and the terms in force now, or **discarded** with a reason (it stays on record).
2. **Issue** (accountants and administrators): the bill gets the next company number (`BILL-0001`, `BILL-0002`, ...) and is locked.
   Issuing checks again that the draft's figures still follow from the project's issued bills: if another bill was issued or voided
   since the draft was prepared, it is refused and the draft must be saved again. A number refused or lost to a failed save leaves a gap.
3. **Void** (accountants and administrators, with a reason): only the project's **latest** issued bill, and only while no payment is
   recorded against it (void the payments first). The bill before it becomes the latest again.

The issued bills of a project form one unbroken chain, so **billed to date is always the latest issued bill's work completed to
date**. Two people issuing or voiding bills of the same project at the same moment can't both succeed.

### 14.5 Payments

A payment is recorded against an issued bill: an amount greater than zero and no more than what is still to collect on the bill, the
date received (not in the future), how the client paid (bank transfer, cheque, cash, other), and an optional reference and note.
People who prepare bills record payments; accountants and administrators void them with a reason, after which the amount is owed again.
What is still to collect on a bill is never negative.

### 14.6 What is owed

Never stored, always worked out: **paid** (payments not voided), **still to collect** (what the bill asks less paid), **due date**
(billing date + the bill's payment terms), **overdue** (today, in the company's time zone, is after the due date and something is still
to collect). A project's billing shows contract price, billed to date and its share of the price, collected, still to collect (and the
overdue part), and retention held. The company's receivables list every project with money to collect or retention held, overdue first.

An archived project's billing is read-only: no new bills, issuing, voiding, payments or terms until it is restored.
