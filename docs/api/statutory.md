# Statutory deductions API

The contribution and tax tables (SSS, PhilHealth, Pag-IBIG, withholding tax), the capability check that says which agencies payroll
works out, and the payroll endpoints that show, adjust and recalculate the calculated amounts. All routes are under `/api`, return JSON in
camelCase and send `Cache-Control: private, no-store`. Money is `long` minor units in properties ending `Minor`; rates are basis points in
properties ending `Bps` (`1,000` is 10%). Dates are `"2026-11-02"`. Writes need the anti-forgery header.

**No legal rate is built in.** Every figure is data a person enters from the official schedule. The examples below use artificial figures.

## Endpoints

| Method and route | Permission |
| --- | --- |
| `GET /api/statutory/status` | `payroll.read` |
| `GET /api/statutory/schedules?agency=&includeRetired=false&page=&pageSize=` | `payroll.read` |
| `GET /api/statutory/schedules/{id}` | `payroll.read` |
| `POST /api/statutory/schedules` | `statutory.manage` |
| `PUT /api/statutory/schedules/{id}` | `statutory.manage` |
| `POST /api/statutory/schedules/{id}/retire` | `statutory.manage` |
| `GET /api/payroll-runs/{id}/statutory` | `payroll.read` |
| `POST /api/payroll-runs/{id}/workers/{workerId}/statutory/{agency}/adjust` | `payroll.run` |
| `POST /api/payroll-runs/{id}/recalculate-statutory` | `payroll.run` |
| `PUT /api/workers/{id}/statutory` | `workforce.manage` (and the service also requires `payroll.read`) |

Roles: Administrator and Accountant read and change the tables; HR Officer reads them (through `payroll.read`) but gets `403` on every
change. Project Manager, Cost Engineer, Viewer and Warehouse Keeper get `403` on every route here. A table, payroll or worker of another
company is `404`.

## Capability check

`GET /api/statutory/status` answers, per agency in the order deductions are taken, whether a table is in force **today** (company time
zone):

```json
{
  "today": "2026-11-01",
  "agencies": [
    { "agency": "Sss", "label": "SSS", "state": "Available", "message": "SSS contributions are worked out from the table in force since 1 Jan 2026.",
      "fallback": null, "scheduleId": "…", "scheduleName": "…", "effectiveFrom": "2026-01-01" },
    { "agency": "PagIbig", "label": "Pag-IBIG", "state": "NeedsSetup",
      "message": "Pag-IBIG contributions were not calculated because no Pag-IBIG table is set up.",
      "fallback": "Enter them as deductions, or ask an accountant to set the table up.", "scheduleId": null, "scheduleName": null, "effectiveFrom": null }
  ]
}
```

It never fails because a table is missing: `NeedsSetup` is a normal answer and payroll keeps working with manual deductions.

## Tables

`agency` is `Sss`, `PhilHealth`, `PagIbig` or `WithholdingTax`; `method` is `Brackets`, `Percentage` or `TaxBrackets` (withholding tax uses
`TaxBrackets`, contributions use the other two); `periodBasis` is `Monthly` or `PerRun`; `status` is `Active` or `Retired`.

Save request (create and update; `agency` and `clientRequestId` are read only when creating):

```json
{
  "agency": "Sss", "name": "Artificial test brackets", "effectiveFrom": "2026-01-01", "method": "Brackets", "periodBasis": "Monthly",
  "employeeRateBps": null, "employerRateBps": null, "baseFloorMinor": null, "baseCeilingMinor": null,
  "employeeMinimumMinor": null, "employeeMaximumMinor": null, "employerMinimumMinor": null, "employerMaximumMinor": null,
  "rows": [
    { "fromMinor": 0, "toMinor": 100000, "employeeMinor": 1111, "employerMinor": 2222, "baseTaxMinor": 0, "rateBps": 0 },
    { "fromMinor": 100000, "toMinor": null, "employeeMinor": 3333, "employerMinor": 4445, "baseTaxMinor": 0, "rateBps": 0 }
  ],
  "clientRequestId": "…", "version": 0
}
```

* A row covers amounts from `fromMinor` up to, but not including, `toMinor`. Rows start at 0, each starts where the one before ends, only
  the last has `toMinor: null`; 1 to 100 rows. `Brackets` rows use `employeeMinor`/`employerMinor`; `TaxBrackets` rows use `baseTaxMinor`
  and `rateBps` (on the excess over `fromMinor`). Unused fields are stored as 0.
* A `Percentage` table uses `employeeRateBps`, `employerRateBps` (0 to 10,000), `baseFloorMinor` (default 0), optional `baseCeilingMinor`
  (not below the floor) and optional minimum and maximum of each share (minimum not above maximum). It has no rows.
