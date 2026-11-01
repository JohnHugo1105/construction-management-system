# Inventory API (warehouse tools, equipment and machinery)

Permissions: `inventory.read` (Administrator, Project Manager, Warehouse Keeper, Cost Engineer, Viewer) and `inventory.manage`
(Administrator, Warehouse Keeper). General conventions (camelCase, string enums, `version`, problem details, paging) are in
`docs/development/vertical-slice-guide.md`.

## Endpoints

| Method and route | Permission | Returns |
| --- | --- | --- |
| `GET /api/inventory/items?search&category&status&projectId&inWarehouse&page&pageSize` | read | `PagedResult<InventoryListItem>` |
| `GET /api/inventory/items/{id}` | read | `InventoryItem` |
| `GET /api/inventory/items/{id}/movements?page&pageSize` | read | `PagedResult<StockMovement>`, newest first |
| `GET /api/projects/{projectId}/inventory?page&pageSize` | read | `PagedResult<ProjectStock>` (what is on that project now) |
| `POST /api/inventory/items` | manage | 201 `InventoryItem` (registers the item and receives its first stock into the warehouse) |
| `PUT /api/inventory/items/{id}` | manage | `InventoryItem` (details only; the category can't change) |
| `POST /api/inventory/items/{id}/receive` | manage | `InventoryItem` (restock a tool; a single piece is received once, when registered) |
| `POST /api/inventory/items/{id}/transfer` | manage | `InventoryItem` |
| `POST /api/inventory/items/{id}/adjust` | manage | `InventoryItem` (a stock count) |
| `POST /api/inventory/items/{id}/write-off` | manage | `InventoryItem` |
| `POST /api/inventory/items/{id}/movements/{movementId}/reverse` | manage | `InventoryItem` |
| `POST /api/inventory/items/{id}/retire` | manage | `InventoryItem` |

Every write returns the item as it now stands, including its new `version` and balances. List filters: `category` is `Tool`,
`Equipment` or `Machinery`; `status` is `Active` or `Retired`; `projectId` keeps items with stock on that project; `inWarehouse=true`
keeps items with stock in the warehouse. No `status` returns both. A wrong `category` or `status` is a 422 on that field.

## Shapes

```
InventoryListItem  { id, description, category, brand?, serialNumber?, unit, unitCostMinor, status, totalQuantity, balances: Balance[] }
InventoryItem      { id, description, category, brand?, serialNumber?, unit, sourceDealer?, invoiceNumber?, purchaseDate, unitCostMinor,
                     remarks?, status, retiredReason?, totalQuantity, balances: Balance[], version }
Balance            { location: "Warehouse" | "Project", projectId?, projectName?, quantity }     warehouse first, then projects by name
StockMovement      { id, type, fromName?, fromProjectId?, toName?, toProjectId?, quantity, date, chargedAmountMinor, note?, recordedBy,
                     recordedAtUtc, isReversed, reversalOfMovementId? }
ProjectStock       { itemId, description, category, brand?, serialNumber?, unit, quantity }
Location (request) { kind: "Warehouse" } | { kind: "Project", projectId }
```

`type` is `Purchase`, `Transfer`, `Adjustment`, `WriteOff` or `Reversal`. A missing `fromName` or `toName` means that side is outside
the system (a supplier, a write-off, a stock count), and `type` says which. `chargedAmountMinor` is above zero only on a charged
transfer. Show "Reverse" on a line only when `type` is not `Reversal` and `isReversed` is false.

## Requests

```
register  { description, category, brand?, serialNumber?, unit, sourceDealer?, invoiceNumber?, purchaseDate, unitCostMinor, quantity,
            remarks?, clientRequestId? }
update    { description, brand?, serialNumber?, unit, sourceDealer?, invoiceNumber?, purchaseDate, unitCostMinor, remarks?, version }
receive   { quantity, date?, note?, clientRequestId?, version }
transfer  { fromLocation, toLocation, quantity, date?, chargeToProject, note?, clientRequestId?, version }
adjust    { location, countedQuantity, reason, date?, clientRequestId?, version }
write-off { location, quantity, reason, date?, clientRequestId?, version }
reverse   { reason, clientRequestId?, version }
retire    { reason, version }
```

`date` left out means today (company time zone). Send a fresh GUID as `clientRequestId` per form: a repeated request returns the
original result instead of doing it twice, even though its `version` is by then stale. Money is minor units (`unitCostMinor`,
`chargedAmountMinor`); quantities are decimals with up to 4 places.

## Rules the server enforces

* Equipment and machinery are single pieces: a serial number is required and unique in the company (however it is typed), the
  quantity is 1, a count is 0 or 1, and a piece can't be recorded in two places.
* A place can't give more than it holds: "Only 10 pc are there, but you asked for 11." Nothing changes when a request is refused.
* A **charged transfer** (`chargeToProject: true`, to a project) posts one cost to that project in the same save: equipment,
  1 lump sum (`l.s`), unit cost = quantity times the item's unit cost (rounded once to the centavo), VAT included at the company's
  rate, dated the movement date. Reversing that transfer voids the cost with the reversal reason. Moving stock to the warehouse is
  never charged, and neither is an item with no unit cost.
* Reversing: once only, a reversal can't be reversed, the stock must still be where the move put it, and a reason is required.
  Writing off a single piece retires it; reversing that write-off restores it. A retired item can't be edited, moved or restocked,
  and only its write-off can be reversed.
* Stock can't be moved **onto** an archived project (409). Moving stock off one, counting it and writing it off are allowed, so a
  finished site can still be emptied by a Warehouse Keeper, who can't restore a project.
* A project named in a request must belong to the company (422 on `fromLocation`, `toLocation` or `location`).

## Errors

422 `validation_failed` (field to messages), 409 `rule_violation` (the rule above, in plain words), 409 `concurrency_conflict` (reload the
item and try again), 404 (no such item, movement or project in this company), 403 (role), 401 (signed out).
Two people moving the last units at once: exactly one succeeds, the other gets `concurrency_conflict`.

## Audit

`Inventory.Registered`, `Inventory.Updated`, `Inventory.Received`, `Inventory.Transferred`, `Inventory.Adjusted`,
`Inventory.WrittenOff`, `Inventory.Reversed`, `Inventory.Retired` on entity type `InventoryItem`, each with a one-line summary naming
the item, the quantity and the places.
