# Billing API

Client billing: a project's billing terms and position, bills to the client (the down payment, progress bills and retention releases),
and the payments the client made against them. All routes are under `/api`, return JSON in camelCase, and send `Cache-Control: private, no-store`.
Money is `long` minor units in properties ending `Minor`; rates are basis points (`...Bps`, 1,000 is 10%); dates are `"2026-11-02"`.
Writes need the anti-forgery header. **Nothing here sends anything to a client or collects money**: recording a payment is a person
saying the money was received.

## Endpoints

| Method and route | Permission |
| --- | --- |
| `GET /api/projects/{projectId}/billing?page=&pageSize=` | `billing.read` |
| `PUT /api/projects/{projectId}/billing/terms` | `billing.manage` |
| `GET /api/projects/{projectId}/billing/preview?cumulativeWorkMinor=&billingDate=` | `billing.manage` |
| `POST /api/projects/{projectId}/billing/invoices` | `billing.manage` |
| `GET /api/billing/receivables?page=&pageSize=` | `billing.read` |
| `GET /api/client-invoices?projectId=&status=&overdue=&search=&page=&pageSize=` | `billing.read` |
| `GET /api/client-invoices/{id}` | `billing.read` |
| `PUT /api/client-invoices/{id}` (change a draft) | `billing.manage` |
| `POST /api/client-invoices/{id}/discard` (a draft) | `billing.manage` |
| `POST /api/client-invoices/{id}/issue` | `billing.approve` |
| `POST /api/client-invoices/{id}/void` (an issued bill) | `billing.approve` |
| `POST /api/client-invoices/{id}/payments` | `billing.manage` |
| `POST /api/client-invoices/{id}/payments/{paymentId}/void` | `billing.approve` |

Roles: Administrator and Accountant do everything; Project Manager prepares bills, saves terms and records payments but neither issues
nor voids; Cost Engineer and Viewer only read; HR Officer and Warehouse Keeper have no access. A project, bill or payment of another
company is `404`.

## Reads

