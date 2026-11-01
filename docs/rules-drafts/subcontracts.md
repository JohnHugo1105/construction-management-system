# Subcontracts (rule draft for docs/business-rules.md)

The company gives work to subcontractors, certifies their progress claims (which become project cost), holds retention, and records what
it has paid. The system never pays anyone: a payment is a person recording that it was paid.

## S.1 The subcontract

* A subcontract names a **firm set up as a subcontractor** (an archived firm, or one that is only a supplier, is refused), a project and
  optionally a sub-project that can receive records, the scope of work (up to 200 characters) and notes. It is numbered `SC-0001` in the
  company's sequence when it is drafted.
* The **contract sum** is a base amount with a VAT treatment, exactly like a cost line (section 4.4): it includes VAT, is before VAT, or
  has no VAT. The company VAT rate **at the moment of drafting is kept** on the subcontract, so a later change of the company rate never
  changes it or its claims.
* **Retention** is 0% to 30% (10% when not given). **Withholding tax** is 0% to 10% (0% when not given): the tax the company withholds from
  the subcontractor's payment, worked out on the amount before VAT.
* Lifecycle: **Draft -> Awarded (Active) -> Completed**, or **Cancelled**. A draft can be edited freely by people who draft subcontracts.
  **Awarding** (an approver) commits the sum to the project. **Completing** (an approver) needs no open draft claim and ends claims.
  **Cancelling**, with a reason, is allowed for a draft (by a drafter) or an awarded subcontract with nothing certified (by an approver).
* A **variation** adds or takes off work as a signed base amount with a description and date. While the subcontract is a draft a drafter
  records it; once awarded, only an approver. The **revised sum** (contract sum plus variations) must stay above zero and never fall below
  the work already certified.
* An archived project's subcontracts are read-only (restore the project first).

## S.2 Progress claims

* The subcontractor's claim states the **work done to date**: the base amount of all work so far, including this claim. It must be more
  than the work already certified and not more than the revised sum. The claim date is not in the future; the period it covers ends on or
  before the claim date. Only one draft claim is open at a time.
* Worked out once, each figure rounded once half away from zero:

  | Figure | Rule |
  | --- | --- |
  | This claim | work to date - work already certified |
  | Net, VAT, gross | this claim on the subcontract's VAT treatment and kept rate (section 4.4) |
  | Retention | round(gross x retention rate) |
  | Withholding tax | round(net x withholding rate) |
  | **To pay (net payable)** | gross - retention - withholding |

  Example, 1,000,000.00 before VAT at 12%, 10% retention, 2% withholding: work to date 250,000.00 gives VAT 30,000.00, gross 280,000.00,
  retention 28,000.00, withholding 5,000.00, to pay 247,000.00. Claim 2 at 600,000.00 to date adds 350,000.00: gross 392,000.00, retention
  39,200.00, withholding 7,000.00, to pay 345,800.00. With VAT included, 250,000.00 splits into net 223,214.29 and VAT 26,785.71;
  withholding is 4,464.29 (2% of the net, 4,464.2858) and to pay 220,535.71.
* **Certifying** (an approver) re-checks the chain: a draft worked out on certified work that has changed since (for example the claim
  before it was voided) must be saved again first. Certifying numbers the claim (1, 2, 3 within the subcontract; numbers are never reused)
  and, **in the same save, posts the work as project cost**: one cost entry naming the subcontractor, item type Subcontract, dated the claim
  date, equal to the claim's net, VAT and gross to the centavo. **Retention is part of the cost** (it is still owed).
* A posted subcontract cost can't be edited or voided at the cost entry. It is corrected by **voiding the claim** (an approver, with a
  reason), which voids the cost in the same save. Only the latest certified claim can be voided, only while no payment is recorded against
  it, and never so that more retention would have been released than is then held. A draft claim is discarded with a reason by a drafter.
  Nothing is deleted.

## S.3 Payments and retention

* A payment (an approver) is against a certified claim, more than zero and not more than what is still owed on it; dated not in the future
  and not before the claim. Its method is bank transfer, cheque, cash or other, with an optional reference and note.
* Retention held is the retention on certified claims. Once the subcontract is **completed**, an approver records retention releases, up to
  the retention still held.
* A payment is voided with a reason; what it paid becomes owed again. Outstanding is never negative.

## S.4 Commitments

* An awarded subcontract commits the project to its revised sum not yet certified: `max(0, revised - certified)`, with and without VAT.
  It is added to actual cost to give the **forecast** on the dashboard and the project cost summary. As claims are certified, actual cost
  rises and the commitment falls by the same amount; a completed or cancelled subcontract commits nothing more; a draft commits nothing.

## S.5 Access

Read: Administrator, Project Manager, Cost Engineer, Accountant, Viewer. Draft subcontracts and claims: Administrator, Project Manager, Cost
Engineer. Award, certify, void, record payments, release retention, complete: Administrator, Accountant. One person may hold both (an
administrator can do every step); the activity log records who did each.

## Decisions that rest on assumptions

1. **Default**: retention 10% and withholding 0% when not given (spec).
2. Retention is held on the **gross** (VAT-included) amount of each claim; withholding is on the **net**.
3. Only **one draft claim** is open at a time per subcontract, so the claim chain stays a single line.
4. A claim's number is given **at certification**, not when drafted, so discarded drafts leave no gap in certified numbers (a save that
   fails after taking the number can still leave one).
5. A payment can't be dated **before the claim date**; the claim's period can't end after its claim date.
6. Cancelling an awarded subcontract with no certified work **discards its open draft claim** with the cancel reason.
7. The posted subcontract cost carries **no work-breakdown classification** (the cost-entry system posting takes none); it groups under
   item type Subcontract and "Unclassified" in the cost summary.
8. A completed subcontract can still have its latest claim voided (as a correction) while retention released stays covered.
