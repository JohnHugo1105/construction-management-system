# Time clock: rules (draft for docs/business-rules.md)

The lead merges this note into `docs/business-rules.md` (a new section after "Needs your action", and lines in the access table and section 16)
and deletes it. API: `docs/api/timeclock.md`.

## 17. Time clock

Workers scan their own badge at a machine when they arrive and when they leave. A person reviews each project's day and confirms it, which
records the attendance. **A scan never reaches pay by itself**: payroll reads confirmed attendance only, so an hour confirmed from the time
clock is paid exactly like the same hour typed by hand. Typing attendance on the Team tab still works and is the fallback when a machine is down.

### 17.1 Badges

* A worker's badge code is 12 characters from an alphabet without look-alikes (digits 2 to 9 and the letters A to Z without I, L and O), random,
  and unique in the company. It is not derived from the employee number or anything else about the worker. The badge prints it as a Code 128
  barcode, a QR code and in groups of four (ABCD-EFGH-JKMN), with the company, the worker's name, employee number and job title for people to
  read; the symbols encode only the code.
* HR officers and administrators issue a code from the worker's page or for every active worker without one (100 at a time), and print badges
  ten to an A4 sheet at card size (85.6 x 54 mm). Only an active worker gets a code.
* **Reissuing replaces the code, and the old badge stops working at once** (a lost badge). Issuing and reissuing are in the activity log; the
  code itself never is.
* A scanned or typed code is read without regard to case, spaces or dashes.

### 17.2 Machines

* A machine (a kiosk tablet with a scanner, or a time clock that sends its scans) is registered by an administrator for one project. Its key
  (256 random bits) is shown once, when it is registered or given a new key, and stored only as a hash. A new key stops the old one at once;
  revoking stops the machine for good. Scans a machine already sent are kept.
* A machine's scans belong to the company that registered it and count for its project. A machine can send scans and do nothing else.
* Each machine may send 120 requests a minute. A batch holds at most 200 scans.

### 17.3 Scans

* A scan is the worker, the project, the machine (none for an import or a supervisor's scan), the time the machine says, the time it arrived,
  and the direction. Scans are never changed or deleted; a wrong one is removed with a reason and stays on record.
* **Work date.** A scan's work date is its date in the company's time zone, except that a scan before the company's day-start (**04:00 by
  default**) belongs to the previous date, so a night shift that ends at 2 am is one day. The work date is fixed when the scan arrives.
* **Direction.** A machine may say In or Out; otherwise the scan is the opposite of the worker's last scan that work day on the project (the
  first is In).
* **Sent twice.** The same scan id from the same machine is stored once; a second scan by the same worker on the same machine within **60
  seconds** is "already recorded" and not stored.
