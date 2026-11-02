# Purchasing API

Purchase orders to suppliers, the match of a supplier invoice to an order line, and the committed cost that open orders add to a
project's forecast. The firms themselves are the Suppliers API (`/api/suppliers`, `suppliers.read` and `suppliers.manage`). All routes are
under `/api`, return JSON in camelCase and send `Cache-Control: private, no-store`. Money is `long` minor units in properties ending
`Minor` (`125050` is 1,250.50). Quantities are `decimal` with at most 4 decimals. Dates are `"2026-11-02"`. Writes need the anti-forgery
header, like every other write.

## Endpoints

| Method and route | Permission |
| --- | --- |
| `GET /api/purchase-orders?projectId&supplierId&status&search&page&pageSize` | `purchasing.read` |
| `GET /api/purchase-orders/{id}` | `purchasing.read` |
| `POST /api/purchase-orders` | `purchasing.manage` |
| `PUT /api/purchase-orders/{id}` | `purchasing.manage` |
| `POST /api/purchase-orders/{id}/approve` | `purchasing.approve` |
| `POST /api/purchase-orders/{id}/cancel` | `purchasing.approve` |
| `POST /api/purchase-orders/{id}/close` | `purchasing.approve` |
| `POST /api/purchase-orders/{id}/receipts` | `purchasing.receive` |
| `POST /api/purchase-orders/{id}/receipts/{receiptId}/void` | `purchasing.receive` |
| `GET /api/projects/{projectId}/purchase-order-lines` | `purchasing.read` |

Roles: Administrator, Cost Engineer and Accountant draft and edit (`purchasing.manage`); Administrator, Project Manager and Accountant
approve, cancel and close (`purchasing.approve`); Administrator, Project Manager, Cost Engineer and Accountant record and void deliveries
(`purchasing.receive`); Administrator, Project Manager, Cost Engineer, Accountant and Viewer read. HR Officer and
Warehouse Keeper have no access to these routes: the Warehouse Keeper records and voids deliveries through the quantity-only
[Receiving API](receiving.md) instead, which shares the same rules and carries no prices. An order, project or firm of another company is `404`, never an empty list. Matching an invoice to an
order line uses the existing cost entry routes (`costs.write`); the optional `purchaseOrderLineId` is described below.

## The order

