# Philippine practice: the contract for the four parallel slices

Research into how construction companies in the Philippines work (summarised in the owner's field guide) found five gaps between the
platform and everyday practice. They are built as four slices, because two of them change the same billing code: **G** billing
(down payment and its recovery, a second withholding line, the government-client setting), **H** payroll (premium pay and 13th-month
pay), **I** delivery receipts on purchase orders, **J** a bonds register.

This file is the contract: who owns which files, what each slice must do, and what it must not touch. It builds on
`docs/development/commercial-control-spec.md`: **its sections 1 (what exists), 3 (conventions) and 4 (where slices meet) apply to every
slice here unchanged.** Read them first, with `AGENTS.md`, `CLAUDE.md` and `docs/business-rules.md`. Rules marked **Default** are
business choices the owner has not confirmed; each slice lists them in its rules note so the lead can add them to the assumptions.

## 1. Ground rules (the same as last time)

* **Money** is `long` minor units, quantities `decimal`, rates basis points (`int`, 10,000 = 100%). Multiply first, divide last, round
  half away from zero once at the line, then add the rounded lines. Every typed amount is bounded by `MoneyLimits`.
* **Tenant isolation, audit, concurrency, idempotency, voiding** exactly as in the commercial-control spec. Financial records are voided
  with a reason, never deleted. A state change carries `Version`. A retriable create takes a `ClientRequestId`.
* **No migration.** Do not add, edit or commit an EF migration and do not run `dotnet ef`. The test host builds the schema from the
  model. The lead regenerates the single unreleased `InitialCreate` after integration.
* **No gradients** in CSS (`npm run check:no-gradients`). Plain words for people, no codes or vendor names. Show a thing when its moment
  arrives: no empty tiles, no zero chips. One primary action per screen. Phone first (375 x 812), no horizontal page scroll.
* **Do not edit a file you do not own** (section 2). If you need a change in one, say so in your report.
* **Rules note.** Do not edit `docs/business-rules.md`. Write your slice's rules, in the style of that document (plain, exact, with the
  figures), to `docs/development/notes/<slice>-rules.md` (new file). The lead merges it and deletes the note.
* **Tests.** Cover every business rule with a domain unit test and the API round trip with an API test (real SQL Server). Add Angular
  specs for the logic and the screens you build. Run the minimum affected tests, once, then fix and re-run only what failed. Run no
  background or wide test commands; never the whole solution test run.
* **Performance.** Every read you add must be a fixed number of SQL statements however many rows exist (assert it the way the existing
  endpoints do), and no new request waterfall in the UI. State "Performance: no measurable path touched" if that is true.
* **Environment.**
  * `source /tmp/claude-0/cms-env.sh` (sets `DOTNET_ROOT`, `PATH`, `CMS_TEST_SQLSERVER`). Run `CI=true` builds before reporting: CI
    builds with warnings as errors and applies the style rules (file-scoped namespaces and so on).
  * SQL Server runs on this machine. If connections are refused, run `bash /tmp/claude-0/start-sql.sh` and wait for it to print ready.
  * Your worktree has no `web/node_modules`: `ln -s /home/user/construction-management-system/web/node_modules web/node_modules`
    (it is ignored by git, never commit it). Do not run `npm ci`.
  * Never `pkill -f` anything. Stop only processes you started.
* **Report** (final message): what you built, files changed, tests run and result (counts), what you did not run and why, the Defaults
  you chose, anything you need from the lead (permissions, seed data, shared-file edits), and a one-line `Performance:` result.

## 2. Ownership ledger

