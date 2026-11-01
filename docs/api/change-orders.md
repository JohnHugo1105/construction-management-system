# Change orders and budget baseline API

The budget baseline of a project (lock, unlock, the original and revised figures) and its change orders. All routes are under `/api`,
return JSON in camelCase, and send `Cache-Control: private, no-store`. Money is `long` minor units in properties ending `Minor`
(`125050` is 1,250.50); a change is signed. Writes need the anti-forgery header, like every other write.

## Endpoints

| Method and route | Permission |
| --- | --- |
| `GET /api/projects/{projectId}/budget-baseline` | `changeorders.read` |
| `POST /api/projects/{projectId}/budget-baseline/lock` | `changeorders.approve` |
| `POST /api/projects/{projectId}/budget-baseline/unlock` | `changeorders.approve` |
| `GET /api/projects/{projectId}/change-orders?status=&page=&pageSize=` | `changeorders.read` |
| `POST /api/projects/{projectId}/change-orders` | `changeorders.manage` |
| `GET /api/change-orders/{id}` | `changeorders.read` |
| `PUT /api/change-orders/{id}` | `changeorders.manage` |
| `POST /api/change-orders/{id}/lines` | `changeorders.manage` |
| `PUT /api/change-orders/{id}/lines/{lineId}` | `changeorders.manage` |
| `POST /api/change-orders/{id}/lines/{lineId}/remove` | `changeorders.manage` |
| `POST /api/change-orders/{id}/submit` | `changeorders.manage` |
| `POST /api/change-orders/{id}/discard` | `changeorders.manage` |
| `POST /api/change-orders/{id}/approve` | `changeorders.approve` |
| `POST /api/change-orders/{id}/reject` | `changeorders.approve` |
| `POST /api/change-orders/{id}/void` | `changeorders.approve` |

Roles: read: Administrator, Project Manager, Cost Engineer, Accountant, Viewer. Manage (draft, edit, submit, discard): Administrator,
Project Manager, Cost Engineer. Approve (lock, unlock, approve, reject, void): Administrator, Project Manager. HR Officer and Warehouse
Keeper have no access. A project or change order of another company is `404`.

## Budget baseline

`GET .../budget-baseline` returns:

| Field | Meaning |
| --- | --- |
| `locked`, `lockedAtUtc`, `lockedBy` | Whether the budget is frozen, when and by whom. |
| `originalBudgetNetMinor`, `originalBudgetGrossMinor`, `originalContractPriceMinor` | Recorded at lock (null while unlocked; the price is null when the project had none). |
| `approvedCount`, `approvedBudgetNetMinor`, `approvedBudgetGrossMinor`, `approvedContractPriceDeltaMinor` | The recorded effects of the change orders that are approved now (voided ones don't count). |
| `revisedBudgetNetMinor`, `revisedBudgetGrossMinor`, `revisedContractPriceMinor` | Original + approved changes. While unlocked: the current figures. |
| `activeLineCount`, `currentBudgetNetMinor`, `currentBudgetGrossMinor`, `currentContractPriceMinor` | The active budget lines and the project price now. |
| `pendingCount`, `pendingBudgetNetMinor`, `pendingBudgetGrossMinor`, `pendingContractPriceDeltaMinor` | Submitted orders waiting for a decision (an estimate; not in any total). |
| `reconciles` | `true` when current = revised for net, gross and price. It is always true unless data was changed outside the API. |
| `lastUnlockReason`, `lastUnlockedAtUtc` | The last unlock. |
| `projectVersion` | The project's `version`, for lock and unlock. |

* `POST .../lock` takes `{ version }` (the project's). Needs at least one active budget line and a project that is not archived.
  Refusals (`409 rule_violation`): "Add at least one budget line before locking the budget.", "The budget is already locked.".
* `POST .../unlock` takes `{ reason, version }`. Refused while any change order is approved: "The budget can't be unlocked because a
  change order has been approved. Record a change order to change it."
* While locked, `POST/PUT /api/.../budget-lines` and `POST /api/budget-lines/{id}/remove` answer `409`: "The budget is locked. Record a
  change order to change it." A project edit that changes the contract price is a `422` on `contractPriceMinor` ("The contract price is
  locked with the budget. Record a change order to change it."), except a first price on a project that had none.
* `ProjectDto` carries `budgetBaselineLocked`. `BudgetLineDto` carries `changeOrderId` and `changeOrderNumber` ("CO-001") on lines a
  change order added.

## Change orders

Statuses: `Draft`, `Submitted`, `Approved`, `Rejected`, `Voided` (with `voidedFromStatus`: `Draft` or `Submitted` = discarded,
`Approved` = reversed). Reasons: `ClientRequest`, `DesignChange`, `SiteCondition`, `ErrorOrOmission`, `Other`.

* **Create** `{ title, description?, reason, contractPriceDeltaMinor, clientRequestId? }` returns `201` with the draft. Needs a locked
  budget. A repeated `clientRequestId` returns the original; reused for another project it is `409`.
* **Update** (draft only) `{ title, description?, reason, contractPriceDeltaMinor, version }`.
* **Lines** (draft only) `{ kind, ..., version }` where `version` is the change order's:
  * `Add`: the budget line fields (`subProjectId?`, `division?`, `mainScope?`, `subScope?`, `wbsCode?`, `description`, `itemType`,
    `quantity`, `unit`, `unitCostMinor`, `vatTreatment`, `entryDate`, `remarks?`), checked by the budget line rules; the company VAT
    rate is snapshotted when the line is added and kept when it is edited.
  * `Revise`: `budgetLineId`, `quantity`, `unitCostMinor`. The target must be an active line of the project, at most once per order,
    and something must change. Its other fields, VAT treatment and rate stay.
  * `Omit`: `budgetLineId`.
  * At most 100 lines. Removing a line renumbers the rest 1, 2, 3.
* **Submit** `{ version }`: needs a line or a price change; takes the next number of the project (`change-order:{projectId}`), "CO-001".
* **Approve** `{ note?, version }`: applies every line and the price delta in one save (Add creates a budget line marked with the order;
  Revise keeps the previous quantity, unit cost and money on the line; Omit removes the line with the reason "Change order CO-001").
  Refused when a target was removed since ("Line 2 (Rebar): that budget line was removed since. Edit or remove this line first.").
* **Reject** `{ reason, version }` (submitted only). **Discard** `{ reason, version }` (draft or submitted).
* **Void** `{ reason, version }` (approved only): reverses every line and the price. Refused while a change order approved after it
  stands ("Void CO-003 first. ...") or the project is archived.

`ChangeOrderDto`: `id`, `projectId`, `number`, `title`, `description`, `reason`, `status`, `voidedFromStatus`,
`contractPriceDeltaMinor`, `budgetEffectNetMinor`, `budgetEffectVatMinor`, `budgetEffectGrossMinor` (signed; an estimate until
approved), who and when for each step (`createdBy`, `submittedBy`, `decidedBy`, `voidedBy` and their `...AtUtc`), `decisionReason`,
`voidReason`, `voidBlockedBy` (the number to void first, for an approved order), `lines`, `version`, and `governmentLimit`
(read-only, advisory: `applies` for a government client, set in the project's billing terms, with a locked baseline; `limitMinor` is 10%
of the original contract price, `usedMinor` the approved price increases plus this order's own while it is a draft or submitted, and
`exceeds` when used passes the limit; approval is still allowed).

Each line: `id`, `lineNumber`, `kind`, `budgetLineId`, the budget line fields as they will be (Add, Revise) or as removed (Omit),
`netMinor`, `vatMinor`, `grossMinor`, `previousQuantity`, `previousUnitCostMinor`, `previousNetMinor`, `previousGrossMinor` (Revise, Omit)
and the signed `effectNetMinor`, `effectVatMinor`, `effectGrossMinor`.

The list returns `{ items, total, page, pageSize }`, drafts first, then by number, newest first; `status` filters (an unknown status is
a `422`).

Errors: `422` with `errors` per field, `409 rule_violation` for a rule, `409 concurrency_conflict` for a stale `version`, `404` for a
record of another company, `403` for a missing permission.

## Statement counts

Fixed whatever the number of orders and lines: list 3 (the project, the count, the page), detail 3 (the order with names and the
"void first" check, its lines, the government limit), baseline 3 (the project, the active line sums, the order sums by status). Asserted in
`ChangeOrderApiTests`.