`PurchaseOrderDto`: `id`, `number` ("PO-0001"), `status` (`Draft`, `Approved`, `Closed`, `Cancelled`), `supplierId`, `supplierName`,
`projectId`, `projectName`, `projectArchived`, `subProjectId?`, `subProjectName?`, `orderDate`, `expectedDate?`, `supplierReference?`,
`notes?`, `netMinor`, `vatMinor`, `grossMinor` (the sum of the lines), `invoicedNetMinor`, `invoicedGrossMinor`, `committedNetMinor`,
`committedGrossMinor` (what an **approved** order has not been invoiced for; zero for any other status), `lines`, `invoices`,
`invoiceCount`, who and when for each step (`createdByName`, `approvedByName`, `closedByName`, `cancelledByName` with their `...AtUtc`),
`closeReason?`, `cancelReason?`, `version`, and four flags that tell the screen which steps the order's state allows right now:
`canEdit`, `canApprove`, `canCancel`, `canClose`, `canReceive` (a flag is false on an archived project; `canCancel` is also false once an
invoice is matched or a delivery stands; `canReceive` is true only for an approved order with something left to receive). The flags say
nothing about the person's permission: the endpoint still refuses a person who lacks it. Delivery fields are described under
[Deliveries](#deliveries): `deliveryState`, `receipts`, `receiptCount`.

Each line: `id`, `lineNumber`, the classification (`division?`, `mainScope?`, `subScope?`, `wbsCode?`), `description`, `itemType`,
`quantity`, `unit`, `unitCostMinor`, `vatTreatment`, `vatRateBps` (the company rate when the line was entered), `netMinor`, `vatMinor`,
`grossMinor`, and from the matched invoices `invoicedNetMinor`, `invoicedGrossMinor`, `remainingNetMinor`, `remainingGrossMinor`
(never below zero) and `overByGrossMinor` (how far the invoices exceed the line; zero unless they do).

`invoices` lists the newest posted supplier invoices matched to any line (at most 100; `invoiceCount` is the true number):
`costEntryId`, `purchaseOrderLineId`, `lineNumber`, `entryDate`, `sourceDealer`, `invoiceNumber`, `description`, `netMinor`, `grossMinor`.

## List

Returns `{ items, total, page, pageSize }`, newest order date first. An item has `id`, `number`, `supplierId`, `supplierName`, `projectId`,
`projectName`, `orderDate`, `expectedDate?`, `status`, `lineCount`, `netMinor`, `vatMinor`, `grossMinor`, `deliveryState?` (for an
**approved** order only: `NotReceived`, `PartlyReceived` or `FullyReceived`; null for every other status). Filters: `projectId`,
`supplierId`, `status` (a name, any letter case), `search` (the number, the supplier's reference or the firm's name; `%` and `_` are
literal). An unknown `status` is a `422` field error. Page size is at most 100.

## Writing

Create takes `projectId`, `subProjectId?`, `supplierId`, `orderDate`, `expectedDate?`, `supplierReference?` (80), `notes?` (500),
`lines` (1 to 100) and `clientRequestId?`. Update takes the same without `clientRequestId` plus `version`, and **replaces** the whole
draft, lines included. A line takes `division?`, `mainScope?`, `subScope?`, `wbsCode?`, `description` (200), `itemType`, `quantity`, `unit`,
`unitCostMinor`, `vatTreatment`. The VAT rate is not a field: the company's rate at the moment of entry is stored on the line. Net, VAT and
gross are worked out once, by the same line rules budget lines and cost entries use, and `net + vat == gross` on every line and on the order.

* **Numbering.** The number is the company's next `purchase-order` sequence value, taken only after the order passed every check, so a
  form with mistakes never uses one up. Numbers are unique and rising per company; a failed save after the number was taken leaves a gap.
* **Validation (`422`, every problem at once).** `orderDate` may not be in the future (the company's business day) and `expectedDate` may
  not precede it; `lines` is empty, has more than 100, or has a problem. Each problem with a line is listed under `lines`, starting with
  its number ("Line 2: Enter the quantity greater than zero."). `supplierId` or `projectId` missing is a field error.
* **Rules (`409 rule_violation`).** The firm must be an active supplier-kind firm; the project (and sub-project) must be able to receive
  records. An archived project refuses a new order and every step on an existing one ("Restore it first").
* **Approve** (`{ version }`) locks the lines. It re-checks the firm and the project. **Cancel** (`{ reason, version }`, reason required)
  takes a draft, or an approved order with no posted invoice matched to it; an approved order with a matched invoice is `409` ("Close it
  instead"). **Close** (`{ reason?, version }`) takes an approved order whose supplier delivered less: what was not invoiced is released.
  A closed or cancelled order is final. Editing an approved order is `409`.
* A stale `version` is `409 concurrency_conflict`. A repeated `clientRequestId` returns the original order, including when two copies
  arrive at the same moment; reusing it for another project or supplier is `409`.

## Matching an invoice

`POST /api/projects/{projectId}/cost-entries` and `PUT /api/cost-entries/{id}` take an optional `purchaseOrderLineId`. The line must exist
in the company, belong to an order of the same project, and that order must be **approved**; otherwise `422` on `purchaseOrderLineId`
(the message does not say whether another company holds such a line). An invoice larger than the line is allowed: the line shows as
over by the excess and its remainder is zero. On update the match is replaced by what is sent (null or absent clears it); a match the
entry already has is kept without checking the order's status again, so correcting an invoice whose order has since closed still works.
`CostEntryDto` carries `purchaseOrderLineId?` and `purchaseOrderNumber?`.

`GET /api/projects/{projectId}/purchase-order-lines` lists the lines of the project's approved orders for the "against a purchase order"
choice: `lineId`, `orderId`, `orderNumber`, `lineNumber`, `supplierName`, `description`, `grossMinor`, `remainingNetMinor`,
`remainingGrossMinor` (at most 500, by order number and line).

## Deliveries

A delivery receipt (`GoodsReceipt`) says what a supplier actually brought against an **approved** order: the day, the supplier's own
delivery receipt number and a quantity per order line. It is append-only: a mistake is voided with a reason and entered again. **A
delivery changes no money**: committed cost still falls only when an invoice is matched to the line, and stock is not updated.

`POST /api/purchase-orders/{id}/receipts` takes `receivedDate` (not in the future, company business day), `deliveryReceiptNumber?` (60),
`note?` (300), `lines` (`[{ purchaseOrderLineId, quantity }]`, a quantity greater than zero with at most 4 decimals, no line twice, leave
out a line nothing arrived for) and `clientRequestId?`. It answers `201` with the **order** as it now stands, so a page replaces what it
holds in one round trip. `POST .../receipts/{receiptId}/void` takes `{ reason, version }` (the delivery's `version`) and answers `200`
with the order. A delivery of another order or company is `404`.

* **Refusals.** `409 rule_violation`: the order is a draft ("... is not approved yet. Approve it before recording a delivery."), closed
  ("This order is closed.") or cancelled ("This order was cancelled."); the project is archived ("Restore it ..."), for recording and for
  voiding. `422` per field, every problem together: `receivedDate`, `deliveryReceiptNumber`, `note`, and `lines` (one message per line,
  naming it: "Line 1 (Rebar 16 mm): only 6 pc are left to receive. Enter 6 or less."; "... has all been received already."; a line of
  another order; a line listed twice; none entered). **Over-receipt is refused**: a line takes at most ordered minus what posted
  deliveries already name. **Duplicate protection**: the same delivery receipt number of the same supplier (compared by letters and digits
  only, so "DR-0045" and "dr 0045" are one) on a delivery that stands is `422` on `deliveryReceiptNumber`: "This delivery receipt is
  already recorded." A voided delivery frees its number. No number given: nothing to compare.
* **Concurrency.** Recording touches the order, so two deliveries of the last quantity at the same moment (or a delivery and a close or
  cancel) collide on the order's version: one is saved, the other is `409 concurrency_conflict` or `422` on `lines` (it found the
  quantity already received). A stale `version` on a void is `409`. A repeated `clientRequestId` returns the original; reusing it for
  another order is `409`.
* **Cancel.** An approved order with a delivery that stands cannot be cancelled (`409`: "Close it instead ..."); voiding the delivery
  makes it possible again. Close is always possible. A delivery can still be voided after the order closed.
* **Audit.** `GoodsReceipt.Recorded` and `GoodsReceipt.Voided` name the order and project; the note, the number and the reason stay on
  the delivery.

On the order read: `deliveryState` (`NotReceived`, `PartlyReceived`, `FullyReceived`), `receipts` (the newest 100, voided ones too,
newest day first) and `receiptCount` (the true number). A receipt: `id`, `receivedDate`, `deliveryReceiptNumber?`, `note?`, `status`
(`Posted` or `Void`), `recordedByName`, `recordedAtUtc`, `voidedByName?`, `voidedAtUtc?`, `voidReason?`, `canVoid`, `version`, `lines`
(`id`, `purchaseOrderLineId`, `lineNumber`, `description`, `unit`, `quantity`). Each order line adds `receivedQuantity`,
`toReceiveQuantity` (ordered minus received, never below zero), `receivedValueNetMinor` and `receivedValueGrossMinor` (the line's unit
cost times the received quantity, rounded once, on the line's own VAT treatment and rate: receiving the whole line is worth exactly the
line) and `invoicedAheadOfDelivery` (advisory: `invoicedGrossMinor` is more than `receivedValueGrossMinor`; the invoice stays valid).

## Committed cost

`PurchaseOrderCommitmentSource` reports, per project, the open remainder of **approved** orders: for each line,
`max(0, line amount - posted matched invoices)` on net and on gross separately, summed per project in one grouped statement. A voided
invoice stops counting as matched, so its remainder returns. A draft, closed or cancelled order commits nothing. The dashboard row, the
dashboard total and the project cost summary add it to the actual cost: `commitments { committedMinor, forecastMinor,
forecastRemainingMinor, forecastHealth }` on the comparison basis the company chose. `commitments` is absent when nothing is open.

Statement counts (checked by tests, constant however many rows exist): the list is 2 (its page carries each order's delivery state), one
order is 7 (before deliveries it was 4: the order, its lines, what each line is invoiced, the newest invoices, what each line has
received, the newest deliveries and their lines), the choice list is 3. The dashboard
and the project cost summary each gain one grouped statement for every commitment source.
