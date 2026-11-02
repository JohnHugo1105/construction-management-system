# Schedule API

A project's schedule: planned activities with dates, each covering budget lines, a baseline, and the S-curve of planned value, earned
value and actual cost over time. The schedule's planned value is also what the Progress tab uses once the schedule has weight (see
[progress](progress.md)). All routes are under `/api`, return JSON in camelCase, and send `Cache-Control: private, no-store`. Money is
`long` minor units in properties ending `Minor`. Percentages are `int` basis points (`10000` is 100%). Dates are `"2026-11-02"`; instants
are UTC (`"2026-11-02T02:00:00+00:00"`). Writes need the anti-forgery header, like every other write.

There are no dependencies between activities, no critical path and no resource levelling: the schedule is activities with dates.

## Endpoints

| Method and route | Permission |
| --- | --- |
| `GET /api/projects/{projectId}/schedule` | `schedule.read` |
| `GET /api/projects/{projectId}/schedule/s-curve` | `schedule.read` |
| `POST /api/projects/{projectId}/schedule/activities` | `schedule.write` |
| `PUT /api/projects/{projectId}/schedule/activities/{id}` | `schedule.write` |
| `POST /api/projects/{projectId}/schedule/activities/{id}/remove` | `schedule.write` |
| `POST /api/projects/{projectId}/schedule/baseline` | `schedule.write` |

Roles: Administrator, Project Manager and Cost Engineer read and plan; Accountant and Viewer only read (the schedule is weighted by budget
amounts, so it is read by the people who read the Progress tab); HR Officer and Warehouse Keeper have no access. A project
or activity of another company is `404`, never an empty list, and a budget line of another company or project cannot be linked.

Every change answers with the whole schedule as it now stands (the `GET` shape), so the screen replaces what it shows without a second
request. An archived project's schedule can be read but not changed (`409`).

## Reading the schedule

