# Progress and earned value (draft for `docs/business-rules.md`)

The retired desktop app had no progress at all: cost was only compared with the budget ("how much have we spent"), never with the work
done ("what did the money buy"). A project could be 90% spent and 40% built and nothing said so. This section adds progress per budget
line and the earned-value figures built on it. Amounts are in minor units; every percentage and index is in basis points (10,000 is
100% or 1.00).

## 1. Recording progress

* **Progress is a statement, and statements are only added.** A progress entry says how far along one budget line is (0% to 100%) on
  a business date, who said so, when it was saved, and an optional note. An entry is never edited or deleted; a correction is a newer
  entry. A line's **current percent** is its latest entry: the latest date, then the latest time saved. A line never updated is at 0%.
* **Dates.** An entry can't be dated in the future (company time zone), and an update can't be dated before the latest entry of a line
  it changes, so the latest entry is always the one that counts. The same day is allowed: the later save wins.
* **Going down needs a reason.** A percent lower than the line's current one needs a note (rework after an inspection, a part redone).
  Without one the update is refused on that line.
* **A batch, all or nothing.** People record several lines in one go (up to 200). Every problem is reported at once and nothing is
  recorded unless the whole batch is accepted. A line whose percent did not change is skipped, and a batch with nothing that changed is
  an error rather than an empty save.
* **Only active budget lines of the project.** A removed line, another project's line and another company's line are refused. A project
  that is archived takes no progress (restore it first); one that is on hold or completed does.
* **Repeated clicks are safe** (rule in section 1): a repeated request id returns the first result and records nothing more, even when
  two copies arrive at the same moment.
* **Stale figures.** When the person tells the system which percent they were looking at and someone saved a different one meanwhile,
  the update is refused and they are asked to reload, so two people updating the same line never overwrite each other unnoticed.
* **Access.** Administrators, project managers and cost engineers record progress. Those and accountants and viewers read it. Warehouse
  keepers and HR officers have no access. Progress changes nothing on the dashboard or the project cost summary.

## 2. Earned value

All figures use the company's comparison basis (section 4.7: with or without VAT), except the forecast margin, which always uses amounts
with VAT. Every division is done last and rounded once, half away from zero. A line's earned value is rounded on its own and the project
adds the rounded figures, so a roll-up always equals the sum of what is shown.

| Figure | Meaning | Rule |
| --- | --- | --- |
| Budget at completion (BAC) | What the work was planned to cost | The sum of the active budget lines |
| Earned value (EV) | What the work done is worth | Per line: budget x percent complete. The sum over lines |
| Actual cost (AC) | What was spent | Posted cost dated on or before the date: the same figure as the dashboard when the date is today |
| Planned value (PV) | What should be done by the date | BAC x the share of time from the project's start date to its target end date that has passed (0 before the start, 100% on or after the end). None without a target end date |
| Cost variance (CV) | Work done minus money spent | EV - AC. Negative: the work cost more than it was worth |
| Schedule variance (SV) | Work done minus work planned | EV - PV. Negative: behind. None without PV |
| Cost index (CPI) | Work earned for each peso spent | EV / AC. None while nothing is spent |
| Schedule index (SPI) | Work done against the plan | EV / PV. None without PV, or while nothing is planned yet |
| Forecast at completion (EAC) | What the project is expected to cost | BAC while nothing is earned or spent; otherwise BAC x AC / EV (the cost index method: the rest of the work costs what the work so far did) |
| To complete (ETC) | What the rest is expected to cost | EAC - AC, never below zero |
| Variance at completion (VAC) | Budget minus forecast | BAC - EAC. Negative: a forecast overrun |
| Percent complete | How much of the budget is earned | EV / BAC. None when nothing is budgeted |
| Forecast margin | What the company expects to keep | Contract price (with VAT) minus EAC worked out on amounts with VAT. None without a contract price |

* On the "excluding VAT" basis the forecast margin is computed from a second, with-VAT calculation (budget, earned value and cost with
  VAT), never by adding VAT to the net forecast.
* **Date.** The report can be read as of an earlier date: each line's percent is its latest entry on or before it and the cost is what
  was dated on or before it. The budget is always the budget as it stands now, because budget lines keep no history.
* **Ceiling.** A forecast above 10^18 centavos (a project with a great deal spent and almost nothing earned) is shown as that ceiling
  instead of failing the report; an index above the largest whole number is shown as that number.
* The Progress tab says it in words: "Spent ₱x for work worth ₱y", "Each peso spent is earning 93 centavos of work", "Behind schedule:
  the work done is 79% of what was planned by today". The server sends the figures; the screen only words them.

## Decisions that rest on assumptions

1. **The date can't go backwards for a line** (an update can't be dated before the line's latest entry). The alternative, allowing
   backfilled history, makes "the latest entry" ambiguous. Entering history for a project that started before the system is done in date
   order.
2. **Planned value is linear in time** from the start date to the target end date. Real plans are rarely linear (concrete before finishes);
   there is no schedule or baseline in the system to measure against. Confirm before using the schedule figures for decisions.
3. **The forecast uses the cost index method** (EAC = BAC x AC / EV), which assumes the rest of the work costs what the work so far did.
   Other methods (the budget for the rest, or a combined cost and schedule index) give different forecasts; the choice is one function.
4. **Spending is not matched to lines.** Actual cost is the project's posted cost, not the cost of each line, so earned value is judged
   for the project and per division of the budget, not line by line against spending.
5. **Committed cost is not in the forecast.** The forecast is earned value based; money promised in purchase orders and subcontracts
   appears on the dashboard and the Budget tab as committed cost, not here.
6. **Percent complete is whatever the person says.** There is no measurement from quantities installed; a project manager's judgement
   is the input, with the history of who said what and when.
7. **A project on hold or completed still accepts progress** (a completed project may be corrected to 100%); only archiving closes it.
8. **Accountants and viewers read progress** (as they read costs); they cannot record it.
