# Needs your action API

What is waiting on the signed-in person, read from statuses that already exist (see [Business rules, section 16](../business-rules.md#16-needs-your-action)).
Nothing is stored or sent. Both routes are reads under `/api`, return JSON in camelCase and send `Cache-Control: private, no-store`.

## Endpoints

| Method and route | Permission |
| --- | --- |
| `GET /api/needs-action` | `actions.read` |
| `GET /api/needs-action/count` | `actions.read` |

`actions.read` is held by every role. **Which groups a caller receives is decided on the server from the caller's own roles**; no query
parameter can widen or narrow it. A group the caller cannot act on is **absent** from the answer; a group they can act on is present
even when it holds nothing (`count: 0`, `rows: []`).

## The queue

`GET /api/needs-action` answers `{ groups }`. Groups with an urgent row come first, then this order:

| `key` | Present for whoever holds | `amountMinor` |
| --- | --- | --- |
| `purchase-orders-to-approve` | `purchasing.approve` | order total with VAT |
| `change-orders-to-approve` | `changeorders.approve` | budget change with VAT (signed) |
| `bills-to-issue` | `billing.approve` | net receivable |
| `claims-to-certify` | `subcontracts.approve` | claim gross |
| `payroll-to-approve` | `payroll.approve` | never |
| `bills-to-chase` | `billing.manage` | outstanding amount |
| `deliveries-expected` | `purchasing.receive` or `receiving.record` | never |
| `bonds-to-attend` | `bonds.manage` | bond amount |
| `progress-not-updated` | `progress.write` | never |
| `attendance-not-recorded` | `attendance.write` | never |
| `statutory-needs-setup` | `statutory.manage` | never |

Notes on what each group counts (the full rules are in Business rules, section 16.2):

* `deliveries-expected`: only approved orders with something left to receive whose **expected date is today or earlier** (the row says "Due
  today" or "N days late, expected 28 Oct 2026"; only the late ones are urgent). Orders expected later, or with no date, are not counted. The
  receiving screen's own list (`GET /api/receiving/orders`) is unchanged and lists every open order.
* `progress-not-updated`: Active projects with at least one active budget line.
* `attendance-not-recorded`: an Active project with no recorded attendance entry for yesterday that did record some a week earlier, with at
  least one posted worker not on their rest day; the row says `"No attendance recorded for Sun 1 Nov"`. A day that was a company holiday
  lists nothing.
* `statutory-needs-setup`: counts 1 only while a table is missing **and** the company has an active worker or a draft payroll; otherwise
  the group is present with `count: 0` for a caller who may act on it.

A group: `key`, `count` (how many wait in all) and `rows` (at most the 8 most urgent). A row:

* `id`: the record the row opens. For `claims-to-certify` it is the claim and `parentId` is its subcontract (the claim is certified on the
  subcontract's page); for `progress-not-updated` and `attendance-not-recorded` it is the project; for `statutory-needs-setup` it is the
  empty id (the page is the company's tables).
* `parentId?`, `label` (the document number and who it concerns, in words: `"PO-0012, Visayas Steel"`), `projectId?`, `projectName?`,
  `detail` (why it waits, in words: `"Waiting 3 days"`, `"5 days overdue, due 28 Oct 2026"`, `"Expires in 12 days, on 14 Nov 2026"`),
  `sinceUtc?`, `dueDate?` (a due, expected or valid-until date), `isUrgent` (overdue, late or expired).
* `amountMinor` only for the groups that list one above; for the others the property is **left out of the JSON entirely**, so a
  Warehouse Keeper's answer holds no property named for money.

The server returns ids and group keys, never web routes: the client maps them to pages.

## The badge

`GET /api/needs-action/count` answers `{ total }`, the sum of the caller's group counts (always equal to the sum of `count` in the queue
above). It reads counts only, never a row. `total` is a number of waiting records, not an amount.

## Cost

A fixed number of SQL statements, however many records wait (asserted with 1 and with 10 records in every group):

* `GET /api/needs-action`: the organization (for today's date in its time zone), then for each record group a count and, when the count
  is not zero, its rows (deliveries are the receiving list's count and page; the statutory tables are one statement, plus one more while a table is missing). An administrator,
  with all eleven groups, costs **22** statements, **23** while a statutory table is missing (one more statement asks whether the company has
  an active worker or a draft payroll).
* `GET /api/needs-action/count`: the organization plus one count per group: **12** for an administrator, **13** while a table is missing.
* A caller with no group (a Viewer) costs **no** statement.