| Slice | Owns |
| --- | --- |
| **G Billing** | `Cms.Domain/Features/Billing`, `Cms.Application/Features/Billing`, `Cms.Infrastructure/Features/Billing`, `Cms.Api/Endpoints/BillingEndpoints.cs`, `web/src/app/features/billing`, tests `*Billing*`. **Also**, only for the government-limit advisory: `ChangeOrderService.cs`, `ChangeOrderContracts.cs` (one new read-only field on the change-order DTO), the change-order page in `web/src/app/features/change-orders` (one banner), and the matching tests. |
| **H Payroll** | `Cms.Domain/Features/Payroll`, `Cms.Application/Features/Payroll`, `Cms.Infrastructure/Features/Payroll`, `Cms.Api/Endpoints/PayrollEndpoints.cs`, `web/src/app/features/payroll`, tests `*Payroll*`, `*Attendance*`. **Also** the worker's rest day: `Worker.cs`, `WorkerService.cs`, `WorkforceContracts.cs`, `WorkforceConfigurations.cs`, the worker form and page in `web/src/app/features/workforce`. |
| **I Receiving** | `Cms.Domain/Features/Purchasing`, `Cms.Application/Features/Purchasing`, `Cms.Infrastructure/Features/Purchasing`, `Cms.Api/Endpoints/PurchasingEndpoints.cs`, `web/src/app/features/purchasing`, tests `*Purchas*`, `*Receipt*`. |
| **J Bonds** | New: `Cms.Domain/Features/Bonds`, `Cms.Application/Features/Bonds`, `Cms.Infrastructure/Features/Bonds`, `Cms.Api/Endpoints/BondEndpoints.cs`, `web/src/app/features/bonds` (replace the placeholder `bonds-page.ts` and `bonds.routes.ts`, keep the exported names `BONDS_ROUTES`, `BondsPage`), tests `*Bond*`. |
| **Lead** | `Permissions.cs` (already extended), `EndpointSecurityTests.cs` (remove your permissions from `ExpectedUnusedWhileBuilding` when your endpoints land: that one edit is allowed), `app.routes.ts`, `navigation.ts`, `projects.routes.ts`, `project-detail-page.ts`, `project-billing-tab` placement of the bonds card, `DevelopmentSeeder.cs`, `docs/business-rules.md`, `README.md`, migrations, `CmsDbContext.cs`, `DependencyInjection.cs`. |

Already done by the lead: the permissions `purchasing.receive` (Administrator, Project Manager, Cost Engineer, Accountant), `bonds.read`
(Administrator, Project Manager, Cost Engineer, Accountant, Viewer), `bonds.manage` (Administrator, Project Manager, Accountant) and
`payrollrules.manage` (Administrator, Accountant, HR Officer); their web constants; the `/bonds` route and a placeholder page; a "Bonds
register" link on the Billing page. Entity configurations are found automatically; there is no `DbSet` to add. Use `Set<T>()` through
the existing repository pattern.

## 3. Slice G: billing (down payment and its recovery, second withholding, government client)

**Why.** A down payment is the most common cash-flow term in both private and government jobs (government infrastructure allows up to
15% of the contract price, recovered by deductions from later progress payments; private jobs commonly take 20% to 30%). Government
clients withhold both income tax and VAT. A government client also brings rules (the advance cap and the cap on added variation orders).

**Terms** (`ProjectBillingTerms`, snapshotted on every bill like the existing terms):

* `AdvanceBps`: the down payment as a share of the contract price. 0 to 5,000. **Default 0.**
* `VatWithholdingBps`: the share of the **amount before VAT** that the client withholds as VAT, covered by its own certificate. 0 to
  1,200. **Default 0.** (The existing `CreditableWithholdingBps` stays and is the income tax line.)
* `IsGovernmentClient`: yes or no. **Default no.** When yes: the advance may not exceed 1,500 (15%): saving more is refused with
  "A government project allows a down payment of up to 15% of the contract price."

**Advance bill** (`ClientInvoice` kind `Advance`):

* One live (not void) advance bill per project. The project needs a contract price and `AdvanceBps` greater than 0.
  `AdvanceMinor = round(contract price x AdvanceBps / 10,000)`.
* **Default: the advance is a receivable only.** No VAT split, no retention, no withholding, `NetReceivable = AdvanceMinor`. VAT is
  billed on the progress bills at the full value of the work, so it is never counted twice. (The accountant confirms the VAT treatment of
  advances; the rule is one function and easy to change.)
* The number comes from the existing `client-invoice` sequence **at issue**. It is issued before any progress bill: refuse otherwise
  ("Bill the down payment before the first progress bill."). It can be voided only while no payment is recorded against it and no
  progress bill has been issued after it; a draft is discarded with a reason, as for any bill.
* The advance bill keeps its own snapshot of `AdvanceBps`; recovery always uses that rate, not the terms as they are later edited.

