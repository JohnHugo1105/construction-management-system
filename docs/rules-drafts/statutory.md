# Statutory deductions (draft for `docs/business-rules.md`, section 6)

### 6.4 Contribution and tax tables

* SSS, PhilHealth and Pag-IBIG contributions and withholding tax are worked out from **tables the company enters from the official
  schedules**. Rates and brackets change by law, so none is built into the system; until a table is entered, the agency's amounts are
  entered by hand as deductions, as before, and payroll says so in one quiet line ("SSS contributions were not calculated because no SSS
  table is set up. Enter them as deductions, or ask an accountant to set the table up.").
* A table has an agency, a name, the date it takes effect, a status (active or retired), a basis (**per month** or **per pay run**) and a
  method:
  * **Brackets**: pay ranges, each with a fixed employee and employer amount.
  * **Percentage**: an employee and an employer rate of the pay, with the pay held between a lowest and a highest figure, and optionally
    a smallest and largest amount for each share.
  * **Tax brackets** (withholding tax only): pay ranges, each with a base tax and a rate on the amount above the start of the range.
* A range runs from its start up to, but not including, its end; the first starts at ₱0.00, each starts where the one before ends, and
  only the last has no end, so every amount falls in exactly one range. A rate is at most 100%. Every problem is reported at once,
  naming its row.
* The table that applies to a payroll is the **active table of the agency with the latest date on or before the last day of the pay
  period**. Only one active table of an agency may take effect on a given date.
* Accountants and administrators add, change and retire tables; HR officers read them. **A table an approved payroll used can't be
  changed** (the payroll must stay explainable): a change in the law is a new table with the date it takes effect. Retiring a table stops
  it applying to payrolls worked out afterwards; payrolls already worked out keep their amounts.

### 6.5 Workers

* Each worker is a member of SSS, PhilHealth and Pag-IBIG, and taxed, unless recorded otherwise (for example a minimum-wage earner is
  exempt from withholding tax). These choices are seen only by people who can see pay and changed by people who manage workers.

### 6.6 Calculation

For each worker in a draft payroll, on the worker's gross pay in the run:

1. A **per-run** table is applied to the run's gross pay.
2. A **monthly** table is applied to an **estimated monthly pay = the run's gross pay × the company's pay runs a month** (1 to 4, set in
   company settings, 2 by default). The month's amount is divided by the runs a month and rounded half away from zero to the centavo, each
   run on its own (the rounding difference between runs is not carried over).
3. Percentage: the pay is held between the lowest and highest figures, each share is the pay times its rate rounded once, then held between
   that share's smallest and largest amounts. Brackets: the shares of the range the pay falls in.
4. Tax: the taxable pay is the pay less the employee's SSS, PhilHealth and Pag-IBIG contributions on the same basis (month or run); the tax
   is the range's base tax plus its rate on the amount above the range start, rounded once; never negative; nothing for an exempt worker.
5. A worker who isn't a member of a fund pays nothing to it, and the employer adds nothing.

The employee's amounts are deductions marked with their agency and table and reduce net pay. **The employer's shares are recorded on the
payroll, shown and exported, but not posted to a project**: the labor cost a payroll posts is still exactly its gross pay.

### 6.7 Limits, adjustments and recalculation

* Deductions (calculated and typed) together can never be more than the pay. When the calculated amounts would be, the payroll is not
  refused: they are taken in agency order (SSS, PhilHealth, Pag-IBIG, tax) until the pay left after typed deductions is used up, and the
  worker's line says "Deductions were limited to the pay".
* While a payroll is a draft, the person preparing it can **adjust** a calculated amount (0 or more, with a reason that stays on the
  payroll, never in the activity log) and **recalculate** the contributions and tax, which applies the current tables, worker details and
  runs a month and resets adjustments. Typed deductions are kept by both, and editing typed deductions never touches the calculated ones.
* An approved or voided payroll is final: its amounts never change, whatever happens to the tables later.
* The payroll's remittance summary shows, per agency, what was taken from the workers, what the employer adds, and the sum to remit. The
  payroll register gains a column for each employee and employer share. **Remitting is done by a person; the system pays nothing.**

### Decisions that rest on assumptions

1. **Estimated monthly pay** for monthly tables is one run's gross pay times the pay runs a month, not the worker's actual pay for the
   month. A worker paid unevenly across runs is over- or under-deducted in a run; the month is not trued up. Confirm, or ask for a
   month-end true-up.
2. **Rounding per run**: each run's share of a monthly amount is rounded on its own, so the runs of a month may differ from the month's
   amount by a centavo or two.
3. **Employer share not charged to projects**: the employer's contributions are recorded and exported but not added to project labor cost.
   Confirm whether they should be part of project cost.
4. **Limit order**: when deductions would pass the pay, SSS is protected first, then PhilHealth, Pag-IBIG and tax.
5. **One active table per agency and date**: a retired table steps aside, so a corrected table can take its date.
6. **A table used by a payroll that was approved and later voided** still counts as used and can't be changed.
7. **Pay runs a month** defaults to 2 (semi-monthly).
8. **Tax exemption** is a yes/no on the worker; partial exemptions and annual tax reconciliation are not modelled.