* The response (`StatutoryScheduleDto`) adds `agencyLabel`, `status`, `inForce` (the table that applies today), `usedByApprovedPayroll`,
  `retiredAtUtc` and `version`. The list returns `{ items, total, page, pageSize }` with `rowCount` and `inForce`, active tables only unless
  `includeRetired=true`, ordered by agency then newest effective date.

Errors:

| Status | When |
| --- | --- |
| `422` | Missing name, date, method or basis; method not allowed for the agency; any row rule (`errors.rows`, one message per problem, each naming its row); rate above 100%; ceiling below floor; minimum above maximum; another **active** table of the agency already takes effect on that date (`errors.effectiveFrom`). |
| `409 rule_violation` | Changing a table an approved payroll used ("add a new table with the date the change takes effect"); changing or retiring a retired table; a `clientRequestId` reused for another agency or date. |
| `409 concurrency_conflict` | Stale `version`. |
| `404` | No such table in this company. |

A repeated create with the same `clientRequestId` returns the original table.

## Payroll

`PayrollRunDto` (every payroll response) carries `runsPerMonth` and `totalEmployerSharesMinor`; each line carries `employerSharesMinor` and
`statutoryLimited`; each deduction carries:

| Field | Meaning |
| --- | --- |
| `source` | `Manual` (typed) or `Statutory` (calculated) |
| `agency` | The agency of a statutory deduction, else null |
| `amountMinor` | What is taken from pay |
| `calculatedMinor` | What the table gave (null for manual) |
| `employerShareMinor` | What the employer adds (0 for manual and tax); never taken from pay, never posted to a project |
| `isAdjusted`, `adjustmentReason` | A person changed the amount, and why |
| `isLimited` | Cut because the deductions would have been more than the pay |

* `POST /api/payroll-runs` (unchanged contract) now also calculates the statutory deductions of a draft from the tables in force at the
  period's end.
* `PUT /api/payroll-runs/{id}/workers/{workerId}/deductions` replaces only the **manual** deductions; the calculated ones stay, and all
  deductions together still can't exceed gross pay (`422`, `errors.deductions`).
* `POST …/workers/{workerId}/statutory/{agency}/adjust` with `{ "amountMinor": 2600, "reason": "Agency statement", "version": 4 }`:
  `422` for a negative amount, no reason (300 characters at most) or deductions above the pay (`errors.amountMinor`); `409` when the agency
  was not calculated for the worker or the payroll is not a draft; `404` for an unknown agency, worker or payroll.
* `POST …/recalculate-statutory` with `{ "version": 4 }`: works every worker's amounts out again (adjustments reset, manual deductions
  kept, current tables, worker flags and runs per month); `409` when the payroll is not a draft.
* `GET /api/payroll-runs/{id}/statutory` (the remittance summary):

```json
{
  "runId": "…", "status": "Draft", "runsPerMonth": 2, "totalEmployeeMinor": 8738, "totalEmployerMinor": 8271, "totalToRemitMinor": 17009,
  "agencies": [
    { "agency": "Sss", "label": "SSS", "scheduleNames": ["…"], "workerCount": 1, "employeeMinor": 1667, "employerMinor": 2223,
      "toRemitMinor": 3890, "adjustedCount": 0, "limitedCount": 0 }
  ],
  "workers": [
    { "workerId": "…", "workerName": "Juan Dela Cruz", "employeeNumber": "W-001",
      "shares": [ { "agency": "Sss", "employeeMinor": 1667, "employerMinor": 2223, "calculatedMinor": 1667, "isAdjusted": false, "isLimited": false } ] }
  ]
}
```

* The payroll register CSV (`GET /api/payroll-runs/{id}/export`) gains, after `Deduction details`: `SSS employee`, `SSS employer`,
  `PhilHealth employee`, `PhilHealth employer`, `Pag-IBIG employee`, `Pag-IBIG employer`, `Withholding tax`, `Employer contributions`
  (plain two-decimal numbers; text cells keep the formula guard).
* Approving is unchanged: the labor cost posted to the projects is the gross pay.

## Workers and company settings

* `WorkerDetailDto.statutory` is `{ sssMember, philHealthMember, pagIbigMember, taxExempt }` for people with `payroll.read`, `null` for
  everyone else. `POST /api/workers` accepts the four as optional booleans (default: member of all three, not exempt).
  `PUT /api/workers/{id}/statutory` with the four and `version` changes them.
* `GET/PUT /api/organization` carry `payrollRunsPerMonth` (1 to 4, default 2; `422 errors.payrollRunsPerMonth` outside that range). Leaving
  it out of the `PUT` keeps the current value.
