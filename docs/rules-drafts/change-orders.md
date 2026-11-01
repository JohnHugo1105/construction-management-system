# Change orders and the budget baseline (draft for `docs/business-rules.md`)

## 4.8 The budget baseline

* Once the budget is agreed, a project manager or administrator **locks** it. The sums of the active budget lines (before VAT and with
  VAT) and the contract price at that moment are recorded as the **original**. Locking needs at least one active budget line, and an
  archived project can't be locked or unlocked.
* While the budget is locked, budget lines can't be added, changed or removed directly ("The budget is locked. Record a change order
  to change it."), and the contract price can't be changed on the project. A project locked before it had a contract price may still
  record its first price, which becomes the original price.
* The **revised** budget and price are the original plus the approved change orders. The active budget lines always add up to the
  revised budget exactly, to the centavo; the baseline page checks it every time it is shown.
* The budget can be **unlocked** (with a reason, kept on the project) only while no change order is approved. Unlocking clears the
  original; locking again records the budget as it is then.

## 4.9 Change orders

* A change order belongs to one project and has a title, a reason (client request, design change, site condition, error or omission,
  other), an optional description, an optional change to the contract price (with VAT, up or down; the price must exist, and stays
  between zero and ₱100,000,000,000.00), and up to 100 lines. Change orders can be recorded only on a locked budget.
* A line **adds** a budget line (with every field a budget line has, checked by the same rules, at the company VAT rate when the line is
  entered), **revises** an active budget line (a new quantity and unit cost; its description, VAT treatment and rate stay), or **omits**
  an active budget line. A budget line is changed at most once per change order.
* Each line's effect on the budget is worked out by the shared line rules: an addition adds its amount, a revision adds the new amount
  minus the old, an omission subtracts the line. The order's effect is the sum of its lines, before VAT, VAT and with VAT.
* **Workflow.** Draft (editable) → **Submitted** (numbered CO-001, CO-002 ... per project, at submission; a submission that is refused
  uses no number) → **Approved** or **Rejected** (with a reason). A draft or submitted order can be **discarded** with a reason. Nothing
  is deleted.
* **Approving** applies everything in one save: added lines become budget lines marked with the change order; revised lines take the
  new quantity and unit cost, and the old ones are kept on the change order; omitted lines are removed with the reason "Change order
  CO-001"; the contract price moves by the change. Every target must still be active, or approval is refused naming the line.
* **Voiding** an approved change order (with a reason) reverses it exactly in one save: added lines are removed, revised lines get
  their old quantity and unit cost back (and therefore their old amounts, to the centavo), omitted lines are reinstated, and the price
  moves back. Change orders are voided **newest first**: a void is refused while a change order approved after it still stands, and
  the message names it. A void is refused on an archived project.
* Drafting, submitting and discarding are for project managers, cost engineers and administrators; locking, unlocking, approving,
  rejecting and voiding are for project managers and administrators. Accountants and viewers read. The activity log records each
  step without the reasons (they stay on the record).
* The dashboard, the project cost summary and the Budget tab need no change of their own: they add up the active budget lines, so an
  approval moves them by exactly the approved effect and a void returns them.

### Decisions that rest on assumptions

1. Change orders need a locked budget; before locking, the budget is changed directly.
2. Approval and void figures use the amounts with VAT for the contract price (it includes VAT) and keep both net and gross for the budget.
3. "Later approved" means approved at a later moment (ties broken by number); voiding is strictly newest first.
4. Unlocking is allowed while change orders are submitted but not approved; they can't be approved until the budget is locked again.
5. A revision keeps the line's own VAT rate; an added line uses the company rate when the line is entered, not when it is approved.
6. Approving takes an optional note; rejecting, discarding and voiding need a reason (up to 500 characters).
7. The contract price can't be typed over on a locked budget; it changes only through change orders (first price excepted).