**Project billing** returns `ProjectBillingDto`: `contractPriceMinor` (null without a price), `billedToDateMinor` (the work on issued
progress bills, VAT included), `percentBilledBps` and `remainingToBillMinor` (null without a price), `retentionWithheldMinor`,
`retentionReleasedMinor`, `retentionHeldMinor` (withheld less released), `collectedMinor` (payments not voided), `outstandingMinor`
(what issued bills still ask for), `overdueMinor` and `overdueCount` (issued bills past their due date with something outstanding),
`canReleaseRetention` (the project is Completed and retention is held), the down payment (`advanceBilledMinor`: the issued down
payment, `advanceRecoveredMinor`: what issued progress bills recovered of it, `advanceToRecoverMinor`: the rest, `advanceBillId`: the
down payment bill that is not void, draft or issued, and `advanceToBillMinor`: what a down payment bill would ask for now, null unless
the terms take one, none exists, no progress bill is issued and there is a contract price), `terms` (`retentionBps`,
`creditableWithholdingBps`, `paymentTermsDays`, `advanceBps`, `vatWithholdingBps`, `isGovernmentClient`, `isDefault` when never
saved, `version`: 0 while default), `vatRateBps` (the company's rate now) and `bills` (a page of the bills list below, for this project).

**Bills list** (`ClientInvoiceListItemDto`): `number` (null for a draft), `kind` (`Progress`, `Advance`, `RetentionRelease`), `status` (`Draft`,
`Issued`, `Void`), `billingDate`, `dueDate`, `description`, `grossMinor`, `netReceivableMinor`, `paidMinor`, `outstandingMinor`,
`isOverdue`, `version`. Filters: `projectId`, `status`, `overdue=true`, `search` (number or description). Order: drafts first, then
newest billing date.

**A bill** (`ClientInvoiceDto`) adds the project's name, status and current contract price, `cumulativeWorkMinor` (progress bills),
`previousCumulativeWorkMinor`, `netMinor`, `vatMinor`, `vatRateBps`, `retentionBps`, `retentionMinor`, `withholdingBps`,
`withholdingMinor`, `vatWithholdingBps` and `vatWithholdingMinor` (VAT the client withholds), `advanceBps` (a down payment bill's own
rate, or the issued down payment's rate on a progress bill; the recovery is proportional to the work, not at this rate),
`advanceRecoveryMinor` (what a progress bill recovers of the down payment), `paymentTermsDays`, `isLatestIssued` (only the latest issued
bill can be voided), `isStale` (a draft whose figures no longer follow from what is billed now, or for a down payment from the contract
price and terms now: save it again before issuing), `canOnlyBeDiscarded` (a down payment draft once a progress bill is issued: it can
only be discarded), who prepared, issued and voided it and when, `voidReason`, and its
`payments` (`amountMinor`, `paidOn`, `method`, `reference`, `note`, who recorded it, `isVoided`, `voidReason`; at most 100).

**Receivables** returns the company totals `outstandingMinor`, `overdueMinor`, `retentionHeldMinor` and `rows` (paged): one per project
with money outstanding or retention held (`billedToDateMinor`, `collectedMinor`, `outstandingMinor`, `overdueMinor`,
`retentionHeldMinor`), most overdue first.

**Preview** returns what a progress bill for `cumulativeWorkMinor` would be (`BillPreviewDto`: previous and new work to date, gross,
net, VAT, retention, withholding, VAT withheld, down payment recovered, net receivable, rates, terms days and due date). It runs the same checks as saving a draft and saves
nothing. `billingDate` defaults to today.

Statement counts (fixed however many bills and payments exist, asserted in `BillingApiTests`): bills list 3, project billing 4,
receivables 4, one bill 3 (4 for a draft, which also reads the project's ledger). Each includes reading the company's time zone.

## Writing

`PUT .../billing/terms`: `{ retentionBps (0 to 3,000), creditableWithholdingBps (0 to 1,000), paymentTermsDays (0 to 180), version,
advanceBps (0 to 5,000; at most 1,500 for a government client), vatWithholdingBps (0 to 1,200), isGovernmentClient }`. The last three
are optional: left out (or null), they keep what is saved (none, none and no when never saved). The first save creates the terms (send
`version: 0`). Returns `BillingTermsDto`.

`POST .../billing/invoices` creates a **draft**: `{ kind, billingDate, description (200), cumulativeWorkMinor, amountMinor,
clientRequestId }`. A `Progress` bill needs `cumulativeWorkMinor`, the VAT-included value of all work completed to date; a
`RetentionRelease` needs `amountMinor`; an `Advance` (the down payment) takes neither: it is the terms' `advanceBps` of the contract
price, with no VAT split and nothing retained or withheld. Each progress bill then deducts
`min(down payment, round(down payment x work to date / contract price now)) - recovered before`, which is exactly what is left on the
bill that reaches the contract price. Returns `201` with the bill. A repeated `clientRequestId` returns the original draft; reused for
another project or kind it is `409`.

`PUT /client-invoices/{id}`: `{ billingDate, description, cumulativeWorkMinor | amountMinor, version }` re-saves a draft with figures
worked out from what is billed now and the terms and VAT rate in force now.

`POST .../issue`: `{ version }`. Numbers the bill (`BILL-0001`, company sequence `client-invoice`) and locks it. `POST .../void` and
`.../discard`: `{ reason (300), version }`.

`POST .../payments`: `{ amountMinor, paidOn, method (BankTransfer, Cheque, Cash, Other), reference (80), note (300), clientRequestId,
version }` with the bill's version. Returns `201` with the bill. A repeated `clientRequestId` returns the bill without recording twice.
`POST .../payments/{paymentId}/void`: `{ reason, version }`.

## Errors

| Status | When |
| --- | --- |
| `422` | Field errors, all at once: `billingDate` (missing, in the future), `description`, `cumulativeWorkMinor` (not more than already billed, more than the contract price), `amountMinor` (release more than held, payment more than outstanding), `paidOn`, `method`, `reference`, `note`, `retentionBps`, `creditableWithholdingBps`, `paymentTermsDays`, `advanceBps` (out of range, or more than 15% for a government client), `vatWithholdingBps`, `kind`. |
| `409 rule_violation` | No contract price; the whole price already billed; a down payment with none in the terms, after a progress bill is issued ("Bill the down payment before the first progress bill."), or when one already exists; deductions more than the bill ("The deductions are more than this bill..."); issuing a progress draft made before the down payment was issued (the stale-draft message); the project is archived (every write); a release on a project not Completed or with no retention held; issuing a stale draft, including a down payment draft whose contract price or terms changed ("Another bill was issued or voided after this draft was prepared..."); issuing when the price dropped below the work billed; voiding a bill that is not the latest ("Void BILL-0005 first."); voiding a bill with payments; voiding a draft (discard it) or discarding an issued bill; a payment on a draft, a void bill or a fully paid bill; two issues or voids of the same project at the same moment; a request id reused for something else. |
| `409 concurrency_conflict` | The `version` sent is not the current one. Issuing a bill changes the bill before it (it now points at its successor), so that bill's version moves too. |
| `404` | No such project, bill or payment in this company. |
| `403` | The permission in the table above is missing. |
