# Time clock API

Workers scan their own badge (a Code 128 barcode or a QR code of a 12-character code) at a machine when they arrive and when they leave. The
machine sends the scans here; a person reviews each project's day and confirms it, which records attendance through the attendance rules.
**A scan never reaches pay by itself**: payroll reads confirmed attendance only, exactly as it does for hours typed by hand.

All routes are under `/api`, return JSON in camelCase and send `Cache-Control: private, no-store`. Dates are `"2026-11-02"`; instants are
ISO 8601 with an offset. Hours are decimals with two places. No response here carries a pay figure.

## Endpoints

| Method and route | Who | Notes |
| --- | --- | --- |
| `POST /api/timeclock/punches` | A registered machine (`X-Device-Key`) | The machine route below. No cookie, no anti-forgery token. |
| `GET /api/timeclock/days?projectId&date&page&pageSize` | `timeclock.review` | Without `date`: the earliest day before today that still waits, or today. |
| `GET /api/timeclock/summary?projectId` | `timeclock.review` | What waits on the project before today. |
| `POST /api/timeclock/days/confirm` | `timeclock.review` | Confirms rows into attendance. |
| `POST /api/timeclock/punches/manual` | `timeclock.review` | A supervisor adds a missing scan, with a reason. |
| `POST /api/timeclock/punches/{id}/void` | `timeclock.review` | Removes a wrong scan, with a reason (kept, marked removed). |
| `POST /api/timeclock/import?projectId` | `timeclock.manage` | A machine's exported file, as text. |
| `GET /api/timeclock/badges?workerIds=...&workerIds=...` | `timeclock.manage` | At most 100 workers. |
| `POST /api/timeclock/badges/issue-missing` | `timeclock.manage` | Codes for active workers without one, 100 at a time. |
| `POST /api/workers/{id}/punch-code` | `timeclock.manage` | Issue or reissue a worker's code (`{ "version": 3 }`). |
| `GET /api/timeclock/devices` | `settings.manage` | The registered machines and the capability line. |
| `POST /api/timeclock/devices` | `settings.manage` | Register a machine; the answer carries its key, once. |
| `POST /api/timeclock/devices/{id}/rotate-key` | `settings.manage` | A new key; the old one stops working at once. |
| `POST /api/timeclock/devices/{id}/revoke` | `settings.manage` | Stops the machine for good. |

Roles: `timeclock.review` is Administrator, Project Manager and HR Officer; `timeclock.manage` is Administrator and HR Officer;
`settings.manage` is Administrator. A project, worker, scan or device of another company is `404`. A stale `version` is `409`. Field problems
are `422` problem details with `errors`; a rule that refuses the change is `409` with a plain message.

## The machine route: `POST /api/timeclock/punches`

This is the contract for a machine integrator (a gateway in front of a dedicated time clock) and for the kiosk page.

**Authentication.** Send the device key in the `X-Device-Key` header. The key is 43 characters of URL-safe base64 (256 random bits). An
administrator registers the machine under Administration, Time clocks, and sees the key once; it is stored only as a SHA-256 hash. The
request belongs to the company that registered the device and to nothing else: the body never names a company, a project or a worker id.
The scans count for the project the device was registered for.

* A missing, malformed, unknown, rotated-out or revoked key is `401` with the same body every time (no detail about why).
* The route takes no cookie: a person's session can't reach it, and the key opens no other route (`401` everywhere else).
* **Rate limit: 120 requests a minute per key** (fixed one-minute window). Over it the answer is `429`; wait and send again. Batch scans
  rather than sending one request per scan when catching up.
* The body is at most 256 KB.

**Request.**

```json
{
  "punches": [
    { "clientPunchId": "gate1-000123", "workerCode": "ABCD-EFGH-JKMN", "punchedAt": "2026-11-02T07:02:10+08:00", "direction": "In" },
    { "clientPunchId": "gate1-000124", "workerCode": "23456789ABCD", "punchedAt": "2026-11-02T07:02:40+08:00" }
  ]
}
```

* `punches`: 1 to 200 scans. An empty list or more than 200 is `422`.
* `clientPunchId` (required, up to 80 characters): the machine's own id for the scan, **unique per machine**. It is the idempotency key:
  sending the same id again (a retry, a whole queue sent twice) stores nothing and answers `AlreadyRecorded`. Use a sequence number or a UUID
  kept with the scan until the server has answered it.
