# Costing API

Budget lines (what a project plans to spend), cost entries (what it has spent), the reference data for their forms, and the
budget-against-actual reports. All routes are under `/api`, return JSON in camelCase, and send `Cache-Control: private, no-store`.
Money is `long` minor units in properties ending `Minor` (`125050` is 1,250.50). Quantities are `decimal` with at most 4 decimals.
Dates are `"2026-11-02"`. Writes need the anti-forgery header, like every other write.

## Endpoints

| Method and route | Permission |
| --- | --- |
| `GET /api/projects/{projectId}/budget-lines` | `costs.read` |
| `GET /api/projects/{projectId}/budget-lines/export` | `costs.read` |
| `POST /api/projects/{projectId}/budget-lines` | `costs.write` |
| `GET /api/budget-lines/{id}` | `costs.read` |
| `PUT /api/budget-lines/{id}` | `costs.write` |
| `POST /api/budget-lines/{id}/remove` | `costs.write` |
| `GET /api/projects/{projectId}/cost-entries` | `costs.read` |
| `GET /api/projects/{projectId}/cost-entries/export` | `costs.read` |
| `POST /api/projects/{projectId}/cost-entries` | `costs.write` |
| `GET /api/cost-entries/{id}` | `costs.read` |
| `PUT /api/cost-entries/{id}` | `costs.write` |
| `POST /api/cost-entries/{id}/void` | `costs.write` |
| `GET /api/costing/reference` | `costs.read` |
| `GET /api/reports/dashboard?includeArchived=false` | `costs.read` |
| `GET /api/projects/{id}/cost-summary` | `costs.read` |
| `GET /api/projects/{id}/cost-summary/export` | `costs.read` |

Roles: Administrator, Cost Engineer and Accountant read and write; Project Manager and Viewer only read; HR Officer and Warehouse
Keeper have no access. A project, line or entry of another company is `404`, never an empty list.

## Lists

Both lists return `{ items, total, page, pageSize, totals: { netMinor, vatMinor, grossMinor } }`. `totals` add up the **whole
filtered set**, not the page, and never include removed lines or voided entries (even when those are listed). Page size is at most 100.

Budget line filters: `subProjectId`, `division` (1 to 16), `itemType`, `search` (description, scopes, work-breakdown code),
`includeRemoved`. Order: work-breakdown order, unclassified last.

Cost entry filters: `subProjectId`, `division`, `itemType`, `origin` (`Manual`, `Payroll`, `Inventory`), `from` and `to` (entry date,
both inclusive), `search` (description, supplier, invoice number), `includeVoided`. Order: newest entry date first.

Search treats `%` and `_` literally. An unknown `itemType` or `origin`, or `to` before `from`, is a `422` field error.

## Writing

Create and update take: `subProjectId?`, `division?`, `mainScope?`, `subScope?`, `wbsCode?`, `description`, `itemType`, `quantity`,
`unit`, `unitCostMinor`, `vatTreatment`, `entryDate`, `remarks?`. Cost entries also take `sourceDealer` and `invoiceNumber`, both
required. Create takes `clientRequestId?` and update takes `version`. `itemType` is `Material`, `Labor`, `Equipment`, `Subcontract` or
`Other`; `vatTreatment` is `Inclusive` (the unit cost has VAT in it), `Exclusive` (VAT is added) or `Exempt`.

* **VAT rate is not a field.** The company's rate at the moment of entry is stored on the line (`vatRateBps`). A later change of the
  company rate never changes an existing line, and editing a line keeps its own rate. Net, VAT and gross are worked out once by the
  domain and stored; `net + vat == gross` always.
* Every problem is reported at once as `422` with `errors` keyed by field. A missing or unknown `itemType` or `vatTreatment` is a field
  error, not a `400`.
* A stale `version` is `409 concurrency_conflict`. An archived project refuses new lines and entries, and refuses edits, removals and
  voids, with `409 rule_violation` (restore the project first).
* A repeated `clientRequestId` returns the original record instead of making a second one. Reusing it for another project is `409`.
* `POST .../remove` (budget line) and `POST .../void` (cost entry) take `{ reason, version }`. The record stays, with the reason, who
  and when; it leaves the default list and every total.
* **Cost entry dates** may not be in the future, judged by "today" in the company's business time zone, never the UTC day.
* **Duplicate supplier invoices.** The same supplier and invoice number (ignoring case, spaces and punctuation in the invoice number;
  trimmed and case-insensitive supplier) can be recorded once per project while the first is posted. A second is `422` on
  `invoiceNumber` and says when the first was recorded. Voiding the first frees the invoice; another project is unaffected. If two
  people save it at the same moment, the database index lets one through and the other gets `409 rule_violation`.
