# Slice H rules note: premium pay and 13th-month pay

For the lead to merge into `docs/business-rules.md` (section 6.2 is replaced, sections 6.8 to 6.11 are new, and the defaults go into
section 10). Delete this note after merging.

## Replaces the last paragraph of 6.2 ("Not computed, on purpose")

Rest-day, holiday and night premiums and the 13th-month pay are computed (sections 6.8 to 6.11) from the **pay rules** and the
**holiday calendar** the company keeps. SSS, PhilHealth, Pag-IBIG and withholding tax are computed from the effective-dated tables
(sections 6.4 to 6.7). Advances and other deductions are entered by a person.

## 6.8 Pay rules and the holiday calendar

* The **pay rules** are one set per company. Until someone saves them, these apply (each a share of the ordinary hourly rate):

  | Day | Factor |
  | --- | --- |
  | Rest day | 130% |
  | Special (non-working) day | 130% |
  | Special day that is also the rest day | 150% |
  | Regular holiday | 200% |
  | Regular holiday that is also the rest day | 260% |
  | Overtime on any of these days | the day's factor x 130% (the "overtime extra" is 30%) |
  | Night differential | 10% of the day's rate for each night hour |

  A day factor is between 100% and 500%; the overtime extra and the night differential are between 0% and 100%. Overtime on an
  **ordinary** day keeps using the company's overtime multiple (125% by default) from company settings; there is no second one.