`GET /api/projects/{projectId}/schedule` answers on today's date in the company's time zone, in four SQL statements however many
activities, lines and entries exist (project, company, activities with their linked lines, budget lines with today's percent):

```json
{
  "projectId": "...", "today": "2026-11-02", "basis": "IncludingVat", "projectArchived": false, "usesSchedule": true,
  "plannedMinor": 130000000, "baselinePlannedMinor": 125000000, "planWeightMinor": 200000000,
  "budgetMinor": 220000000, "unplannedBudgetMinor": 20000000, "unplannedLineCount": 1,
  "baselineSetAtUtc": "2026-10-01T02:00:00+00:00",
  "activities": [
    { "id": "...", "name": "Masonry walls", "plannedStart": "2026-10-01", "plannedFinish": "2026-10-28", "plannedDays": 28,
      "baselineStart": "2026-10-01", "baselineFinish": "2026-10-21", "slipDays": 7, "sortOrder": 1,
      "weightMinor": 60000000, "percentCompleteBps": 4000, "isBehind": true, "needsLines": false,
      "budgetLineIds": ["..."], "version": 4 }
  ],
  "budgetLines": [
    { "id": "...", "description": "CHB walls", "wbsCode": "4200", "division": 4, "divisionName": "Masonry",
      "budgetMinor": 60000000, "percentBps": 4000, "activityId": "..." }
  ]
}
```

* `basis` is the company's comparison basis; every amount uses it, the same line amounts the Progress tab uses.
* `activities` are the active activities in list order (`sortOrder`, then creation). Removed activities are absent.
* `weightMinor` is the budget of the activity's linked active lines. `plannedDays` counts both ends. `percentCompleteBps` is the
  budget-weighted percent of its lines (null with none linked; never 10000 while a line is open). `isBehind` is true when the planned
  finish is before today and the activity is below 100%. `needsLines` is true when no line is linked: the activity is listed but counts
  for nothing in the plan. `slipDays` is planned finish minus baseline finish (positive is later); null without a baseline.
* `plannedMinor` is the schedule's planned value by the end of today; `baselinePlannedMinor` is the same from the baseline dates, null
  until a baseline is set (an activity added after the baseline has no baseline dates and plans nothing there).
* `planWeightMinor` is the sum of the weights: the planned value reaches it at the last finish. `unplannedLineCount` and
  `unplannedBudgetMinor` are the active budget lines in no activity: their budget has no planned value.
* `usesSchedule` is true when at least one activity has weight; the Progress tab then takes its planned value from this schedule.
* `budgetLines` are the project's active lines for the line picker (grouped by `division`), with the activity that links each.

## The S-curve

`GET /api/projects/{projectId}/schedule/s-curve` answers in six SQL statements however many activities, lines, progress entries and costs
exist (the four above, the progress history grouped into steps, the posted cost grouped into steps; the last three are skipped without
activities):

```json
{
  "projectId": "...", "today": "2026-11-02", "basis": "IncludingVat", "hasBaseline": true, "stepDays": 7,
  "points": [
    { "date": "2026-09-01", "isToday": false, "plannedMinor": 3333333, "baselinePlannedMinor": 3333333,
      "earnedMinor": 0, "actualMinor": 0 },
    { "date": "2026-11-02", "isToday": true, "plannedMinor": 130000000, "baselinePlannedMinor": 125000000,
      "earnedMinor": 124000000, "actualMinor": 118000000 },
    { "date": "2026-11-03", "isToday": false, "plannedMinor": 160000000, "baselinePlannedMinor": 150000000,
      "earnedMinor": null, "actualMinor": null }
  ]
}
```

* Points run from the earliest start (planned or baseline) every `stepDays` days to the first point on or after the latest finish, plus
  a point for today when today falls between two of them. `stepDays` is 7, or a multiple of 7 when weekly points would be more than 119:
  there are **at most 120 points**. A two-year plan (731 days) has 106 weekly points and today: about 107 points, roughly 15 KB.
* All amounts are cumulative. `plannedMinor` and `baselinePlannedMinor` run to the end; `earnedMinor` and `actualMinor` (posted cost)
  stop at today and are null after it. The first point already counts progress and cost dated before the plan began.
* The point for today equals the Progress tab's planned value, earned value and spent amount (an API test pins it).
* No activities: `points` is empty.

## Adding and changing an activity

`POST .../schedule/activities` answers `201` with the schedule:

```json
{ "name": "Masonry walls", "plannedStart": "2026-10-01", "plannedFinish": "2026-10-28",
  "budgetLineIds": ["..."], "clientRequestId": "6f1c..." }
```

`PUT .../schedule/activities/{id}` answers `200` with the schedule; it takes the same fields plus `sortOrder` (null keeps the position) and
the `version` the person opened. A change that changes nothing saves nothing.

* `name` is required, at most 120 characters. `plannedStart` and `plannedFinish` are required dates between 2000 and 2100, and the
  finish can't be before the start (`422` on `plannedFinish`).
* `budgetLineIds` may be empty (the activity is then listed with `needsLines`). At most 500 lines per activity. Each must be an active
  line of this project (`422` on `budgetLineIds[i]`: "That budget line isn't part of this project any more."), and **a line belongs to
  at most one activity**: a line already in another active activity is `422` on `budgetLineIds[i]`, naming that activity.
* `clientRequestId` (up to 80 characters) makes the create safe to retry: the same id answers with the schedule instead of adding a
  second activity; an id already used on another project is `409`.
* A stale `version` is `409` with code `concurrency_conflict`.

Every add and change writes an audit line with the name, the dates and how many lines (never an amount).

## Removing an activity

`POST .../schedule/activities/{id}/remove` with `{ "reason": "Merged into Masonry walls", "version": 4 }` answers `200` with the schedule.
The reason is required (at most 300 characters) and stays on the activity's record. A removed activity leaves every figure and frees its
budget lines for another activity; it can't be changed or restored. This is planning data, not a financial record: it is removed with a
reason and audited, not voided.

## Setting the baseline

`POST .../schedule/baseline` with the activities and versions the person saw:

```json
{ "activities": [ { "id": "...", "version": 4 }, { "id": "...", "version": 2 } ] }
```

It copies every active activity's planned dates to its baseline dates and answers `200` with the schedule. It can be set again; each time
the baseline becomes the plan as it stands, and each time writes an audit line. A list that does not match the active activities and their
versions exactly (one added, changed or removed since) is `409` with code `concurrency_conflict`, so the person reloads and confirms
what they are fixing. With no activity it is `409` ("Add an activity to the schedule before setting the baseline.").

## Errors

| Status | When |
| --- | --- |
| `403` | The person lacks `schedule.read` (reads) or `schedule.write` (changes). |
| `404` | The project or activity is not in the person's company, or the activity belongs to another project. |
| `409` `concurrency_conflict` | A stale `version`, or a baseline request for a schedule that changed since it was read. |
| `409` `rule_violation` | The project is archived, the activity was removed, no activity exists for a baseline, or a request id was used elsewhere. |
| `422` | A field is missing or wrong; `errors` is keyed by field (`name`, `plannedStart`, `plannedFinish`, `budgetLineIds[i]`, `reason`). |

The rules and their figures are in [business rules](../business-rules.md) (the schedule section) and in `SchedulePlan`.