* **Clock.** A scan stamped more than **5 minutes** ahead of the server, or more than **7 days** old, is refused ("The time on this machine
  is wrong"). A machine that was offline sends its queued scans with their original times.
* **Codes.** An unknown code, a replaced badge and a worker who is not active are refused with the same words ("This badge isn't active. See
  your supervisor."), so the answers can't be used to find out which codes exist.
* **Not posted.** A worker not posted to the machine's project that day is told so in words; the scan is kept and the day is flagged.
* **Import.** A machine's exported file (`code,timestamp[,direction]`, at most 5,000 rows) is read under the same rules for one project; a
  time without a time zone is the company's local time. Every row is answered with its number and a reason; the file is not kept.
* **Supervisor's scan.** A person who confirms days may add a missing scan (time in or out, not in the future, with a reason) or remove a
  wrong one (with a reason). Neither is possible once the day is recorded as attendance: the attendance entry is what changes then.

### 17.4 A day's hours

For each worker, project and work date:

1. The scans are taken in time order as In, Out, In, Out. Worked time is the sum of the In-to-Out spans. Two Ins in a row, an Out with no In,
   or a last In with no Out is an **exception** (no time out / no time in), never a guess: only the complete spans count.
2. **Break.** A single span of 5 hours or more has the company's unpaid break taken off once (**60 minutes by default**; 0 turns it off). When
   the worker scanned out and back in, that gap was the break and nothing more is taken off.
3. **Rounding.** The worked time is rounded to the nearest **15 minutes by default** (half a step rounds up; 0 keeps the nearest whole
   minute), once, on the day's total, never on each scan. Hours keep two decimals (247 minutes is 4.12 h).
4. **Regular hours** are the rounded time up to the company's paid hours per day. **Overtime is the rest and is only proposed**: it is recorded
   as overtime only when the reviewer approves it for that row (overtime needs the employer's authorization).
5. **Night hours** are the worked time between 22:00 and 06:00, rounded the same way, never more than the hours worked (and, when overtime is
   not approved, never more than the regular hours recorded).
6. **Exceptions:** no time out, no time in, not posted here, and more than 16 hours worked in the day. Day types, premiums and holidays are
   payroll's (section 6) and are not worked out here.

Example: 07:02 to 17:03 is 10 h 01 min; less the 60-minute break, 9 h 01 min; rounded, 9 h: 8 h regular and 1 h overtime proposed. A
13:00 to midnight shift is 11 h, less the break 10 h: 8 regular, 2 overtime proposed, 2 night hours.

### 17.5 Confirming

* A person who may confirm (Administrator, Project Manager, HR Officer) confirms rows of a project's day, approving overtime row by row.
  Confirming creates the attendance entry **through the attendance rules** (an active worker that day, the project open, one entry per worker,
  project and day, 24 hours a day across projects, a holiday already paid as not worked), for the whole project (no sub-project), marked as
  coming from the time clock with the first in, the last out and the break kept for explanation.
* **A day that already has attendance is shown as "Already recorded" and is never overwritten**, whether it was typed or confirmed.
  Confirming again records nothing new.
* A row with a missing scan is never confirmed. "Confirm all clean rows" confirms every row of the day with no exception and no overtime
  waiting for approval; the others are listed with the reason.
* **Never automatic.** No scan, import or push creates attendance or pay on its own; a person confirms.

### 17.6 Needs your action

"Time clock scans to review" (people who confirm days: Administrator, Project Manager, HR Officer): one row per project, not archived, with
work days before today whose scans are not yet recorded as attendance: "12 workers to review for Sun 1 Nov", or across several days "15
workers' days to review, Sun 1 Nov to Tue 3 Nov", and "; 2 need a closer look" when a day's In and Out scans don't pair up or a worker wasn't
posted there. Such projects come first and are urgent ("Check scans"). No amounts. Confirming the days (or removing the stray scans) takes the
project off the list.

### 17.7 Who sees what

| | Administrator | Project Manager | HR Officer | Others |
| --- | :-: | :-: | :-: | :-: |
| Review, confirm, add or remove scans | yes | yes | yes | |
| Issue codes, print badges, import a machine's file | yes | | yes | |
| Register, rotate and revoke machines; time clock settings | yes | | | |

The review shows hours only, never pay. A badge shows the name, employee number and job title, never pay. The kiosk shows one worker's first
and last name, the direction and the time, and nothing else.

### 17.8 Company settings

Day-start (default 04:00, midnight to noon), unpaid break (default 60 minutes, 0 to 180) and rounding (default 15 minutes, 0 to 60), on the
company settings page. A change applies to days worked out afterwards; a scan keeps the work date it was given when it arrived, and confirmed
attendance keeps its hours.

## Defaults (business choices the owner has not confirmed)

* The day-start (04:00), break (60 minutes, taken only from a single span of 5 hours or more) and rounding (nearest 15 minutes, half up) are
  the spec's defaults, settable per company.
* Rounding to the nearest step (not down) can add up to 7 minutes 29 seconds a day in the worker's favour, or take as much off. Late and
  undertime deductions are not worked out (a company decision); the scans are kept so they can be added.
* A confirmed day is recorded against the whole project, not the worker's sub-project posting.
* A machine's scans count for the project it was registered for; a machine moved to another site is registered again.
* A rejected scan in a queue the kiosk resends is dropped from the tablet once the server has answered it (the worker has left the gate).
* The kiosk keeps at most 2,000 unsent scans; beyond that the oldest give way.
* An import uses the same 7-day bound as a machine: an older file is refused row by row.
* The "needs a closer look" count in Needs your action and the summary uses the scans' In/Out counts and the not-posted flag recorded when
  the scan arrived; the review page checks postings as they are now and also flags a day over 16 hours.
* The kiosk shows the worker's first and last name (not the middle name, never the employee number).
* The demo kiosk key in `TimeClockDemo` is fixed for development only.

## Not built

Biometrics (no fingerprints, face data or photos are collected or stored: they are sensitive personal data under the Data Privacy Act), GPS
or geofencing, camera scanning on the kiosk, shift rosters, late and undertime deductions, a native app, and vendor-specific time clock
protocols (no driver for any brand: a machine that can't call the HTTP route needs a gateway, or its export file is imported).

## What is proven and what is not

Proven by tests on this branch (domain unit tests, API tests on a real SQL Server, Angular specs):

* The device scheme: a hashed key compared in constant time, the tenant taken only from the device, identical 401s for unknown, revoked,
  rotated-out and malformed keys, 429 over 120 requests a minute per key, the machine route refusing a person's cookie, and a device key
  refused (401) on a sample of 16 other routes. The endpoint security test allows exactly one route with the device policy.
* Idempotency and offline replay, the clock bounds, identical answers for unknown, replaced and inactive badges, the kiosk answer carrying only
  name, direction and time (a name walk of the JSON for pay, money, code, key, photo and place words), the 60-second rule, automatic
  directions, the Manila date differing from the UTC date and the 04:00 day-start, pairing, the break, rounding with awkward minutes, the cap
  at paid hours, overtime proposed and never recorded until approved, night hours, each exception, a worker not posted.
* Confirmation through the attendance rules, never overwriting typed attendance, idempotent, "Confirm all clean rows" skipping exceptions and
  unapproved overtime, and **the same pay for the same hours typed by hand and confirmed from the clock** (a payroll run over both).
* Tenant isolation for devices, scans, workers, projects, badges and the import; role refusals for every permission boundary.
* A fixed number of SQL statements: the device push costs 10 statements for 1 scan and for 10 (the device's key, the device, its last-seen
  mark, the company, the project, the scan ids, the workers, the day's scans, the postings, one insert); the review list costs 6 for 1
  worker and for 10. EF Core batches inserts 42 rows to a command, so a push of 200 new scans adds a few insert commands (per 42 scans, not
  per scan).
* The Code 128 drawing matches an independent implementation (JsBarcode 3.11.6) for three codes, and every symbol in its table was checked
  against that implementation's table; the QR code comes from the bundled library.

Not proven:

* **No real time clock machine or scanner was ever connected.** The keyboard-style USB or Bluetooth scanner is simulated in the kiosk spec by
  key events (each character, then Enter). A physical scanner's speed, its suffix key (some send Tab, not Enter) and its handling of the QR
  code were not tested; nor was a printed badge scanned. Scanners are commonly configured to send Enter; check it when one is set up.
* The kiosk's beep, the A4 print layout and the badge size on paper were not checked on a printer or a tablet (browser specs only, not
  Android).
* No vendor protocol, no load test beyond the fixed statement counts, and the rate limit is per server process (several servers would each
  allow 120 a minute).
