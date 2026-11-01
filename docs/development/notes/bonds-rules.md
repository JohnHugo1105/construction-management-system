# Bonds register: rules note (slice J)

For the lead to merge into `docs/business-rules.md` (a new section on bonds, and the access table) and then delete. Written in the style of
that document. **Default** marks a choice the owner has not confirmed; they are collected at the end for the assumptions list (section 10).

## Bonds

A **bond** is a guarantee a bank or surety gives. Government and many private jobs require performance, advance payment and warranty
bonds, each valid until a date; a bond that lapses unnoticed is a real risk, and the register exists so nobody lets one lapse.

**A bond is not a cost and changes no money figure anywhere.** Recording, editing, extending, releasing, calling or voiding a bond never
adds to or changes a budget, a cost, a bill, a payment, retention, commitments, the dashboard or a project's cost summary. The amount is
kept for reference only. (A test asserts that the cost summary, the project and the dashboard read identically before and after bonds
are recorded, extended and released.)

### What a bond records

| Field | Rule |
| --- | --- |
| Project | Required. The project must not be archived. Fixed once recorded. |
| Direction | **Posted**: the company gave it to the client. **Received**: a subcontractor or supplier gave it to the company. Fixed once recorded. |
| Kind | Bid, Performance, Advance payment, Warranty or Other. |
| Bank or surety | Required, up to 120 characters. |
| Bond number | Required, up to 60 characters. |
| Amount | Whole centavos, more than zero, at most ₱100,000,000,000.00. The currency is the company's when the bond was recorded. |
| Issued on, Valid until | Both required, between 1 Jan 2000 and 31 Dec 2100. Valid until is the last day the bond is in force and is not before the issue date. |
| Given by | Received bonds only, optional, up to 120 characters: the subcontractor or supplier. A posted bond may not name one. |
| Note | Optional, up to 500 characters. |

### Where a bond stands

* **Active**: in force. It can be edited, extended, released, marked as called or voided.
* **Released**: returned or no longer needed, with a date and an optional note. Final.
* **Called**: the beneficiary claimed against it, with a date and a note saying what was claimed (required). Final.
* **Void**: entered by mistake, with a reason (required, up to 300 characters). It stays on record and counts nowhere.

Released and Called are final: nothing edits, extends, releases or calls them again. A wrong release or call is **voided and the bond
entered again**; that is the one step still allowed on them. A voided bond refuses every step. A release or call date is not in the
future (company calendar) and not before the bond was issued.

### Expiry

Every Active bond has an expiry state, judged on the **company's calendar day** (never the UTC day), worked out each time it is read and
never stored:

| State | When |
| --- | --- |
| Valid | More than 30 days remain. |
| Expiring soon | 30 days or fewer remain, counting the last valid day (a bond valid until today is expiring soon, not expired). |
| Expired | The valid-until date is before today. |

An Active bond that has expired **stays Active** until a person releases or voids it; it is shown as Expired and is never changed behind
anyone's back. An expired bond can still be extended. Released, Called and Void bonds have no expiry state.

### Changing a bond

* **Edit** (Active only) changes the kind, bank or surety, number, amount, dates, the firm's name and the note. The valid-until date may
  stay or move later; moving it earlier is refused ("An active bond can't be changed to an earlier date than 1 Jun 2027. Extend it to move
  the date later."). Saving details that are the same as before changes nothing and records nothing. A later date made through an edit is
  recorded with the old and new date, like an extension.
* **Extend** (Active only) moves the valid-until date to a **later** date only (the same or an earlier day is refused), with an optional
  note. Every extension is audited and is a line in the bond's history with the old and new date.
* **Release**, **Mark as called** and **Void** as above.
* An **archived project is read-only** for its bonds: nothing can be recorded, edited, extended, released, called or voided until the
  project is restored. Bonds on it can still be read, and still count in the register's totals (they still lapse).
* Every change needs the version the person opened: a stale step is refused with "reload", never applied. A repeated create with the same
  request id returns the first bond; the same id used for another project or direction is refused.
* **History.** Each bond keeps a line for every step (who, when, and the dates and note that went with it), newest first, the latest 100
  shown. The activity log records each step without notes or reasons (they stay on the record).

### Reading bonds

* The **register** lists every bond of the company: filtered by project, direction, kind, state or expiry, paged. Active bonds come first,
  the one that lapses soonest on top; then the closed ones, the most recently valid first. The screen opens on the Active bonds.
* A **project's list** has its bonds that are not voided, Active first, at most 50.
* The **totals** are the Active amounts and counts by direction (posted, received) and the number of Active bonds that have expired or
  expire within 30 days. Voided, released and called bonds are not in them.
* Every read is a fixed number of SQL statements however many bonds exist (asserted by a test).

### Who can do what

Reading needs `bonds.read` (Administrator, Project Manager, Cost Engineer, Accountant, Viewer). Recording, editing, extending, releasing,
marking as called and voiding need `bonds.manage` (Administrator, Project Manager, Accountant). A bond of another company is "not found".

### Not built, deliberately

* Swapping retention for a bond (a bond does not release or replace retention held).
* Automatic expiry emails or messages (the register and the project card show what is lapsing; nothing is sent).
* Bond premiums as a cost.

## Decisions that rest on assumptions

1. **Default:** "expiring soon" is 30 days or fewer, with the last valid day counted as still valid. A constant in the code, not a setting.
2. **Default:** the project and the direction of a bond are fixed once recorded; a bond entered against the wrong project or the wrong way
   round is voided and entered again.
3. **Default:** a released or called bond can be voided (the correction path); it cannot otherwise change.
4. **Default:** a bond that has already lapsed can still be recorded (a person may be entering an old bond); it shows as Expired. An
   expired bond can be extended to any later date, not necessarily one after today.
5. **Default:** an issue date may be any date in range (a bond can be entered a day before it is issued); only release and call dates are
   kept out of the future.
6. **Default:** no check for a repeated bond number: a double-click is covered by the request id, but two bonds with the same number can
   be recorded (a surety may reissue a number on renewal).
7. **Default:** a posted bond may not name the other party; the beneficiary is the project's client.
8. **Default:** Active bonds on an archived project stay in the register totals and the attention counts, because they still lapse.
9. The register opens on the Active bonds; closed and voided bonds are one filter away.
