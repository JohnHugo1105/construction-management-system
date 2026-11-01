# Purchasing (draft for `docs/business-rules.md`)

To be merged by the lead as a new numbered section, with its rows added to the access table, then this file deleted.

## 11. Purchasing and committed cost

### 11.1 Purchase orders
A **purchase order** is the company's promise to buy from one supplier for one project (and optionally one of its sub-projects). It has a
number ("PO-0001", one sequence per company, taken only once the order is valid), an order date (not in the future), an optional expected
date (not before the order date), the supplier's own reference and notes, and **1 to 100 lines**. A line is a cost line: classification,
description, item type, quantity, unit, unit cost and VAT treatment, with the company VAT rate **at the moment of entry** kept on it. Its
net, VAT and gross come from the same rules as budget lines and cost entries (section 4.4), and the order's total is the sum of its lines,
so `net + VAT = gross` on every line and on the order.

The firm must be an **active firm marked as a supplier**: an archived firm, or one that is only a subcontractor, is refused with the way
out ("Restore it" or "Edit the firm and tick Supplier"). A new order needs a project, and sub-project, that can receive records.

### 11.2 Steps and who may take them
| State | Meaning | Who |
| --- | --- | --- |
| Draft | Being prepared. The lines can be replaced. Nothing is committed. | Draft and edit: Administrator, Cost Engineer, Accountant |
| Approved | Lines locked. What is not yet invoiced is committed cost. | Approve: Administrator, Project Manager, Accountant |
| Closed | The supplier delivered less; the unspent remainder is released. Final. | Close: as approve |
| Cancelled | Called off with a reason. Final. | Cancel: as approve |

Nothing stops one person holding both roles; the person is recorded on every step. A draft can be cancelled. An approved order can be
cancelled only while **no posted invoice is matched to it**; once one is, the order is closed instead. An approved order is never edited:
cancel or close it and raise another. On an **archived project** nothing new is raised, and nothing is approved, cancelled or closed until
the project is restored. Approving checks the firm and the project again.

### 11.3 Matching an invoice
A supplier invoice (a manual cost entry) may name the **order line** it is for. The line must belong to an **approved** order of the
**same project**. An invoice larger than the line is allowed: the line then shows as "over by" the excess. Correcting an invoice keeps
its match even if the order has been closed since; a new match to a closed, cancelled or draft order is refused. The match can be changed
or cleared while the invoice is posted. Costs made by the system (payroll, stock) cannot be matched.

### 11.4 Committed cost and the forecast
For each line of an **approved** order, the open remainder is `line amount - posted invoices matched to it`, never below zero, worked out
on the amount before VAT and the amount with VAT separately. A project's **committed cost** is the sum of those remainders over its approved
orders (one line's overrun never reduces what another line still owes). A voided invoice no longer counts as matched, so its remainder
returns; a closed or cancelled order, and a draft, commit nothing.

**Forecast = actual cost + committed cost**, on the company's comparison basis (with or without VAT, section 4.7), and is judged against the
budget with the same thresholds as actual cost. The dashboard row, the dashboard total and the project's cost summary show the same
committed and forecast figures, and show nothing when nothing is committed. A forecast over the budget is shown as a warning, not as spent.

### Decisions that rest on assumptions
* **Default:** the number is taken when the draft is first saved, not at approval, and the company has one sequence of purchase orders
  (the specification's rule). A save that fails after the number was taken leaves a gap.
* **Default:** a closed order releases its whole unspent remainder; there is no partial close and no reopening. A mistaken close is
  corrected by raising a new order.
* **Default:** an order can be cancelled by anyone who may approve, as well as closed or approved; there is no separate permission for
  cancelling.
* **Default:** only **posted** invoices count as matched, so an order whose matched invoices have all been voided can be cancelled again.
* **Default:** the order total is stated with VAT as the headline figure; the comparison basis only decides which amount feeds the
  forecast.
* **Default:** the match between an invoice and an order line is chosen by a person; the system never matches automatically, and never
  pays or approves anything by itself.
* A cancel racing an invoice being matched to the same order at the same instant could leave a cancelled order with a matched invoice. The
  invoice is still a valid actual cost and the order commits nothing, so no figure is wrong; the order page lists the invoice.
