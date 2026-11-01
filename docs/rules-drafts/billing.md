# Client billing and retention (draft for `docs/business-rules.md`)

The accounts side bills the client against the contract, holds retention, records what the client pays, and sees what is still to
collect. The system never sends a bill or collects money: **recording a payment is a person saying it was received.**

### 11.1 Billing terms

Each project has billing terms, saved the first time someone changes them; until then the standard terms apply. Changing them affects
only bills prepared afterwards: every bill keeps the terms it was prepared with.

| Term | Range | Standard |
| --- | --- | --- |
| Retention: the share of each progress bill the client holds until the project is completed | 0% to 30% | 10% |
| Creditable withholding tax: the share of the amount before VAT the client withholds and covers with a certificate | 0% to 10% | 0% |
| Payment terms: days from the billing date to the due date | 0 to 180 | 30 |

### 11.2 Progress bills

A progress bill states the **work completed to date**: the value, VAT included like the contract price, of all work billed so far
including this bill. It must be more than what the issued bills already billed, and no more than the project's contract price (a
project needs a contract price to be billed). Then, each figure rounded once, half away from zero:

* **This bill** = work completed to date − work billed before.
* **Net** = round(this bill × 10,000 ÷ (10,000 + VAT rate)); **VAT** = this bill − net (the company's VAT rate when the bill is prepared, kept on it).
* **Retention** = round(this bill × retention rate).
* **Withholding** = round(net × withholding rate).
* **The client pays** = this bill − retention − withholding.

Example (₱11,200,000.00 contract, 12% VAT, 10% retention, 2% withholding): work to date ₱2,240,000.00 gives net ₱2,000,000.00, VAT
₱240,000.00, retention ₱224,000.00, withholding ₱40,000.00, and the client pays ₱1,976,000.00. The next bill at ₱5,600,000.00 to date
bills ₱3,360,000.00: retention ₱336,000.00, withholding ₱60,000.00, the client pays ₱2,964,000.00.

### 11.3 Retention release

Once the project is **Completed**, a retention release bill gives back retention: any amount up to what is still held (withheld on
progress bills less earlier releases). It adds no VAT (the VAT was billed with the work) and nothing is retained or withheld again: the
client pays the amount.

### 11.4 Draft, issue, void

1. **Draft** (people who prepare bills): no number and nothing counts. A draft can be changed, which works its figures out again from
   what is billed now and the terms in force now, or **discarded** with a reason (it stays on record).
2. **Issue** (accountants and administrators): the bill gets the next company number (`BILL-0001`, `BILL-0002`, ...) and is locked.
   Issuing checks again that the draft's figures still follow from the project's issued bills: if another bill was issued or voided
   since the draft was prepared, it is refused and the draft must be saved again. A number refused or lost to a failed save leaves a gap.
3. **Void** (accountants and administrators, with a reason): only the project's **latest** issued bill, and only while no payment is
   recorded against it (void the payments first). The bill before it becomes the latest again.

The issued bills of a project form one unbroken chain, so **billed to date is always the latest issued bill's work completed to
date**. Two people issuing or voiding bills of the same project at the same moment can't both succeed.

### 11.5 Payments

A payment is recorded against an issued bill: an amount greater than zero and no more than what is still to collect on the bill, the
date received (not in the future), how the client paid (bank transfer, cheque, cash, other), and an optional reference and note.
People who prepare bills record payments; accountants and administrators void them with a reason, after which the amount is owed again.
What is still to collect on a bill is never negative.

### 11.6 What is owed

Never stored, always worked out: **paid** (payments not voided), **still to collect** (what the bill asks less paid), **due date**
(billing date + the bill's payment terms), **overdue** (today, in the company's time zone, is after the due date and something is still
to collect). A project's billing shows contract price, billed to date and its share of the price, collected, still to collect (and the
overdue part), and retention held. The company's receivables list every project with money to collect or retention held, overdue first.

An archived project's billing is read-only: no new bills, issuing, voiding, payments or terms until it is restored.

### Decisions that rest on assumptions

1. **Retention is released only once the project is Completed** (not, for example, at substantial completion or in instalments before).
2. **Standard terms**: 10% retention, no withholding, 30 days, until the project's terms are saved.
3. **Withholding is on the amount before VAT** of each progress bill, and none is taken on a retention release.
4. **A retention release carries no VAT**, because the VAT was billed with the work.
5. **Only the latest bill can be voided.** Correcting an older bill means voiding the later ones first, then billing again.
6. **People who prepare bills (project managers included) record payments**; only accountants and administrators issue bills and void
   bills or payments.
7. **No billing-date order is enforced** between successive bills; the chain is by issue order.
8. **Overdue starts the day after the due date.**