* Administrators, accountants and HR officers keep the rules and the calendar; everyone who can see payroll reads them. Every change
  is in the activity log (the factors as percentages, the holiday's date and name; never a pay figure).
* A change applies to payrolls worked out or **recalculated** afterwards. An approved payroll keeps its amounts.
* The **holiday calendar** has at most one holiday per date: a date, a name (up to 80 characters) and a type, **regular holiday** or
  **special day**. No real holiday is built in; the people who keep payroll enter the year's proclaimed holidays.
* A holiday on a date that a non-void **approved** payroll covers can't have its type changed or be removed ("A payroll that covers this
  date was approved. Void it before changing this holiday."), and no holiday can be added on such a date ("... Void it before adding a
  holiday on this date."). Renaming it is allowed: the name changes no pay.
* A payroll for a year in which the company has entered no holiday at all says so in one line ("No holidays are entered for 2026. Add
  them under Payroll, Holidays if any fall in this period."). It is never a block.

## 6.9 Rest day and day type

* A worker's **rest day** is a day of the week, or none. A new worker has **none** (every day is an ordinary day for pay); HR sets
  Sunday, or another day, for crews that rest on it. People who manage workers set it on the hire form and the profile.
* The **day type** of an attendance date for a worker is decided by the server from the date's holiday (if any) and the worker's rest
  day: ordinary, rest day, special day, special day on the rest day, regular holiday, regular holiday on the rest day. Attendance shows
  it as a chip ("Rest day", "Regular holiday"); an ordinary day shows nothing. The chip follows the calendar and rest day as they are
  now; an approved payroll keeps what it paid.

## 6.10 Pay of an attendance entry

Each figure is worked out per entry at the rate in force on the work date, multiplied first and divided once, last, and rounded once
half away from zero; a worker's pay and each project's share are sums of those rounded figures, so they always add up.

* **Regular pay** (the basic pay) = hourly rate x regular hours (a daily rate is divided by the paid hours a day).
* **Premium** = hourly rate x regular hours x day factor, rounded, minus the regular pay (zero on an ordinary day).
* **Overtime** = hourly rate x overtime hours x the company overtime multiple on an ordinary day; on any other day hourly rate x
  overtime hours x day factor x (100% + the overtime extra).
* **Night differential** = hourly rate x night hours x day factor x the night rate. **Night hours** (0 up to the hours worked that day,
  2 decimals) are typed on the attendance entry by the person who knows the shift; the system does not work shifts out.
* **Unworked regular holiday.** A worker paid **by the day**, active and hired on the date, **posted** to a project that day, with **no
  attendance** anywhere that day, and the date not their rest day, is paid **one day at the daily rate** for a regular holiday in the pay
  period, charged to the project (and sub-project) they were posted to. Hourly-rated workers are paid for hours worked only. A special
  day not worked is not paid.
* A worker is paid for an unworked holiday by **one standing payroll only** (the database enforces it); voiding that payroll frees the
  day. Recording work on a day a standing payroll already paid as an unworked holiday is refused ("... was paid for 7 Oct 2026 as a
  regular holiday not worked. Void that payroll before recording work on this day.").
* Worked example (800.00 a day, 8 paid hours, the default rules, Sunday the worker's rest day): 8 h + 2 h overtime with 2 night hours on
  the Sunday = 800.00 + 240.00 premium + 338.00 overtime (100.00 x 2 x 130% x 130%) + 26.00 night (100.00 x 2 x 130% x 10%) = 1,404.00;
  8 h on a regular holiday = 1,600.00. An awkward rate: 1,000.01 a day, 0.75 h regular + 0.75 h overtime + 0.75 night hours on the
  rest day = 93.75 + 28.13 + 158.44 + 12.19.
* The payroll shows each worker's gross folded into Regular pay, Overtime and premium, Night differential and Holiday pay (a part that
  is zero is left out); the register (CSV) has a column for each.

## 6.11 Recalculating and approving a draft

* **Recalculate** on a draft works the pay out again from the pay rules, holidays, rest days, postings and the company's paid hours and
  overtime multiple as they are now, then the contributions and tax (6.7). Typed deductions are kept; it is refused when they would be
  more than the new pay, or when a worker who would leave the payroll has typed deductions.
* **Approving** works the pay out again (with the paid hours and overtime multiple the draft was made with) and is refused when it no
  longer matches: "The pay rules, holidays, or a worker's rest day or posting changed after this payroll was worked out. Recalculate the
  payroll, then approve." An approved payroll can always be explained from its rules and calendar.

## 6.12 13th-month pay

* A **13th-month payroll** is prepared for a calendar year (this year at any time, for example an early payout when a worker leaves;
  never a future year). Each worker's amount is **one twelfth of their basic pay for the year**, rounded once half away from zero.
* **Basic pay** is the plain-rate pay for regular hours (hours x rate, no day factor) of the worker's **approved** regular payrolls whose
  pay period **ends** in that year. Overtime, premiums, night differential and holiday pay are not basic pay. The basic pay is stored on
  each project share of each payroll, so the year's sum is exact.
* A worker with no basic pay in the year is not included. A worker is in **one standing 13th-month payroll per year** (the database
  enforces it); a second one for the year includes only workers not already in one, and voiding one frees its workers.
* **No contributions and no tax** are taken; typed deductions still work.
* Approving posts the cost to the projects like any payroll, split in proportion to the worker's basic pay on each project over the year
  by the **largest remainder** (the shares add up to the amount exactly; a tie goes to the earlier project). The cost is labor, described
  "13th-month pay 2026", dated the year's last day or, when paid within the year, the day it is approved. Voiding reverses it.
* Approving is refused when the year's approved payrolls changed since the draft was worked out ("The approved payrolls of 2026 changed
  after this 13th-month pay was worked out. Recalculate it, then approve.").
* Prepared with payroll.run (HR officers, accountants, administrators), approved with payroll.approve (accountants, administrators);
  an approved one is voided with payroll.approve, a draft by whoever prepares payroll (as for any payroll).
* Worked example: 2,400.00 of basic pay (1,600.00 on Tower A, 800.00 on Warehouse B) gives 200.00, charged 133.33 and 66.67.

## Decisions that rest on assumptions (for section 10)

* Default factors as in 6.8 (rest day 130%, special day 130%, special day on the rest day 150%, regular holiday 200%, regular holiday on
  the rest day 260%, overtime on those days x 130%, night differential 10%).
* A new worker has **no** rest day (defaulting to Sunday would pay a premium nobody agreed).
* The night differential is computed on the day's regular-hour rate, also for night hours that are overtime (this slightly understates
  night overtime).
* An unworked regular holiday is paid without checking that the worker was present on the day before; HR marks a worker away or
  suspended through the status history when the day should not be paid. Hourly-rated workers get none.
* Renaming a holiday an approved payroll covers is allowed; changing its type, removing it, or adding a holiday on such a date is not.
* 13th-month basic pay counts the approved regular payrolls whose pay period ends in the year (a payroll crossing the year end counts
  in its last day's year).
* 13th-month pay takes no tax: the tax exemption up to the legal ceiling is not tracked, so any tax on an excess is a typed deduction.
* A 13th-month draft is voided by whoever prepares payroll, like any draft; an approved one needs payroll.approve.

## For the lead

* `CostEntry.RecordSystem` ignores `sourceName` for a payroll cost (it always writes "Payroll"). The 13th-month posting passes
  "13th-month pay"; to show it as the source, change the payroll arm of that switch to
  `CostOrigin.Payroll => string.IsNullOrWhiteSpace(sourceName) ? "Payroll" : sourceName.Trim()` (one line, `CostEntry.cs`).
* Demo data (section 7 of the spec): `PayRules` defaults need no row; add illustrative `CompanyHoliday` rows on the Riverside dates and,
  if wanted, a rest day of Sunday on the crew (`Worker.SetRestDay`).
