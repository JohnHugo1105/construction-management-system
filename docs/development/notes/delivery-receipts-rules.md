# Delivery receipts on purchase orders: rules for `docs/business-rules.md`

For the lead to merge (this note is deleted after the merge). Proposed home: section 12 Purchasing, as **12.5 Deliveries**, plus one row in the
section 2 access table and the principle row "The system never pays or collects" needs no change (a delivery moves no money).

## Access table row (section 2)

| Permission | Administrator | Project Manager | Cost Engineer | Accountant | Warehouse Keeper | HR Officer | Viewer |
| --- | :-: | :-: | :-: | :-: | :-: | :-: | :-: |
| Record or void a delivery against a purchase order | yes | yes | yes | yes | | | |

Everyone who may see purchase orders sees the deliveries on them. Warehouse Keeper has no purchasing access (**Default**: it is the
existing role matrix; the lead may want to give Warehouse Keeper `purchasing.receive` and `purchasing.read` later, since in practice the
store keeper receives the goods).

## 12.5 Deliveries

Today an order says what was ordered and what was invoiced. A **delivery** (a delivery receipt, "DR") says what the supplier actually brought,
so ordered, received and invoiced are three honest numbers.

* **What a delivery is.** A record against one **approved** order: the day it arrived (not in the future, in the company's business day), the
  number on the supplier's own delivery receipt (up to 60 characters, optional), a note (up to 300) and a quantity for each order line that
  arrived. It names the person who recorded it.
* **Append-only.** A delivery is never edited or deleted. A mistake is **voided with a reason** and the delivery is entered again. A voided
  delivery stays on record with who voided it and why, counts for nothing, and frees its delivery receipt number. A void is refused on an
  archived project ("Restore it first"). A delivery can still be voided after its order was closed.
* **When one can be recorded.** Only against an approved order of a project that is not archived: a draft ("... is not approved yet"), a closed
  order ("This order is closed.") and a cancelled one ("This order was cancelled.") refuse.
* **How much.** A line's quantity must be greater than zero (at most 4 decimals) and **not more than what is still to receive**: ordered minus
  what deliveries that stand already name. **Default: over-receipt is refused.** What a supplier brings beyond the order is a new order, or
  an amended one. Exactly the quantity ordered is accepted. A line is named at most once in a delivery, and a delivery names at least one line.
* **Duplicate protection.** The same delivery receipt number for the same supplier on a delivery that stands is refused: "This delivery receipt
  is already recorded." Numbers are compared by their letters and digits only, ignoring case, so "DR-0045", "dr 0045" and "DR0045." are one
  delivery receipt. It holds across all of that supplier's orders. A delivery with no number is never a duplicate; a voided delivery frees its
  number. The database holds the same rule, so two people saving the same delivery receipt at the same moment cannot both succeed.
* **Two people at once.** Recording a delivery touches the order, so two deliveries of the last quantity (or a delivery and a close or cancel)
  at the same moment cannot both be saved: one succeeds, the other is told to reload or that the quantity has already been received.
* **What is derived (never stored).** Per line: the **received quantity** (deliveries that stand), the quantity **still to receive** (ordered
  minus received, never below zero) and the **received value**: the line's unit cost times the received quantity, rounded once, half away from
  zero, on the line's own VAT treatment and the VAT rate kept on the line, so net + VAT = gross and receiving the whole line is worth exactly
  the line. Per order: the **delivery state**: *not received* (nothing arrived), *partly received* (something arrived, a line is still short)
  or *fully received* (every line has arrived in full). The list shows the state only for an approved order.
* **Invoiced ahead of delivery (advisory).** A line is flagged when what was invoiced to it (with VAT) is more than its received value. It
  is a note on the order page ("Invoiced ahead of delivery: ... invoiced, ... delivered so far. The invoice stands."); the invoice is still valid,
  still matched, and still reduces the commitment.
* **A delivery changes no money.** **Default:** committed cost still falls only when an invoice is matched to the order line (section 12.4),
  so the dashboard, the Budget tab and the forecast do not move when goods arrive or when a delivery is voided. Stock is not updated by a
  delivery (receiving into stock is not built; the screen says nothing about it).
* **Cancel and close.** An approved order with a delivery that stands **cannot be cancelled** ("Deliveries are already recorded against this
  order, so it can't be cancelled. Close it instead ..."), like an order with a matched invoice; voiding the delivery makes cancelling
  possible again. **Close** is always possible and keeps the deliveries on record.
* **Audit.** `GoodsReceipt.Recorded` and `GoodsReceipt.Voided` name the order and the project. The note, the delivery receipt number and the
  void reason stay on the delivery and are not copied into the activity log.
* **On screen.** The order page leads with **Record delivery** as its one primary action while something is left to receive (for people who
  may record deliveries); the **Deliveries** section appears once a delivery exists, newest first, voided ones muted with their reason; each
  line shows "Ordered 10 pc · Received 4 pc · Invoiced ₱2,000.00" once anything has arrived. The orders list (and the project's Purchases
  tab) carries a delivery chip only for an approved order that has had something arrive or has an expected date ("Partly delivered",
  "Delivered in full", "Expected 10 Nov 2026"); a draft, closed or cancelled order shows none.

## Decisions that rest on assumptions

1. **Over-receipt is refused** (the supplier's extra is a new or amended order). Some companies accept a tolerance (for example 5% on bulk
   material); that would be a company setting.
2. **A delivery does not release committed cost.** Committed cost falls only on a matched invoice, so goods received but not yet invoiced stay
   committed (they are owed). The accountant may prefer to accrue goods received not invoiced separately; not built.
3. **An approved order with a delivery cannot be cancelled** (it must be closed). Not in the original ask; added so a cancelled order never
   carries delivered goods. Easy to relax: the check is one condition in `PurchaseOrder.Cancel`.
4. **The delivery receipt number is compared by letters and digits only**, the way supplier invoice numbers are, and per supplier across orders.
5. **Fully received** means every line has arrived in full; an order cut short is closed by a person, not by the system.
6. **No stock movement.** Warehouse receiving into stock stays a separate, manual stock movement.
7. **Warehouse Keeper has no purchasing access** (existing role matrix, `purchasing.receive` is Administrator, Project Manager, Cost Engineer
   and Accountant). Site receiving by a warehouse keeper would need the permission added to that role.

## Not built, deliberately

Delivery photos or a scanned delivery receipt; receiving into stock; partial returns or rejected goods; a tolerance for over-receipt; an
overdue-delivery alert (the list shows the expected date only); automatic closing of a fully received and fully invoiced order.
