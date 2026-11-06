# Bonds API

The bonds register: performance, advance payment, bid and warranty bonds the company posted to clients or received from subcontractors and
suppliers, each with the date it is valid until. A bond is tracked so that nobody lets one lapse unnoticed. **It is not a cost and changes
no budget, cost, billing or payment figure anywhere.** All routes are under `/api`, return JSON in camelCase and send
`Cache-Control: private, no-store`. Money is `long` minor units in properties ending `Minor`. Dates are `"2026-11-02"`. Writes need the
anti-forgery header, like every other write.

## Endpoints

| Method and route | Permission |
| --- | --- |
| `GET /api/bonds?projectId&direction&kind&status&expiry&page&pageSize` | `bonds.read` |
| `GET /api/bonds/totals` | `bonds.read` |
| `GET /api/bonds/{id}` | `bonds.read` |
| `GET /api/projects/{projectId}/bonds` | `bonds.read` |
| `POST /api/bonds` | `bonds.manage` |
| `PUT /api/bonds/{id}` | `bonds.manage` |
| `POST /api/bonds/{id}/extend` | `bonds.manage` |
| `POST /api/bonds/{id}/release` | `bonds.manage` |
| `POST /api/bonds/{id}/call` | `bonds.manage` |
| `POST /api/bonds/{id}/void` | `bonds.manage` |

Roles: Administrator, Project Manager and Accountant manage (`bonds.manage`); Administrator, Project Manager, Cost Engineer, Accountant and
Viewer read (`bonds.read`). HR Officer and Warehouse Keeper have no access. A bond or project of another company is `404`, never an empty
list. A change on an archived project is refused (restore the project first). A stale `version` is `409`.

## The bond

`direction` is `Posted` (the company gave it to the client) or `Received` (a subcontractor or supplier gave it to the company). `kind` is
`Bid`, `Performance`, `AdvancePayment`, `Warranty` or `Other`. `status` is `Active`, `Released`, `Called` or `Void`. For an Active bond
`expiry` is `Valid`, `ExpiringSoon` (30 days or fewer left, the last valid day counts) or `Expired`, and `daysLeft` counts from today in
the company's time zone. Both are worked out when read and never stored; an expired bond stays Active until a person releases or voids it.

`GET /api/bonds/{id}` also returns `history`: the latest 100 steps, newest first (created, edited, extended with the old and new date,
released, called, voided), each with who and when.

## Writes

* `POST /api/bonds` takes `projectId`, `direction`, `kind`, `issuerName`, `bondNumber`, `amountMinor` (greater than zero), `issuedOn`,
  `validUntil` (not before `issuedOn`), `otherPartyName` (a received bond only), `note` and an optional `clientRequestId`; a repeated
  request id returns the first bond. Problems with what was typed come back together as field errors.
* `PUT` changes the details of an Active bond (the project and direction are fixed once recorded). Saving unchanged details records
  nothing. Moving the date later is recorded like an extension.
* `extend` takes a later `validUntil`, an optional `note` and `version`. `release` and `call` take a date (not in the future, not before
  the issue date), a note (required to call) and `version`. `void` takes a `reason` and `version`.

## Reads

* The list is paged and opens on Active bonds, soonest to expire first. `GET /api/bonds/totals` returns the Active amount and count by
  direction and how many Active bonds are expired or expiring soon. `GET /api/projects/{projectId}/bonds` returns the project's bonds
  that are not void, Active first, at most 50.
* Every read is a fixed number of SQL statements however many bonds exist: list 3 (4 with a project filter), one bond 3, project list 3,
  totals 2.
