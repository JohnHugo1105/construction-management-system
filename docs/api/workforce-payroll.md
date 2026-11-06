# Workforce, attendance and payroll API

Conventions are in `docs/development/vertical-slice-guide.md`: camelCase JSON, enums as strings, dates as `"2026-10-05"`, money as `long` minor
units in properties ending `Minor` (the currency is the organization's `currencyCode`), `version` on every record you edit and on every update,
status change or void request, `PagedResult<T>` lists with `page` and `pageSize` (at most 100). Errors are RFC 9457 problem details: 422
`validation_failed` with `errors` keyed by field, 409 `rule_violation` or `concurrency_conflict`, 404 `not_found`, 403 `forbidden`.

## Who sees pay

Pay is confidential. A worker's rate and pay history are returned only to callers who hold `payroll.read` (Administrator, HR Officer,
Accountant). For everyone else `currentRateMinor` and `currentRateUnit` are `null` and `rates` is `[]`; a project manager or viewer still
sees who the worker is, their designation, where they are posted, their status and their cases. Attendance never carries pay. No audit line,
log line or error message carries a pay figure.

## Endpoints

| Method and route | Permission | Notes |
| --- | --- | --- |
| `GET /api/workers?search=&status=&projectId=` | workforce.read | `search` matches name or employee number. `projectId` is the current posting. Returns `WorkerListItemDto`. |
| `GET /api/workers/lookup?search=&includeLeft=` | workforce.read | Picker rows (`WorkerLookupDto`), at most 500. Workers who left (Resigned, Retired, Terminated) only with `includeLeft=true`. |
| `GET /api/workers/{id}` | workforce.read | `WorkerDetailDto`: profile, current posting, full posting history, pay history (if allowed), cases. |
| `POST /api/workers` | workforce.manage | Hire. 201. `clientRequestId` makes a repeated submission return the same worker. |
| `PUT /api/workers/{id}` | workforce.manage | Profile (name, birth date, contacts, addresses) and `restDay`: a day of the week, `"None"`, or left out to keep it. The hire request takes `restDay` too (default none). `WorkerDetailDto.restDay` is the day or `null`. |
| `POST /api/workers/{id}/change-rate` | workforce.manage | `category` Promotion, Demotion or RateAdjustment; adds a pay-history line, never edits one. |
| `POST /api/workers/{id}/assign` | workforce.manage | Post to a project (and optional sub-project) from a date. Ends the previous posting on that date. |
| `POST /api/workers/assign` | workforce.manage | Crew: `workerIds`, `projectId`, `subProjectId`, `startDate`. All or nothing; a refusal is 422 with `errors.workers`, one message per worker who couldn't be posted. |
| `POST /api/workers/{id}/release` | workforce.manage | End the current posting with no new one. |
| `POST /api/workers/{id}/status` | workforce.manage | Active, OnLeave, Suspended, Resigned, Retired, Terminated. Suspending or terminating needs a reason. Leaving ends the posting. |
| `POST /api/workers/{id}/reinstate` | workforce.manage | Bring back a worker who left. Reason required. |
| `POST /api/workers/{id}/cases` | workforce.manage | Add an incident note (never edited). |
| `GET /api/attendance?workerId=&projectId=&from=&to=&includeVoided=` | workforce.read | Newest day first. Each entry carries `nightHours` and `dayType` (Ordinary, RestDay, Special, SpecialRestDay, RegularHoliday, RegularHolidayRestDay, decided from the calendar and the worker's rest day as they are now). |
| `GET /api/projects/{projectId}/attendance?date=` | workforce.read | `AttendanceSheetDto`: the day's entries, totals, `crewWithoutEntry` (workers posted to the project that day with no entry yet), and `holidayName`/`holidayType` of the company holiday on the day (or `null`). Today when no date. |
| `POST /api/attendance` | attendance.write | One entry (`nightHours` optional, 0 up to the hours worked). 201. `clientRequestId` for double clicks. Refused (422 on `workDate`) on a day a standing payroll already paid the worker as an unworked regular holiday. |
| `POST /api/attendance/batch` | attendance.write | A crew's day: `projectId`, `subProjectId`, `workDate`, `entries[] {workerId, regularHours, overtimeHours, nightHours, remarks}`, `clientRequestId` (at most 40 characters). All or nothing. Field errors are keyed `entries[3].regularHours`. |
| `PUT /api/attendance/{id}` | attendance.write | Revise hours (including `nightHours`), sub-project, remarks. |
| `POST /api/attendance/{id}/void` | attendance.write | Reason required. |
| `GET /api/payroll-runs?status=` | payroll.read | Newest first, with totals, `kind` (Regular or ThirteenthMonth) and `year`. |
| `GET /api/payroll-runs/{id}` | payroll.read | `PayrollRunDto`: header (`kind`, `year`, `holidaysMissingForYear`), totals, every line with deductions and project shares. A line's gross is `regularPayMinor + premiumPayMinor + overtimePayMinor + nightPayMinor + holidayPayMinor + thirteenthMonthPayMinor`; it also carries `nightHours`, `holidayDates` and `thirteenthMonthBasisMinor`. |
| `GET /api/payroll-runs/{id}/export` | payroll.read | Register as CSV (UTF-8 with BOM, amounts like `1050.00`), with night hours, premium, night, holiday and 13th-month columns before the gross. |
| `POST /api/payroll-runs` | payroll.run | `{periodStart, periodEnd, clientRequestId}`. Calculates a Draft from recorded, unpaid attendance in the period (and unworked regular holidays) and locks that attendance. |
| `POST /api/payroll-runs/thirteenth-month` | payroll.run | `{year, clientRequestId}`. A 13th-month Draft for the year (not a future one): a twelfth of each worker's basic pay from the year's approved payrolls, for workers not already in a standing 13th-month payroll for the year. 201. No contributions or tax. |
| `PUT /api/payroll-runs/{id}/workers/{workerId}/deductions` | payroll.run | Replaces that worker's deductions `[{description, amountMinor}]` with the run's `version`. Draft only. |
| `POST /api/payroll-runs/{id}/recalculate-statutory` | payroll.run | `{version}`. Works a draft out again: the pay from the current rules, holidays, rest days, postings and company settings, then contributions and tax. Typed deductions are kept (refused when they would pass the new pay). A 13th-month draft is worked out again from its year. |
| `POST /api/payroll-runs/{id}/approve` | payroll.approve | Approves and posts one labor cost per project and sub-project. Refused (409) when the draft no longer matches a fresh calculation; recalculate first. |
| `GET /api/payroll-rules` | payroll.read | `PayRulesDto`: the factors in basis points, the company `overtimeMultiplierBps` (read only), `isDefault`, `version` (0 while the defaults apply). |
| `PUT /api/payroll-rules` | payrollrules.manage | The seven factors and `version`. Day factors 10,000 to 50,000; the overtime extra and night differential 0 to 10,000. |
| `GET /api/payroll-holidays?year=` | payroll.read | `HolidayCalendarDto`: the year's holidays (this year when left out), each with `coveredByApprovedPayroll`. |
| `POST /api/payroll-holidays` | payrollrules.manage | `{date, name, type}` (Regular or Special). 201. One per date; refused on a date an approved payroll covers. |
| `PUT /api/payroll-holidays/{id}` | payrollrules.manage | `{name, type, version}`. Changing the type is refused while an approved payroll covers the date; renaming is allowed. |
| `POST /api/payroll-holidays/{id}/remove` | payrollrules.manage | `{version}`. 204. Refused while an approved payroll covers the date. |
| `POST /api/payroll-runs/{id}/void` | payroll.run, plus payroll.approve for an approved payroll | `{reason, version}`. Releases the attendance; voids the posted labor cost with the same reason. |

## Payroll rules

* Each attendance entry is paid at the rate in force on its work date. A daily rate is divided by the company's paid hours per day; overtime is
  paid at the company's overtime multiple (125% by default). 800.00 a day, 8 h regular and 2 h overtime is 800.00 + 250.00 = 1,050.00.
* Each figure is rounded once, per entry, half away from zero. A worker's pay and each project's share are sums of those figures, so they
  always add up. The labor cost posted on approval equals the run's gross pay to the centavo (checked before saving).
* Rest days, special days and regular holidays pay the day factor of the pay rules on the regular hours and on overtime (times the overtime
  extra); night hours earn the night differential; a daily-rated worker posted on a regular holiday without attendance is paid one day. The
  full rules are in `docs/business-rules.md` (premium pay and 13th-month pay).
* SSS, PhilHealth, Pag-IBIG and tax come from the contribution tables (`docs/api/statutory.md`); without a table a person enters them as
  deductions. Deductions can't exceed the worker's gross pay.
* Attendance in a run is locked: it can't be edited or removed until the run is voided. A voided run frees it for a new run.
* Approval and void use the run's `version`. Two simultaneous approvals produce one approval and one set of cost entries; the other request is 409.
  Approval fails (and saves nothing) when one of the run's projects has since been archived; the message names the project.
* The cost entry for each project and sub-project is origin Payroll, labor, quantity 1, unit `l.s`, VAT exempt, dated the period's last day.

## Attendance rules

* A worker's hours across all projects on one day (regular plus overtime) are at most 24.
* One entry per worker, project, sub-project and day. A removed entry steps aside so the hours can be entered again.
* A project that is archived takes no attendance. A worker can be recorded from the hire date, and for a worker who left, up to the day they left.

## Audit

Actions: `Worker.Hired`, `Worker.ProfileUpdated`, `Worker.RateChanged`, `Worker.Assigned`, `Worker.Released`, `Worker.StatusChanged`,
`Worker.Reinstated`, `Worker.CaseRecorded`, `Attendance.Recorded`, `Attendance.Revised`, `Attendance.Voided`, `Payroll.Drafted`,
`Payroll.DeductionsChanged`, `Payroll.StatutoryRecalculated`, `Payroll.Recalculated`, `Payroll.Approved`, `Payroll.Voided`, `Worker.RestDayChanged`,
`PayRules.Changed`, `PayRules.HolidayAdded`, `PayRules.HolidayChanged`, `PayRules.HolidayRemoved`. A rate change reads "Changed pay rate for Juan Dela Cruz (promotion from Mason
to Foreman) effective 10 Oct 2026." and free-text reasons that might hold an amount are kept out of audit lines.
