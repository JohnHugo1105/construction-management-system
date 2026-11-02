# Receiving API

The quantity-only view of deliveries, for the people who receive goods on site (the Warehouse Keeper). It records and voids the same
deliveries as the purchasing routes (`POST /api/purchase-orders/{id}/receipts`, see [Purchasing](purchasing.md#deliveries)), with the
same request bodies, rules, refusals, idempotency and audit lines, but every answer is **quantities only**: no unit cost, VAT, line or
order total, invoiced or committed amount, invoice number or cost data, and no property whose name says so. The Warehouse Keeper holds
`receiving.record` and **no** purchasing permission, so every `/api/purchase-orders` route stays `403` for them. All routes are under
`/api`, return JSON in camelCase and send `Cache-Control: private, no-store`. Quantities are `decimal` with at most 4 decimals. Dates
are `"2026-11-02"`. Writes need the anti-forgery header.

## Endpoints

| Method and route | Permission |
| --- | --- |
| `GET /api/receiving/orders?projectId&search&page&pageSize` | `receiving.record` |
| `GET /api/receiving/orders/{id}` | `receiving.record` |
| `POST /api/receiving/orders/{id}/deliveries` | `receiving.record` |
| `POST /api/receiving/orders/{id}/deliveries/{receiptId}/void` | `receiving.record` |

`receiving.record` is held by Administrator and Warehouse Keeper. Project Manager, Cost Engineer, Accountant, HR Officer and Viewer
get `403` (they use the purchasing routes where they hold `purchasing.receive`). An order of another company is `404`.

## Orders awaiting delivery

`GET /api/receiving/orders` answers `{ items, orderCount, page, pageSize }`. `orderCount` is how many orders wait in all (the paging
total; it is not called `total` so no property of this API names a total). It lists **approved** orders of a project that is not
archived with at least one line still short of what was ordered (what posted deliveries name counts), soonest `expectedDate` first, an
order without one last, then by number. `search` matches the order number, the supplier's reference, the supplier's name or the
project's name; `projectId` narrows to a project. Page size is at most 100 (default 25). A row: `id`, `number`, `supplierName`,
`projectName`, `subProjectName?`, `expectedDate?`, `deliveryState` (`NotReceived` or `PartlyReceived`), `version`. The read is two SQL
statements (a count and a page) however many orders exist.

## One order

`GET /api/receiving/orders/{id}` answers the order, whatever its status (a draft, closed or cancelled order can be read to see what was
received): `id`, `number`, `status` (`Draft`, `Approved`, `Closed`, `Cancelled`), `supplierName`, `projectName`, `subProjectName?`,
`expectedDate?`, `supplierReference?`, `deliveryState` (`NotReceived`, `PartlyReceived`, `FullyReceived`), `canRecord` (an approved
order of a project that is not archived with something still to receive), `version`, `lines` and `deliveries`.

* A line: `id`, `lineNumber`, `description`, `unit`, `orderedQuantity`, `receivedQuantity` (deliveries that stand),
  `remainingQuantity` (ordered minus received, never below zero).
* A delivery: `id`, `receivedDate`, `deliveryReceiptNumber?`, `note?`, `status` (`Posted` or `Void`), `recordedByName`,
  `recordedAtUtc`, `voidedByName?`, `voidedAtUtc?`, `canVoid`, `version`, `lines` (`id`, `orderLineId`, `lineNumber`, `description`,
  `unit`, `quantity`). The newest 100 are returned, voided ones too, newest day first; `deliveryCount` is the true number. **The reason
  typed when a delivery was voided is not returned** (a reason can mention people or amounts); only that it was voided, by whom and when.

The read is five SQL statements (the order, its lines, what each line has received, the newest deliveries and their lines) however many
lines or deliveries exist.

## Recording and voiding

`POST /api/receiving/orders/{id}/deliveries` takes `receivedDate`, `deliveryReceiptNumber?` (60), `note?` (300), `lines`
(`[{ purchaseOrderLineId, quantity }]`, a quantity greater than zero, no line twice, leave out a line nothing arrived for) and
`clientRequestId?`, exactly as the purchasing route does, and answers `201` with the order above as it now stands. `POST
.../deliveries/{receiptId}/void` takes `{ reason, version }` (the delivery's `version`) and answers `200` with the order above. The
refusals are the purchasing ones: `409 rule_violation` for a draft, closed or cancelled order and an archived project, `422` per field
(`receivedDate`, `deliveryReceiptNumber`, `note`, `lines`, `reason`; **over-receipt is refused** and a delivery receipt number the supplier
already has is refused), `409 concurrency_conflict` for a stale `version` or two deliveries of the last quantity at once, and `404` for an
order or delivery of another company or of another order. A repeated `clientRequestId` returns the first delivery's result; the same
request id sent to the purchasing route answers with the same delivery. The audit entries are `GoodsReceipt.Recorded` and
`GoodsReceipt.Voided`, without the typed reason or note.
