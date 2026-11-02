# Progress API

How far along each budget line is, and earned value: what the work done is worth against what was spent and what was planned, and
what the project is forecast to cost. All routes are under `/api`, return JSON in camelCase, and send `Cache-Control: private, no-store`.
Money is `long` minor units in properties ending `Minor` (`125050` is 1,250.50). Percentages and indices are `int` basis points
(`10000` is 100% or 1.00). Dates are `"2026-11-02"`. Writes need the anti-forgery header, like every other write.

## Endpoints

| Method and route | Permission |
| --- | --- |
| `GET /api/projects/{projectId}/progress?asOf=` | `progress.read` |
| `POST /api/projects/{projectId}/progress` | `progress.write` |
| `GET /api/budget-lines/{id}/progress?page=&pageSize=` | `progress.read` |

Roles: Administrator, Project Manager and Cost Engineer read and record; Accountant and Viewer only read; HR Officer and Warehouse
Keeper have no access. A project or budget line of another company is `404`, never an empty list.

## Reading a project's progress

`GET /api/projects/{projectId}/progress` answers with one object, built in a fixed number of SQL statements however many lines and
entries the project has (project, company, lines with their current percent, actual cost, and the schedule's activities):

```json
{
  "projectId": "...", "asOfDate": "2026-11-02", "basis": "IncludingVat", "hasProgress": true, "lastUpdatedOn": "2026-10-20",
  "earnedValue": {
    "budgetMinor": 170000000, "earnedMinor": 80000000, "spentMinor": 40000000, "plannedMinor": 68000000,
    "costVarianceMinor": 40000000, "scheduleVarianceMinor": 12000000, "cpiBps": 20000, "spiBps": 11765,
    "forecastMinor": 85000000, "toCompleteMinor": 45000000, "forecastVarianceMinor": 85000000,
    "percentCompleteBps": 4706, "plannedPercentBps": 4000, "contractPriceMinor": 300000000, "forecastMarginMinor": 215000000
  },
  "divisions": [
    { "division": 3, "name": "Concrete", "budgetMinor": 150000000, "earnedMinor": 60000000, "percentBps": 4000,
      "lines": [ { "budgetLineId": "...", "description": "Footings", "wbsCode": "3100", "budgetMinor": 100000000,
                   "percentBps": 5000, "lastUpdatedOn": "2026-10-20", "earnedMinor": 50000000 } ] }
  ]
}
```

* `asOf` (a date, not in the future; default today) gives the figures as they stood that day: each line's percent is its latest entry
  on or before the date, and spending is the posted cost dated on or before it. A future date is `422` on `asOf`. The budget is always
  the budget as it stands now (budget lines keep no history).
* **Comparison basis.** `basis` is the company setting (`IncludingVat` or `ExcludingVat`). Every figure in the report uses it, except
  `forecastMarginMinor`, which always uses amounts with VAT because the contract price includes VAT.
* `divisions` list the active budget lines by division in work-breakdown order, unclassified last. Each carries its roll-up. Removed
  lines are absent. A line that was never updated is at `percentBps: 0` with `lastUpdatedOn: null`.
* `hasProgress` is false until any line has an entry on or before the date.
* **Planned value.** `plannedFrom` says where `plannedMinor` comes from: `"Schedule"` when an activity of the project's
  [schedule](schedule.md) covers budget lines (the planned value is then the schedule's planned value on the date, and
  `scheduleVarianceMinor`, `spiBps` and `plannedPercentBps`, the planned value as a share of the budget, follow it), `"ProjectDates"` for
  the straight line between the project's start and target end dates, or null without either. Forecasts use the cost index and never
  the planned value, so a schedule changes none of them.
* Nulls: `plannedMinor`, `scheduleVarianceMinor`, `spiBps` and `plannedPercentBps` are null without a target end date (`spiBps` also
  while nothing is planned yet); `cpiBps` is null while nothing is spent; `percentCompleteBps` is null when nothing is budgeted;
  `contractPriceMinor` and `forecastMarginMinor` are null without a contract price.
* **On the dashboard.** With no `asOf` (today), `forecastMinor`, `forecastVarianceMinor` (negated) and `percentCompleteBps` are the figures
  the dashboard rows and the cost summary carry as `progressForecast { estimateAtCompletionMinor, overBudgetMinor, percentCompleteBps }`
  (see [costing](costing.md)); an API test pins that they are equal.

The formulas are in [business rules](../business-rules.md) (progress and earned value) and in `EarnedValueCalculator`.

## Recording progress

`POST /api/projects/{projectId}/progress` takes a batch and answers `201` with `{ asOfDate, linesUpdated, recordedAtUtc }`:

```json
{ "asOfDate": "2026-11-02", "clientRequestId": "…",
  "lines": [ { "budgetLineId": "...", "percentBps": 6000, "note": null, "expectedPercentBps": 5000 } ] }
```

* Up to **200 lines**, **all or nothing**. A line whose percent equals its current percent is skipped (no entry is made); a batch with
  nothing that changed is `422` on `lines`.
* Entries are **append-only**: there is no edit or delete. A correction is a newer entry. A percent lower than the line's current one
  needs a `note` (up to 300 characters), or it is `422` on `lines[i].note`.
* The date may not be in the future (company time zone) and may not be earlier than the latest entry date of a line being changed
  (`422` on `asOfDate`); the same day is fine and the later save wins.
* Only **active budget lines of this project**. A line of another project, another company, a removed line or an unknown id is `422` on
  `lines[i].budgetLineId`, and says nothing about whether it exists elsewhere. A line twice in one batch is `422` there too.
* `percentBps` outside 0 to 10,000, or missing, is `422` on `lines[i].percentBps`. `i` is the position in the request. Every problem is
  reported at once and nothing is recorded.
* **Stale figure.** `expectedPercentBps` is optional: the percent the person was looking at. When another person saved a different
  figure for that line meanwhile, the whole batch is `409 concurrency_conflict` and nothing is recorded.
* **Repeats.** `clientRequestId` is shared by every entry of the batch (unique per company and line). A repeat returns the first
  result and records nothing more, including when two copies arrive at the same moment. The same id for another project or date is
  `409 rule_violation`.
* An archived project takes no progress: `409 rule_violation`, "Restore it first". Reading it still works. A project that is on hold or
  completed accepts progress.
* The audit trail records one line per batch (the project, the number of lines and the date), never the percentages or notes.

## A line's history

`GET /api/budget-lines/{id}/progress` returns `{ items, total, page, pageSize }`, newest first (latest as-of date, then latest saved):
`{ id, asOfDate, percentBps, note, recordedBy, recordedAtUtc }`. Page size is at most 100. A removed line keeps its history.