* `workerCode` (required): what the badge says. Case, spaces and dashes don't matter (`abcd efgh jkmn` is `ABCDEFGHJKMN`).
* `punchedAt` (required): the time the machine scanned it, **with its offset**. Send the original time when a queue is sent late.
* `direction` (optional): `In`, `Out`, or `Auto` / left out. Auto is the opposite of the worker's last scan that work day on the project
  (the first scan of a day is In).

**Response** `200`, one result per scan, in the order sent:

```json
{
  "results": [
    { "clientPunchId": "gate1-000123", "outcome": "Accepted", "displayName": "Juan Dela Cruz", "direction": "In",
      "punchedAt": "2026-11-01T23:02:10+00:00", "localTime": "07:02", "notPostedHere": false, "reasonCode": null, "reason": null },
    { "clientPunchId": "gate1-000124", "outcome": "Rejected", "displayName": null, "direction": null, "punchedAt": null,
      "localTime": null, "notPostedHere": false, "reasonCode": "BadgeNotActive", "reason": "This badge isn't active. See your supervisor." }
  ]
}
```

* `Accepted`: stored. `displayName` (first and last name), `direction` and `localTime` (the company's time of day) are for the screen at the
  gate; nothing else about the worker is ever returned. `notPostedHere` is true when the worker isn't posted to this project that day (the
  scan is still kept; tell the worker to see their supervisor).
* `AlreadyRecorded`: nothing stored. The same `clientPunchId` from this machine was sent before, or the same worker scanned on this machine
  within 60 seconds. It carries the earlier scan's name, direction and time.
* `Rejected`: nothing stored, with `reasonCode` and a plain `reason` to show:
  * `BadgeNotActive`: an unknown code, a replaced (reissued) badge, or a worker who is not active. **The three answer identically**, so the
    route can't be used to find out which codes exist.
  * `ClockWrong`: `punchedAt` is more than 5 minutes ahead of the server's clock, or more than 7 days old. Fix the machine's clock.
  * `Unreadable`: no `clientPunchId`, one longer than 80 characters, no `punchedAt`, or a `direction` that isn't In, Out or Auto.
  * `ProjectClosed`: the device's project is archived.

A machine should drop a scan from its queue once the server has answered it (any outcome) and keep everything else. `401` means the key
needs replacing; `429` and `5xx` mean try again later; nothing was stored for an unanswered request except what a retry will report as
`AlreadyRecorded`.

**Example.**

```sh
curl -sS https://constructopx.example.com/api/timeclock/punches \
  -H "Content-Type: application/json" \
  -H "X-Device-Key: $DEVICE_KEY" \
  -d '{"punches":[{"clientPunchId":"gate1-000123","workerCode":"ABCD-EFGH-JKMN","punchedAt":"2026-11-02T07:02:10+08:00"}]}'
```

**Checking a key without recording anything.** Send `{"punches":[]}`: `422` means the key is accepted (the batch is empty), `401` means it
isn't.

Vendor protocols of particular time clock brands are not built: a machine that can't make this request needs a small gateway, or its export
file can be imported.

## Import: `POST /api/timeclock/import?projectId={id}`

Body: `{ "csv": "<the file's text>" }`. One scan per row, `code,timestamp[,direction]`, an optional header row starting with `code`, at most
5,000 rows (about 1 MB). A timestamp with an offset (`2026-11-02T07:02:00+08:00` or `...Z`) is taken as written; without one
(`2026-11-02 07:02`) it is the company's local time. The same rules as the machine route apply (codes, the 5-minute and 7-day bounds, the
60-second duplicate rule, automatic directions); there is no machine, so importing the same file twice stores nothing the second time. The
file is never stored.

Response: `{ "rows": 7, "accepted": 3, "alreadyRecorded": 1, "rejected": 3, "rejectedRows": [{ "row": 6, "reason": "..." }] }`. `row` is the
line number in the file; a row's content is not echoed back.

## Review

`GET /api/timeclock/days` returns `projectId`, `projectName`, `projectArchived`, `workDate`, the page (`items`, `total`, `page`, `pageSize`)
and `summary`. Each item is one worker's day:

| Field | Meaning |
| --- | --- |
| `firstIn`, `firstInLocal`, `lastOut`, `lastOutLocal` | The first time in and last time out (instants and the company's time of day). |
| `breakMinutes` | The unpaid break taken off a single span of 5 hours or more, or the gaps the worker scanned out for. |
| `regularHours` | Worked time after the break and rounding, up to the company's paid hours per day. |
| `proposedOvertimeHours` | The rest. **Proposed only**: recorded as overtime only when the reviewer approves it for the row. |
| `nightHours` | Worked time between 22:00 and 06:00, rounded the same way, never more than the hours worked. |
| `exceptions` | `MissingTimeOut`, `MissingTimeIn`, `NotPostedHere`, `TooLong` (more than 16 hours). |
| `alreadyRecorded` | The worker already has attendance on this project that day; the row is never confirmed over it. |
| `canConfirm` | No missing scan, hours above zero, not recorded, project open. |
| `isClean` | No exception and no overtime waiting: "Confirm all clean rows" takes it. |
| `punches` | The scans (`localTime`, `direction`, `source` Kiosk/Terminal/Import/Manual, `deviceName`, a manual scan's `reason`, `version`). |

`summary`: `daysWaiting`, `rowsWaiting` (worker-days), `rowsWithExceptions` (a day whose In and Out counts differ, or a scan of a worker not
posted there) and `earliestDate`, counting days before today with scans not yet recorded as attendance.

`POST /api/timeclock/days/confirm` takes `{ "projectId", "workDate", "rows": [{ "workerId", "approveOvertime" }], "allClean": false }`.
Without `allClean`, exactly the listed rows are confirmed; a row's overtime is recorded only when `approveOvertime` is true. With
`allClean: true`, every row with no exception and no unapproved overtime is confirmed (approvals in `rows` count). A row with a missing scan
is never confirmed; a row already recorded is left as it is. Each confirmed row becomes an attendance entry (whole project, no sub-project)
through the attendance rules, marked `source: "TimeClock"` with `clockFirstIn`, `clockLastOut` and `clockBreakMinutes`. The answer is
`{ "confirmed": [workerIds], "skipped": [{ "workerId", "workerName", "reason" }] }`. Confirming again records nothing new.

`POST /api/timeclock/punches/manual` takes `{ "projectId", "workerId", "at": "2026-11-01T17:00", "direction": "Out", "reason", "clientRequestId" }`.
`at` is the company's wall clock. Refused (`422`) without a direction or reason, in the future, within 60 seconds of another scan of the
worker, or on a day the worker couldn't be recorded; refused (`409`) on a day already recorded as attendance or an archived project.
`POST /api/timeclock/punches/{id}/void` takes `{ "reason", "version" }` with the same day and project rules.

## Badges and devices

`GET /api/timeclock/badges` answers `{ "companyName", "items": [{ "workerId", "fullName", "employeeNumber", "jobTitle", "code" }],
"activeWithoutCode" }`. A worker of another company, or one that doesn't exist, is simply not in `items`. `code` is null until issued.

`POST /api/workers/{id}/punch-code` answers `{ "workerId", "code", "reissued", "version" }`; reissuing replaces the code and the old badge
stops working at once. Only an active worker gets a code. The audit says a code was issued, never the code.

`POST /api/timeclock/devices` takes `{ "name", "projectId", "kind": "Kiosk" | "Terminal", "clientRequestId" }` and answers
`{ "device": {...}, "key": "..." }`. The key is in this answer and in a rotate answer only. A repeated `clientRequestId` is refused (`409`),
because the key can't be shown again. `GET /api/timeclock/devices` answers `{ "items": [...], "capability": { "status": "Available" | "NeedsSetup",
"message" } }`: Available once an active device has sent scans.

The company settings (`PUT /api/organization`) take `timeClockDayStartMinutes` (0 to 720, default 240 = 04:00), `unpaidBreakMinutes` (0 to
180, default 60) and `punchRoundingMinutes` (0 to 60, default 15); left out, each stays as it is.

## What is not collected

No fingerprints, face data, photos or GPS positions are collected or stored, and no camera scanning is built. A badge carries only its random
code; the employee number and job title are printed on the card for people to read, never encoded.
