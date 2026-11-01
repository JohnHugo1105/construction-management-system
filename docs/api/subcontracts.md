# Subcontracts API

Work awarded to subcontractors, their progress claims (certified into project cost), retention, and the payments a person records. All
routes are under `/api`, return JSON in camelCase and send `Cache-Control: private, no-store`. Money is `long` minor units in properties
ending `Minor`; rates are basis points in properties ending `Bps` (1,000 is 10%). Dates are `"2026-11-02"`. Writes need the anti-forgery
header, like every other write. **Every write answers with the whole `SubcontractDto`**, so the page always shows the server's figures.

## Endpoints

| Method and route | Permission |
| --- | --- |
| `GET /api/subcontracts?projectId=&subcontractorId=&status=&search=&page=&pageSize=` | `subcontracts.read` |
| `GET /api/subcontracts/{id}` | `subcontracts.read` |
| `POST /api/subcontracts` (201) | `subcontracts.manage` |
| `PUT /api/subcontracts/{id}` (draft only) | `subcontracts.manage` |
| `POST /api/subcontracts/{id}/award` | `subcontracts.approve` |
| `POST /api/subcontracts/{id}/complete` | `subcontracts.approve` |
| `POST /api/subcontracts/{id}/cancel` | `subcontracts.read` at the route; the service requires `manage` for a draft, `approve` once awarded (else 403) |
| `POST /api/subcontracts/{id}/variations` | as cancel: `manage` while Draft, `approve` while Active |
| `GET /api/subcontracts/{id}/claims/preview?cumulativeWorkMinor=` | `subcontracts.manage` |
| `POST /api/subcontracts/{id}/claims` (draft a claim) | `subcontracts.manage` |
| `PUT /api/subcontracts/{id}/claims/{claimId}` (edit the draft) | `subcontracts.manage` |
| `POST /api/subcontracts/{id}/claims/{claimId}/discard` | `subcontracts.manage` |
| `POST /api/subcontracts/{id}/claims/{claimId}/certify` | `subcontracts.approve` |
| `POST /api/subcontracts/{id}/claims/{claimId}/void` | `subcontracts.approve` |
| `POST /api/subcontracts/{id}/claims/{claimId}/payments` | `subcontracts.approve` |
| `POST /api/subcontracts/{id}/retention-releases` | `subcontracts.approve` |
| `POST /api/subcontracts/{id}/payments/{paymentId}/void` | `subcontracts.approve` |

Roles: read by Administrator, Project Manager, Cost Engineer, Accountant and Viewer; drafted by Administrator, Project Manager and Cost
Engineer; approved (award, certify, void, pay, release, complete) by Administrator and Accountant. HR Officer and Warehouse Keeper have no
access. A subcontract, claim, payment, project or firm of another company is `404`; so is a claim id that belongs to a different subcontract.

## Requests

* **Create**: `subcontractorId`, `projectId`, `subProjectId?`, `title` (scope, 200), `notes?` (500), `contractSumMinor` (> 0),
  `vatTreatment` (`Inclusive`, `Exclusive`, `Exempt`), `retentionBps?` (0 to 3,000; 1,000 when absent), `withholdingBps?` (0 to 1,000; 0 when
  absent), `clientRequestId?`. The firm must be an active firm with the Subcontractor kind (else `409`); the project and sub-project must be
  able to receive records. The company VAT rate is **kept on the subcontract** (`vatRateBps`). The number `SC-0001` is taken from the
  company sequence `subcontract` only after the details pass their checks.
* **Update** (Draft only): the same fields without `projectId`, plus `version`. The kept VAT rate does not change.
* **Award / complete**: `{ version }`. **Cancel**: `{ reason, version }`.
* **Variation**: `{ description, amountMinor (signed base amount, not 0), variationDate, version }`.
* **Claim** (draft or edit): `{ claimDate, periodEnd, cumulativeWorkMinor, notes?, clientRequestId?, version }`. `cumulativeWorkMinor` is
  the base amount of **all work done to date** including this claim.
* **Certify**: `{ version }`. **Discard / void claim**: `{ reason, version }`.
* **Payment / retention release**: `{ amountMinor, paidOn, method (BankTransfer, Cheque, Cash, Other), reference? (80), note? (300),
  clientRequestId?, version }`. **Void payment**: `{ reason, version }`.

## Rules the API enforces

* Every problem with a form comes back at once as `422` with `errors` keyed by field. A stale `version` is `409 concurrency_conflict`.
  A business rule is `409 rule_violation` with a message to show as is.
* **Archived project**: every write is `409` ("This project is archived. Restore it before making changes."); reads still work and
  `projectArchived` is true.
* **Claims**: only on an Active subcontract; one draft at a time; `cumulativeWorkMinor` more than the certified work and not more than the
  revised sum (`422` on `cumulativeWorkMinor`); claim date not in the future (company time zone); period end not after the claim date.