* **System-made costs** (`origin` `Payroll` or `Inventory`) are listed with `canEdit: false` and cannot be edited (`409`). The owning
  feature voids its own. Voiding one here is allowed with a reason; the audit line (`CostEntry.VoidedAtEntry`) says it was voided at
  the entry itself, not at its source.

## DTOs

`BudgetLineDto`: `id`, `projectId`, `subProjectId`, `subProjectName`, `division`, `mainScope`, `subScope`, `wbsCode`, `description`,
`itemType`, `quantity`, `unit`, `unitCostMinor`, `vatTreatment`, `vatRateBps`, `netMinor`, `vatMinor`, `grossMinor`, `entryDate`,
`remarks`, `status` (`Active`, `Removed`), `createdAtUtc`, `removedAtUtc`, `removedReason`, `version`.

`CostEntryDto`: the same fields except the removal ones, plus `origin` (`Manual`, `Payroll`, `Inventory`), `originReferenceId`,
`sourceDealer`, `invoiceNumber`, `status` (`Posted`, `Voided`), `voidedAtUtc`, `voidReason`, `canEdit`.

`CostingReferenceDto`: `divisions[] { number, mainScope, scopes[] { code, name } }`, `units[]`, `itemTypes[] { value, label }`,
`vatTreatments[] { value, label, hint }`, `vatRateBps`. Labels: "Unit cost includes VAT", "Unit cost is before VAT", "No VAT".

## Reports

Budget and actual are compared on VAT-inclusive (gross) amounts when the company's `varianceBasis` is `IncludingVat`, and on net
amounts when it is `ExcludingVat`. Removed budget lines and voided cost entries never count.

* `remainingMinor = budgetMinor - actualMinor` (negative is over budget).
* `usedBps = actual / budget` in basis points (`10000` is 100%); `null` when nothing is budgeted.
* `health`: `NoActivity` (nothing budgeted or spent), `OnTrack` (under 90% used), `NearLimit` (90% up to and including 100%),
  `OverBudget` (more than 100%), `Unbudgeted` (money spent where nothing is budgeted). Thresholds compare exact amounts, so 89.999% is
  still `OnTrack` even though `usedBps` rounds to `9000`.
* Margins are always on VAT-inclusive amounts, because the contract price includes VAT, whichever basis is chosen:
  `marginAfterActualMinor = contractPriceMinor - actual gross`, `budgetMarginMinor = contractPriceMinor - budget gross`. Both are
  `null` when the project has no contract price.

**Dashboard** `{ basis, projects[], totals }`. A row is `{ id, name, status, contractPriceMinor, budgetMinor, actualMinor,
remainingMinor, usedBps, health, marginAfterActualMinor, budgetMarginMinor }`, in name order. `totals` adds budget, actual and
remaining over every listed project; `contractPriceMinor` and the two margins add up only projects that have a contract price (`null`
when none has). Archived projects are listed only with `includeArchived=true`.

**Cost summary** `{ projectId, projectName, projectStatus, basis, contractPriceMinor, position { budgetMinor, actualMinor,
remainingMinor, usedBps, health }, marginAfterActualMinor, budgetMarginMinor, byDivision[], bySubProject[], byItemType[] }`.
`byDivision[]` rows are `{ division, name, <figures>, wbs[] { wbsCode, name, <figures> } }`; lines with no classification form one
`Unclassified` row (`division: null`, no `wbs`). `bySubProject[]` rows are `{ subProjectId, name, <figures> }` (`null` and
"Whole project" for the rest). `byItemType[]` rows are `{ itemType, label, <figures> }`. `<figures>` are `budgetMinor`, `actualMinor`,
`remainingMinor`, `usedBps`, `health`. Inside every breakdown rows run from the biggest overrun (smallest `remainingMinor`) to the most
money left; a row with actual cost and no budget is `Unbudgeted`.

Statement counts (SQL Server, measured by the API tests): lists 3, dashboard 4, cost summary 8, whatever the number of projects or
lines.

## CSV exports

`GET .../budget-lines/export` and `.../cost-entries/export` take the same filters as the list (all matching rows, not one page).
`.../cost-summary/export` is the variance by work-breakdown code (division and code order, unclassified last).

UTF-8 with a byte-order mark, invariant culture, amounts as plain decimals with two places (`125050` is `1250.50`), ISO dates, CRLF
rows. A text cell that starts with `=`, `+`, `-`, `@`, a tab or a carriage return gets a leading apostrophe (formula-injection
protection); amounts are never touched, so a negative remaining stays `-0.01`. File names: `<project>-budget-YYYY-MM-DD.csv`,
`<project>-costs-YYYY-MM-DD.csv`, `<project>-variance-YYYY-MM-DD.csv`, where the project name keeps only ASCII letters and digits
(accents dropped, everything else a dash) and the date is today in the company time zone.
