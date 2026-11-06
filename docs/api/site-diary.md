# Site diary API

The site diary: one structured report per project per day (weather, work done, issues and delays, visitors and inspections, safety notes and
the number of subcontractor workers on site). **A report carries no money**, and the attendance and deliveries read beside it select no pay
figure, no hours and no price. All routes are under `/api/projects/{projectId}/diary`, return JSON in camelCase and send
`Cache-Control: private, no-store`. Dates are `"2026-11-02"`. Writes need the anti-forgery header, like every other write.

## Endpoints

| Method and route | Permission |
| --- | --- |
| `GET /api/projects/{projectId}/diary?page&pageSize` | `diary.read` |
| `GET /api/projects/{projectId}/diary/{date}` | `diary.read` |
| `GET /api/projects/{projectId}/diary/{date}/filled-in` | `diary.read` |
| `POST /api/projects/{projectId}/diary` | `diary.write` |
| `PUT /api/projects/{projectId}/diary/{date}` | `diary.write` |
| `POST /api/projects/{projectId}/diary/{date}/void` | `diary.write` |

Roles: Administrator, Project Manager, Cost Engineer, Accountant, Warehouse Keeper and Viewer read (`diary.read`); Administrator, Project Manager
and Cost Engineer write (`diary.write`). HR Officer has no access. A project of another company is `404`, never an empty list. A date that is not
a date is `400`. A change on an archived project is refused (restore the project first). A stale `version` is `409`.

## The report

`weather` is `Fair`, `Cloudy`, `Rain`, `HeavyRain` or `Storm`. `status` is `Posted` or `Void`. `GET .../diary/{date}` returns the project's **live**
(not voided) report for the day, or `404`.

```json
{
  "id": "…", "projectId": "…", "projectName": "Riverside Residences", "projectArchived": false,
  "reportDate": "2026-11-02", "weather": "Rain", "weatherNote": "Showers from 1 pm",
  "workDone": "…", "issuesAndDelays": null, "visitorsAndInspections": null, "safetyNotes": null, "subcontractorWorkers": 6,
  "status": "Posted", "authorName": "Pia Santos", "createdAtUtc": "…", "updatedAtUtc": "…",
  "changeableUntil": "2026-11-03", "canChange": true,
  "onSite": { "count": 2, "names": ["Ana Abad", "Juan Reyes"] },
  "deliveries": [ { "supplierName": "Visayas Steel Corp.", "description": "Rebar 16 mm", "unit": "pc", "quantity": 55 } ],
  "version": 0
}
```

* `canChange` is true when the caller holds `diary.write`, the project is not archived, the report is Posted, and the caller is its author inside
  the window or holds `projects.manage`. `changeableUntil` is the last day the author may change it (the day after `reportDate`, on the company's
  calendar).
* `onSite.count` is the number of workers with attendance recorded that day on the project (each counted once; voided attendance does not count).
  `onSite.names` is **left out of the JSON** unless the caller holds `workforce.read` (sorted by last name, at most 300).
* `deliveries` are what posted deliveries received that day on the project's purchase orders, grouped by supplier, item and unit with the quantity
  summed (at most 200 lines). No unit cost, VAT or total is ever selected.

`GET .../diary/{date}/filled-in` returns `{ onSite, deliveries }` for any day (a day with no report too), for the form that writes the report.

`GET .../diary` returns a page, newest first, of Posted reports only: `{ id, reportDate, weather, workersOnSite, subcontractorWorkers,
workDoneFirstLine, authorName, version }`.

## Writes

* `POST` takes `reportDate` (not in the future on the company's calendar), `weather`, `weatherNote`, `workDone` (required), `issuesAndDelays`,
  `visitorsAndInspections`, `safetyNotes`, `subcontractorWorkers` (0 to 2,000) and an optional `clientRequestId`; it answers `201` with the report
  and a `Location` of the day's URL. A repeated request id returns the first report; a request id reused for another day is `409`. A day that
  already has a live report is `409` ("There is already a site report for 2 Nov 2026 on this project. Open it to correct it, or void it to write
  it again."). Problems with what was typed come back together as field errors (`422`).
* `PUT` corrects the report (the day and the author never change) and takes the same details plus `version`. The author may correct until the end of
  the next day; a person who holds `projects.manage` may at any time; anyone else is `409` with a message that says who can. Saving unchanged
  details changes nothing (same version, no audit line). The audit line names the fields changed, never the text.
* `void` takes a `reason` (required, up to 300 characters) and `version`, with the same who and when. The voided report is answered, leaves the
  list, and its day can be written again.

## Needs your action

`GET /api/needs-action` gains the group `diary-not-written` for callers who hold `diary.write`: one row per Active project that has no Posted report
for yesterday while one exists within the seven days before, with `id` = the project, `label` = its name and `detail` "No report for Sun 1 Nov".
Rows never carry `amountMinor`. Nothing is listed when yesterday is a company holiday.

## Statement counts

List 3, one report 6 (5 for a caller who may not see the workers' names), filled-in 4, however many reports, workers or deliveries exist.