* **Certify** re-checks the chain: a draft worked out on certified work that has changed since (`isStale: true`) is `409` ("Open the claim
  and save it again..."); saving it again (`PUT`) brings it up to date. It is also refused if the revised sum fell below its work to date.
  Certifying gives the claim its number (sequence `subcontract-claim:{subcontractId}`) and **posts a cost entry in the same save**:
  origin `Subcontract`, `originReferenceId` = claim id, `sourceDealer` = the firm's name, item type `Subcontract`, quantity 1, unit `l.s`,
  unit cost = the claim's base amount, the subcontract's VAT treatment and kept rate, dated the claim date, description
  `"SC-0001 claim 2: <scope>"`. Its net, VAT and gross equal the claim's to the centavo (retention is part of the cost). The claim's
  `costEntryId` points at it. The cost entry cannot be edited or voided at `/api/cost-entries` (`409`).
* **Void a claim**: only the latest certified claim, only with no recorded payment against it, never so that more retention would be
  released than held; its cost entry is voided in the same save with the same reason. Claim numbers are never reused.
* **Payments**: against a certified claim, at most its outstanding net payable (`422` on `amountMinor`, naming the amount still owed), dated
  not in the future and not before the claim date. A **retention release** only on a Completed subcontract, at most the retention still held.
  Recording a payment is a person saying it was paid; nothing is paid by the system.
* **Complete** needs no open draft claim. **Cancel** is for a Draft, or an Active subcontract with nothing certified (open drafts are
  discarded with the same reason). **Variations** keep the revised sum above zero and not below the certified work.
* **Repeated requests**: a repeated `clientRequestId` on create, draft claim, payment or retention release answers with the subcontract as
  the first request left it, even with the stale version; reused for a different subcontract, claim or payment kind it is `409`.

## Responses

`SubcontractListItemDto` (list, newest first, `PagedResult`): `id`, `number`, `title`, `subcontractorId`, `subcontractorName`,
`projectId`, `projectName`, `subProjectName`, `status` (Draft, Active, Completed, Cancelled), `vatTreatment`, `revisedSumMinor`,
`certifiedWorkMinor`, `percentCertifiedBps` (null when the revised sum is 0), `retentionHeldMinor` (still held), `paidMinor` (claims and
retention), `outstandingMinor`, `version`. An unknown `status` filter is `422`. Search matches number, scope and firm name, literally.

`SubcontractDto`: the terms (`number`, `title`, `notes`, firm, project, sub-project, `projectArchived`, `status`, `vatTreatment`,
`vatRateBps`, `retentionBps`, `withholdingBps`), the money position (`contractSumMinor`, `variationsMinor`, `revisedSumMinor`,
`revisedNetMinor`, `revisedVatMinor`, `revisedGrossMinor`, `certifiedWorkMinor`, `remainingWorkMinor`, `percentCertifiedBps`,
`certifiedNetMinor`, `certifiedGrossMinor`, `retentionHeldMinor`, `retentionReleasedMinor`, `retentionBalanceMinor`, `withholdingMinor`,
`netPayableMinor`, `paidOnClaimsMinor`, `outstandingMinor`), who and when for each step, `cancelReason`, `claims[]` (newest first),
`payments[]` (newest first), `variations[]` (in number order), `version`.

`SubcontractClaimDto`: `id`, `claimNumber` (null for a draft), `status` (Draft, Certified, Voided), `claimDate`, `periodEnd`,
`cumulativeWorkMinor`, `previousCumulativeMinor`, `thisClaimBaseMinor`, `netMinor`, `vatMinor`, `grossMinor`, `retentionMinor`,
`withholdingMinor`, `netPayableMinor`, `paidMinor`, `outstandingMinor`, `isStale`, `notes`, `costEntryId`, who and when, `voidReason`.

`SubcontractPaymentDto`: `id`, `kind` (Claim, RetentionRelease), `claimId`, `claimNumber`, `amountMinor`, `paidOn`, `method`, `reference`,
`note`, `status` (Recorded, Voided), who and when, `voidReason`.

`ClaimPreviewDto` (preview): `cumulativeWorkMinor`, `previousCumulativeMinor`, `thisClaimBaseMinor`, `netMinor`, `vatMinor`, `grossMinor`,
`retentionMinor`, `withholdingMinor`, `netPayableMinor`. A cumulative the API would refuse is the same `422` as drafting.

## Commitments

`SubcontractCommitmentSource` adds, for each **Active** subcontract, `max(0, revised - certified)` on net and on gross, per project, in one
grouped statement. The dashboard (`/api/reports/dashboard`) and the project cost summary show it as `commitments` (committed and forecast).
Drafts, completed and cancelled subcontracts commit nothing; certified work is already actual cost and is never counted twice.

## Statement counts

The list is at most 4 statements (the project check when filtered by project, count, page; no per-row queries); a subcontract is
4 (subcontract, claims, payments, variations), however many rows exist. Both are asserted by `SubcontractAccessApiTests`.
