# Schedule: rules note for business-rules.md (slice S)

The lead merges this into `docs/business-rules.md` (a new section after "11. Progress and earned value", plus the rows below in section 2
"Access", the planned-value row of the 11.2 table and the "Needs your action" table) and then deletes this file. Rules marked
**Default** are choices the owner has not confirmed.

## The schedule (proposed section 11.3, or a new section after 11)

Until now the planned value on the Progress tab assumed the work is spread evenly between the project's start date and its target end
date. Real work is not even: foundations take money early, finishes late. The schedule lets planners say when each part of the work is
planned to happen, and the planned value follows it. It is activities with dates, not a network: **there are no dependencies between
activities, no critical path and no resource levelling.**

* **An activity** belongs to one project and has a name (up to 120 characters), a planned start and a planned finish (business dates,
  the finish not before the start, both between 2000 and 2100), and the budget lines whose work it covers (up to 500).
* **Weight.** An activity's weight is the sum of its linked active lines' budgets on the company's comparison basis (section 4.7), the
  same line amounts the Progress tab uses. An activity with no line is listed and marked "Link budget lines to include it in the plan";
  it weighs nothing and plans nothing.
* **A budget line belongs to at most one activity (Default).** Linking a line that another active activity holds is refused, naming that
  activity ("\"CHB walls\" is already in the activity \"Masonry walls\"."). To split a line's work across activities, split the budget
  line. A removed activity frees its lines. A removed budget line drops out of every figure.
* **Days.** Both the start and the finish are working days of the activity: 1 Mar to 10 Mar is 10 days; a one-day activity starts and
  finishes on the same day. "By a date" means by the end of that day.
* **Planned value at a date** = for each activity, weight x (planned days passed by the end of the date / planned days), the share clamped
  to 0 before the start and 1 from the finish on, multiplied first, divided last and rounded once (half away from zero); the project adds
  the rounded activity figures. Example: an activity of ₱1,000,000.00 over 10 days has ₱300,000.00 planned by the end of its third day,
  ₱1,000,000.00 from its finish on.
* **Percent complete** of an activity = the budget-weighted average of its linked lines' current percent (rounded once); a plain average
  when every linked line has a zero budget. It reads 100% only when every linked line is at 100%: an average that would round up to 100%
  while a line is still open shows as 99.99%. Read-only: progress is still recorded per line on the Progress tab.
* **Behind.** An activity is behind when its planned finish is before today (company time zone) and it is below 100%. An activity with no
  line is never "behind"; it is flagged to have lines linked instead. The screen says "Behind", "On track", "Done" or "No budget lines"
  in words with an icon, never colour alone.
* **Baseline.** "Set baseline" copies every active activity's planned dates to its baseline dates. It asks first (it says what freezing
  means: later date changes show as days later or earlier than the baseline), and it can be set again; each time replaces the baseline
  and writes an audit line. It is refused when the schedule changed since the person read it (an activity added, changed or removed), so
  they reload and confirm. An activity added after the baseline has no baseline dates and plans nothing in the baseline planned value.
* **Slip** = planned finish minus baseline finish, in days: positive is later than the baseline ("Finishes 7 days later than the
  baseline"), negative earlier.
* **Baseline planned value** is the planned value worked out from the baseline dates (same rule, same weights).
* **Removing an activity** needs a reason (up to 300 characters) that stays on its record, and is audited. A schedule is planning data, not
  a financial record, so the activity is removed rather than voided; it can't be changed or put back.
* **Archived projects.** The schedule can be read but not changed (restore the project first).
* **Changes** carry the version the person opened (a stale one is refused and they reload), and an add carries a request id so a double
  click adds one activity.
* **Audit lines** name the activity, its dates and how many lines, never an amount.

### Where the numbers land

When at least one activity has weight, the Progress tab's planned value is the schedule's planned value, and the schedule variance
(EV - PV), the schedule index (EV / PV, none while nothing is planned) and the planned percent (PV / BAC) follow it; the summary then says
"of what the schedule planned by today". With no activity with weight, the straight line over the project's dates stays. Forecasts use
the cost index (section 11.2) and never the planned value, so a schedule changes no forecast, no margin and nothing on the dashboard.
Replace the Planned value row of the 11.2 table with:

| Planned value (PV) | What should be done by the date | From the schedule when an activity covers budget lines (11.3); otherwise BAC x the share of time from the project's start date to its target end date that has passed (0 before the start, 100% on or after the end). None without either |

### The S-curve

The Schedule view of the Progress tab draws cumulative planned value, baseline planned value, earned value and actual cost (posted cost)
over time, from the earliest start (planned or baseline) to the latest finish. Points are weekly, or every 2, 3 or more weeks when weekly
points would be more than 119, so there are never more than 120 (a two-year plan has about 107). Earned value and actual cost stop at
today. **The point for today equals the Progress tab's planned value, earned value and spent amount**, because both are worked out from the
same line amounts (an automated test pins it). The chart is drawn in the app with a sentence that says what it shows and the same figures
as a table.

## Access (rows for the section 2 table)

| Permission | Administrator | Project Manager | Cost Engineer | Accountant | Warehouse Keeper | HR Officer | Viewer |
| --- | :-: | :-: | :-: | :-: | :-: | :-: | :-: |
| See the schedule and the S-curve (they show budget amounts) | yes | yes | yes | yes | | | yes |
| Plan the schedule: add, change and remove activities, set the baseline | yes | yes | yes | | | | |

The schedule readers are exactly the people who see budget and costs (`costs.read`); a test pins it.

## "Needs your action" row

| Group | Who receives it (permission) | What it counts | Amount shown |
| --- | --- | --- | --- |
| Schedule activities past their planned finish | Plan the schedule: Administrator, Project Manager, Cost Engineer | Active projects with at least one activity whose planned finish is before today while a linked active budget line is below 100%; one row per project ("3 activities are past their planned finish"), the most activities first, then the earliest finish. The row opens the project's Progress tab | None |

## Defaults chosen in this slice

* A budget line belongs to at most one activity (the spec's default).
* Removing an activity, not voiding it, with a reason kept on the record (the spec's wording).
* Activities with no budget line are kept and flagged, not refused.
* At most 500 budget lines per activity; names up to 120 characters; removal reasons up to 300 characters; dates between 2000 and 2100.
* The baseline request names the activities and versions the person saw; any difference is a conflict rather than a silent baseline of
  a schedule they did not see.
* The S-curve's first point counts progress and cost dated before the plan began (so the curve starts where the project stands).
* On screen: the Schedule view lives on the Progress tab behind an "Update progress / Schedule" switch kept in the address
  (`?view=schedule`); the timeline bars appear only when the view is at least 40rem wide, phones get the plain list with the same facts in
  words; the first 30 activities are drawn, then "Show more activities".
