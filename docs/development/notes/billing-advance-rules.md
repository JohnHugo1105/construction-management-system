# Slice G rules note: down payment, VAT withholding, government client

For the lead to merge into `docs/business-rules.md` section 14 (Client billing) and section 10.1 (assumptions), then delete this note.
Wording follows that document. Money is in centavos, each figure rounded once, half away from zero, multiplying before dividing.

## 14.1 Billing terms (rows to add to the table)

| Term | Range | Standard |
| --- | --- | --- |
| VAT withheld: the share of the amount before VAT the client withholds as VAT and covers with its own certificate | 0% to 12% | 0% |
| Down payment: a share of the contract price billed before any progress bill | 0% to 50% (15% for a government client) | 0% |
| Government client: the client is a government agency | yes or no | no |

A government client allows a down payment of **up to 15%** of the contract price; saving more is refused with "A government project
allows a down payment of up to 15% of the contract price." Leaving the down payment, the VAT withheld or the government setting out of a
save keeps what is saved.

## 14.2 Progress bills (replace the list and add the second example)

* **This bill** = work completed to date − work billed before.
* **Net** = round(this bill × 10,000 ÷ (10,000 + VAT rate)); **VAT** = this bill − net.
* **Retention** = round(this bill × retention rate).
* **Withholding** = round(net × withholding rate).
* **VAT withheld** = round(net × VAT withheld rate).
* **Down payment recovered**: see 14.3a. It is taken after the other deductions, which are all worked out on the bill as it is.
* **The client pays** = this bill − retention − withholding − VAT withheld − down payment recovered. If that would be below zero the bill
  is refused: "The deductions are more than this bill. Check the down payment, retention and withholding rates." (Exactly zero is
  allowed.)

Example (₱11,200,000.00 contract, 12% VAT, 10% retention, 2% withholding, 5% VAT withheld, 15% down payment): the down payment bill asks
for ₱1,680,000.00. Work to date ₱2,240,000.00 gives net ₱2,000,000.00, retention ₱224,000.00, withholding ₱40,000.00, VAT withheld
₱100,000.00, down payment recovered ₱336,000.00, and the client pays ₱1,540,000.00. At ₱5,600,000.00 to date the bill is ₱3,360,000.00:
recovered ₱840,000.00 − ₱336,000.00 = ₱504,000.00, the client pays ₱2,310,000.00. The last bill, to ₱11,200,000.00, recovers the
₱840,000.00 left and the client pays ₱3,850,000.00. Over the contract the client is asked for ₱9,380,000.00 = the price less retention
₱1,120,000.00, withholding ₱200,000.00 and VAT withheld ₱500,000.00: the down payment is never billed twice.

## 14.3a Down payment (new subsection)

* A project has at most **one** down payment bill that is not void (a draft or an issued one). It needs a contract price and a down
  payment in the terms. **Down payment** = round(contract price × down payment rate). If that rounds to nothing it is refused.
* It is a **receivable only**: no VAT is split out of it and nothing is retained or withheld; the client pays the whole amount. The VAT
  is billed on the progress bills at the full value of the work, so it is never counted twice.
* It takes the next company bill number **when it is issued**, like any bill, and it must be issued **before the first progress bill**:
  preparing or issuing it after a progress bill is issued is refused with "Bill the down payment before the first progress bill."
* It keeps its own rate. Changing the down payment in the terms later changes nothing already billed, and recovery always uses the rate
  on the down payment bill.
* **Recovery** on each progress bill is worked out on the work to date, so it is exact when the work reaches the price:
  recovered = min(down payment, round(work to date × rate)) − recovered on earlier issued progress bills, never below zero. The bill
  that brings the work to the contract price recovers **everything still to recover**, so a price changed by a change order since the
  down payment was billed still leaves nothing behind. Without an issued down payment nothing is recovered.
* Nothing is stored on the down payment: recovery is part of each progress bill. **Voiding the latest progress bill gives back the
  recovery it took**, and the next bill recovers it again.
* A progress draft prepared before the down payment was issued (or before a price change made it the last bill) is out of date: issuing
  it is refused with the usual reload message until it is saved again, when it takes its recovery.
* A down payment bill is voided like any bill: only while it is the latest issued bill (no progress bill after it) and with no payment
  recorded; a draft is discarded with a reason.
* The project's billing shows the down payment billed, recovered and **still to recover**. What is owed counts the down payment bill
  like any other bill, paid by recorded payments.

## 4.9 Change orders (add a bullet)

* **Government projects (a warning, not a block).** When the billing terms say the client is a government agency and the budget
  baseline is locked, each change order shows how the **added work** compares with **10% of the original contract price**: the price
  increases of the approved change orders, plus the order's own increase while it is a draft or submitted. Price decreases do not reduce
  it. When it passes the limit the change order says: "Added work on this government project passes 10% of the original contract price.
  Check that the agency has approved it." Approving is still allowed: that approval is the agency's.

## 10.1 Decisions that rest on assumptions (add)

* **Down payment VAT:** the down payment is a receivable only, with no VAT split, retention or withholding; VAT is billed on the progress
  bills at the full value of the work. The accountant confirms the VAT treatment of advances (one function, `BillingCalculator.Advance`).
* **Down payment defaults:** no down payment, no VAT withheld and not a government client until someone sets them.
* **Last-bill recovery:** the bill that brings the work to the contract price recovers whatever is left of the down payment, even when a
  change order moved the price since the down payment was billed (the formula alone would leave some behind when the price fell).
* **A pending down payment draft does not block progress billing:** once a progress bill is issued the draft can only be discarded.
* **Government variation limit is advisory:** the system warns past 10% of the original price; the agency's approval is outside it.
  Only price increases of approved change orders count; a rejected or discarded order counts nothing.
* **Government limit needs a locked baseline:** without one there is no original price, and nothing is shown.