**Recovery on progress bills** (cumulative, so it is exact at 100%):

* `AdvanceRecoveryMinor` on a progress bill = `min(AdvanceMinor, round(CumulativeWorkMinor x advanceBps / 10,000)) - recovered on earlier
  issued progress bills`, never below 0. With no issued advance bill, recovery is 0.
* `NetReceivable = gross - retention - withholding - VAT withholding - advance recovery`. Retention and both withholdings are
  computed as today, on the amounts **before** recovery. If the deductions exceed the bill, refuse: "The deductions are more than this
  bill. Check the down payment, retention and withholding rates."
* The draft shows recovery in the preview. At issue, recompute against the issued chain and refuse a stale draft with the existing
  reload message (an advance issued, or a previous bill voided, since the draft was made).
* A bill's `VatWithholdingMinor = round(net x VatWithholdingBps / 10,000)`.
* Voiding the latest issued progress bill restores the recovery it took (it is derived, nothing is stored on the advance).
* Reads add `advanceBilledMinor`, `advanceRecoveredMinor`, `advanceToRecoverMinor` to the project billing summary, and each bill carries
  `advanceRecoveryMinor` and `vatWithholdingMinor`. The receivables overview counts what is outstanding exactly as now (the advance bill
  is a bill like any other and is paid by recorded payments).

**Government limit on added variation orders (advisory, not a block).** `ChangeOrderService` reads the project's billing terms. When
`IsGovernmentClient`, each change order DTO carries `governmentLimit`: `applies`, `limitMinor` (10% of the baseline contract price),
`usedMinor` (the sum of the approved additive price changes, with this change order's own when it is not yet approved), `exceeds`. The
change-order page shows one line when `exceeds`: "Added work on this government project passes 10% of the original contract price.
Check that the agency has approved it." Approval is still allowed (**Default**: the rule is the agency's; the system warns).

**UI** (`web/src/app/features/billing`): the terms form (folded, as now) gains "Down payment (% of contract price)", "VAT withheld by the
client (%)" and "Government client" with its one-line help. The project Billing tab leads with "Bill the down payment" only when
`AdvanceBps` is greater than 0 and none exists, then the progress bills as today. The bill form and bill page breakdowns add two rows
only when they are not zero: "VAT withheld, N% of the amount before VAT" and "Down payment recovered". The money line adds "Down payment
still to recover" only when an advance exists. All figures are the server's. Update the existing fixtures and specs rather than adding
parallel ones.

## 4. Slice H: payroll (premium pay and 13th-month pay)

**Why.** Daily-paid crews are owed rest-day, holiday and night premiums and a 13th-month pay, project employees included. Today these are
typed in by hand as overtime hours.

**Pay rules** (new aggregate `PayRules`, one per company, defaults when none exists, edited with `payrollrules.manage`, read with
`payroll.read`). **Default** factors, in basis points of the ordinary hourly rate, bounded 10,000 to 50,000:
rest day 13,000; special (non-working) day 13,000; special day that is also the rest day 15,000; regular holiday 20,000; regular
holiday that is also the rest day 26,000. Plus `PremiumDayOvertimeExtraBps` **3,000** (overtime on a premium day is paid at the day's
factor times 1.30) and `NightDifferentialBps` **1,000** (10%). Ordinary-day overtime keeps using the company overtime multiple that
already exists (one answer in one place; do not add a second one). A change to the rules affects drafts when recalculated; an approved
run keeps its amounts.

**Holiday calendar** (new `CompanyHoliday`: company, date, name up to 80, type `Regular` or `Special`; one per date). Added and removed by
`payrollrules.manage`. Changing or removing a holiday that a non-void approved run covers is refused ("A payroll that covers this date was
approved. Void it before changing this holiday."). **No real holiday is hard-coded**; the demo seed adds illustrative ones.

**Rest day.** `Worker.RestDay`: a day of the week, or not set. **Default: not set**, meaning the worker has no fixed rest day and every
day is ordinary. HR sets Sunday for crews that rest on Sunday. (Defaulting to Sunday would silently pay a premium nobody agreed.)

**Day type** of an attendance date for a worker: Ordinary, RestDay, Special, SpecialRestDay, RegularHoliday, RegularHolidayRestDay,
from the date's holiday (if any) and the worker's rest day. The server decides it; the client displays it.

**Pay of an attendance entry** (each amount rounded once, multiply first, divide last, shares across projects add up exactly as today):

* Regular hours pay = hourly rate x regular hours x day factor (ordinary day factor 10,000).
* Overtime pay = hourly rate x overtime hours x (ordinary day: the company overtime multiple; premium day: day factor x (10,000 +
  `PremiumDayOvertimeExtraBps`) / 10,000).
* Night differential = hourly rate x night hours x day factor x `NightDifferentialBps` / 10,000. **Default: it is computed on the day's
  regular-hour rate, also for night hours that are overtime**, which slightly understates night overtime; documented.
* `NightHours` is a new field on the attendance entry (0 to the hours worked that day), typed by the person who knows the shift. The
  system does not infer shifts.
* **Unworked regular holiday.** For a **daily-rated** worker, an active worker posted to a project on a regular holiday with **no
  attendance entry that day**, and where the day is not the worker's rest day, a payroll run pays one day at the daily rate, posted to
  the project the worker is posted to that day. **Default: the "present on the day before" condition is not checked**; HR marks a worker
  away or suspended through the existing status history if the day should not be paid. Hourly-rated workers get none (documented).
  This is a computed line in the draft; approval is a person's act as always.
* The run's lines and the worker's pay show the breakdown (Regular, Overtime and premium, Night, Holiday pay) folded under the gross, so
  the page is not longer; the gross is the sum.
* Needs-setup note: show one line on the run page only when the company has no holiday entered for the year of the period's end
  ("No holidays are entered for 2026. Add them under Payroll, Holidays if any fall in this period.").

**13th-month pay** (a new payroll run kind, `ThirteenthMonth`, for a calendar year):

* Basic pay for the year = the **plain-rate pay for regular hours worked** (hours x hourly rate, no day factor) summed over the worker's
  approved, non-void regular runs in that year. Overtime, premiums, night differential and holiday pay are excluded (**Default**,
  following the labor department's definition of basic salary). Store the plain-rate basic amount on each regular run line so the sum is
  exact and cheap to read.
* Amount per worker = `round(sum of basic / 12)`. A worker with no basic pay in the year is not included.
* One live (not void) 13th-month line per worker per year; a second run for the same year includes only workers not already in a live run.
* **No statutory contributions and no tax withheld on this run** (**Default**: tax exemption up to the legal ceiling is not tracked, so the
  full amount is paid and any tax on an excess is a manual deduction). Manual deductions still work.
* The cost posts to projects on approval like any run, split in proportion to each worker's basic pay per project over the year
  (largest remainder, so the shares add up exactly), with source name "13th-month pay". Voiding reverses it.
* Permissions: prepare with `payroll.run`, approve or void with `payroll.approve`, read with `payroll.read`. The payroll page offers
  "13th-month pay" as a secondary action; the primary action stays the next payroll run.
* Period rules do not apply (the period is the calendar year); a run for the current year is allowed at any time (an early payout when a
  worker leaves is common), a run for a future year is refused.

**UI.** `Payroll > Pay rules` and `Payroll > Holidays` pages (managers edit, readers read), the worker form's rest day, a night-hours field
on the attendance entry, a day-type chip next to a recorded attendance date ("Rest day", "Regular holiday"), and the 13th-month run page
(the same pattern as the payroll run page). Existing run pages show the folded breakdown.

## 5. Slice I: delivery receipts on purchase orders

**Why.** Today a purchase order links to invoices and nothing says what has actually arrived. Site teams need ordered, received and
invoiced to be three honest numbers.

* `GoodsReceipt` (aggregate): purchase order, project (from the order), received date (not in the future, company time zone),
  the supplier's delivery receipt number (up to 60, optional), note (up to 300), status `Posted` or `Void`, who and when, lines of
  (order line, quantity received). Append-only: a mistake is voided with a reason and entered again.
* Only for an order in status Approved. A closed or cancelled order refuses ("This order is closed.").
* A line's quantity must be greater than 0 and not more than the quantity still to receive (ordered minus received on non-void
  receipts). **Default: over-receipt is refused**; the supplier's extra is a new order or an amended one.
* **Duplicate protection:** the same delivery receipt number for the same supplier on a non-void receipt is refused ("This delivery
  receipt is already recorded.") when a number is given.
* A void is refused on an archived project ("Restore it first").
* Permissions: record and void with `purchasing.receive`, read with `purchasing.read`. Audit like the other purchasing actions (no
  reasons in audit summaries).
* Derived on the order read: per line `receivedQuantity`, `receivedValueMinor` (the line's unit price times the received quantity,
  rounded once, with the same net/VAT basis as the order's lines), and the existing invoiced amount; order-level `deliveryState`:
  NotReceived, PartlyReceived, FullyReceived. A line is flagged `invoicedAheadOfDelivery` when what was invoiced to it is more than the
  received value (advisory only, the invoice is still valid).
* **Delivery does not change committed cost.** Committed cost still falls only when an invoice is matched (**Default**; recorded in the
  rules note). Stock is not updated by a receipt (not built; say so on the screen only if it matters).
* The order list gets a delivery chip only when the order is Approved and something has arrived or is expected (no chip on drafts).
* **UI.** The order page gains a "Deliveries" section: the list of receipts with void, and "Record delivery" as the primary action on an
  Approved order with something left to receive (date, delivery receipt number, a quantity per line pre-filled with what is left, a
  note). The line table shows Ordered, Received and Invoiced quantities or amounts, with the "invoiced ahead of delivery" note only
  when it applies.

## 6. Slice J: bonds register

**Why.** Government and many private jobs require performance, advance payment and warranty bonds with a validity date. A lapsed bond is
a real risk and today nothing tracks it.

* `Bond` (aggregate): project (required), `Direction` (`Posted`: we gave it to the client; `Received`: a subcontractor or supplier gave
  it to us), `Kind` (`Bid`, `Performance`, `AdvancePayment`, `Warranty`, `Other`), issuer name (the bank or surety, up to 120), bond
  number (up to 60), amount (minor units, greater than 0, bounded by `MoneyLimits`) and the company currency, issued on, valid until (not
  before issued), the other party's name for a Received bond (up to 120, optional), note (up to 500), status `Active`, `Released`
  (returned, with a date and optional note), `Called` (the beneficiary claimed it, with a date and a note), `Void` (entered by mistake,
  with a reason). A bond is **not a cost and changes no money figure anywhere**; say so in the rules note.
* Actions (all `bonds.manage`, all need `Version`): create, edit the details while Active, **extend** the valid-until date (must be later
  than the current one; each extension is audited), release, mark called, void. An Active bond cannot be edited into an earlier valid-until.
  Released and Called are final; a wrong release is voided and re-entered. An archived project is read-only ("Restore it first").
* Derived `expiry` for an Active bond (company-zone date): `Valid`, `ExpiringSoon` (30 days or fewer left: **Default**, a constant),
  `Expired` (valid-until before today). An Active bond that has expired stays Active until a person releases or voids it; it is shown as
  Expired, never changed behind the user's back.
* Reads: company list `/api/bonds` (paged; filters: project, direction, kind, status, expiry; sorted by valid-until ascending for Active),
  one bond with its history, per project `/api/projects/{id}/bonds` (small list for the project page), and totals of the Active amounts
  by direction. Fixed statement counts.
* **UI.** `/bonds` (the placeholder route): the list with chips ("Expired", "Expires in N days"), filters folded, "Add bond" as the one
  primary action; a bond page (details, history, Extend, Release, Mark as called, Void with a reason). Export a standalone component
  `ProjectBondsCard` (selector `app-project-bonds-card`, input `projectId`, input `projectArchived`) that lists the project's Active
  bonds with "Add bond"; the lead places it in the project Billing tab. It shows nothing at all (not even a heading) when the project
  has no bonds and the person cannot manage bonds.
* **Not built, deliberately:** swapping retention for a bond, automatic expiry emails or messages, bond premiums as costs. Say so in the
  rules note.

## 7. After the slices (lead)

One regenerated migration; demo data on the Riverside project (an advance bill, a holiday calendar and pay rules, a received delivery,
two bonds with one expiring soon); sections 15 to 18 of `docs/business-rules.md`; the README feature table; the full .NET and Angular
runs; an independent development-lead review and a security review of the whole diff; fixes in one batch.
