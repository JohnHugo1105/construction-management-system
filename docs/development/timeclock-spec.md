# Time clock: workers scan themselves in and out at a scanning machine

On Philippine sites each worker scans their own ID badge (a barcode or a QR code) at a machine when they arrive and when they leave. This
slice takes those scans in, turns a day's scans into a proposed attendance entry, and lets a supervisor confirm it. **Payroll is unchanged:
it reads confirmed attendance, exactly as today; a scan never reaches pay directly.** Typing attendance by hand still works and is the
fallback when a machine is down.

Read first: `AGENTS.md`, `docs/development/philippine-practice-spec.md` section 1 (ground rules, environment, report format),
`docs/development/commercial-control-spec.md` sections 1, 3 and 4 (conventions), `docs/development/coordination-spec.md` sections 4 (the
capability model) and 9 (how a feature adds a group to Needs your action), `docs/business-rules.md` section 6 (attendance, pay, day types,
night hours) and the access section. Study the attendance code (`ConstructOPX.Domain/Features/Payroll/AttendanceEntry.cs`, the attendance
service and endpoints, the project Team tab and its attendance sheet in `web/src/app/features/workforce`) and the **bonds register** for the
new-feature patterns.

## 1. What is built, and the assumption about the machines

Two ways in, one set of rules behind them:

* **A kiosk page** (`/kiosk`) for a tablet or PC at the gate with an ordinary **USB or Bluetooth barcode/QR scanner**. Those scanners type the
  code and press Enter, like a keyboard, so the page just listens. This is the most common, cheapest setup and the only one that can be tested here.
* **Machine integration** for dedicated time clocks and terminals: an HTTP push route a machine or its gateway can call, and a CSV import for machines that
  export a file. **Vendor-specific protocols are not built** (no driver for any particular brand). Say so in the docs: it needs the machine in hand.

Not built, deliberately: biometrics (no fingerprints, face data or photos are stored: they are sensitive personal data under the Data Privacy Act), GPS or
geofencing, camera scanning on the kiosk, shift rosters, late and undertime deductions (the arithmetic is a company decision; punches are kept so it can
be added), and a native app.

## 2. Data

* `Worker.PunchCode`: 12 characters from an alphabet without look-alikes (no 0/O, 1/I/L), random, unique per company, null until issued. **The badge encodes
  only this code**, never the employee number or anything about pay. HR issues it from the worker's page; **reissuing replaces it and the old badge stops
  working at once** (a lost badge). Both actions are audited (no code in the audit summary).
* `TimeClockDevice` (aggregate): name (up to 80), default project, kind (`Kiosk`, `Terminal`), `Active`/`Revoked`, a **hash** of its device key (SHA-256 of a
  256-bit random key; the key is shown once when the device is registered or its key rotated, then never again), last seen, `Version`. Registered, rotated and
  revoked in Admin (`settings.manage`).
* `TimePunch` (append-only): worker, project, device (or none), the time the machine says (`PunchedAtUtc`), the time the server received it, direction
  (`In`, `Out`, or `Auto` resolved at receipt to the opposite of the worker's last punch that work date), source (`Kiosk`, `Terminal`, `Import`, `Manual`), a
  `ClientPunchId` (unique per device: the idempotency key), flags, and voided with a reason (never deleted). A `Manual` punch is a supervisor adding a missing scan,
  with a reason.
* Where a confirmed day lands: an `AttendanceEntry` created through the **existing attendance rules**, marked `Source = TimeClock` with the first in, the last out and the
  break taken kept for explanation. Add what you need to the attendance entry (owned by this slice) without changing how pay is worked out.

## 3. Rules (company time zone, `Asia/Manila` by default)

* **Work date.** A punch's work date is its local date, except punches before the company's day-start (**Default 04:00**, a company setting) belong to the previous
  date, so a night shift that ends at 2 am is one day.
* **Sequence.** A worker's punches for a work date are taken in time order as In, Out, In, Out. Worked time is the sum of the In-to-Out intervals. Two Ins in a row, an
  Out with no In, or a final In with no Out are **exceptions to review**, not guesses.
* **Duplicate scans.** A second scan by the same worker within **60 seconds** of the last (the same machine) is answered "already recorded" and not stored.
* **Break.** A single In-to-Out span of at least 5 hours has the company's unpaid break taken off once (**Default 60 minutes**, a company setting; 0 turns it off).
  When the worker scanned out and in for a break, the gap is the break and nothing more is taken off.
* **Rounding.** The day's worked minutes are rounded to the nearest **15 minutes** (**Default**, a company setting; 0 turns it off), once, on the total, not on each punch.
* **Hours.** Regular hours = the rounded worked time up to the company's paid hours per day (the existing setting). **Overtime = the rest, and it is proposed, not paid:**
  it becomes attendance overtime only when the reviewer approves it for that row (overtime needs the employer's authorization). **Night hours** = the worked time that falls
  between 22:00 and 06:00, capped at the hours worked, rounded the same way. Day types, premiums and holidays are the payroll's business and are not touched here.
* **Exceptions** (flags): missing time-out, missing time-in, **not posted here** (the worker is not posted to this project that day: the kiosk says so in words and the
  scan is still kept), clock skew (the machine's time is more than 5 minutes ahead of the server, or more than 7 days behind: such a punch is rejected with a plain reason),
  and too long (more than 16 hours in one day).
* **Confirming a day** (`timeclock.review`) creates the attendance entry through the existing attendance service, so every attendance rule applies (active worker, posting,
  project open, one entry per worker per project per day, archived projects refused). **A day that already has an attendance entry is shown as "Already recorded" and is never
  overwritten.** Confirming is idempotent. "Confirm all clean rows" confirms every row of the day with no exception and no unapproved overtime.
* **Never automatic.** No scan, import or push creates attendance or pay on its own; a person confirms.

## 4. Routes

* `POST /api/timeclock/punches`: authenticated by the **device key** (header `X-Device-Key`) with its own authentication scheme and policy, **not** a user cookie (no
  anti-forgery token: it is not a browser session). Body: up to 200 punches `{ clientPunchId, workerCode, punchedAt, direction? }`. Each answers `Accepted` (with the worker's
  display name, the resolved direction and the time, for the kiosk's feedback), `AlreadyRecorded`, or `Rejected` with a plain reason (unknown code, revoked badge, clock skew,
  worker not active). The tenant comes **only** from the key. A per-device rate limit (**120 requests a minute**). An unknown, revoked or malformed key is 401 with no detail.
* `POST /api/timeclock/import` (`timeclock.manage`, CSV, at most 5,000 rows, columns `code,timestamp[,direction]`): returns counts and the rejected rows with reasons; the file is not stored.
* Review (`timeclock.review`): `GET /api/timeclock/days?projectId&date&page` (one row per worker per work date with punches, computed hours, proposed overtime, exceptions,
  and whether the day is already recorded), `GET /api/timeclock/summary?projectId` (how many days wait), `POST /api/timeclock/days/confirm` (rows and the overtime approvals),
  `POST /api/timeclock/punches/manual`, `POST /api/timeclock/punches/{id}/void` (reason).
* Badges and codes (`timeclock.manage`): `POST /api/workers/{id}/punch-code` (issue or reissue), `GET /api/timeclock/badges?workerIds` (name, employee number, job title, the code;
  never a pay figure; at most 100 workers).
* Devices (`settings.manage`): `GET/POST /api/timeclock/devices`, `POST .../{id}/rotate-key`, `POST .../{id}/revoke`.
* Company settings: day-start, unpaid break minutes and rounding minutes are added to the company settings (the existing settings page and API; `settings.manage`).

## 5. Screens

* **Kiosk** (`/kiosk`, outside the signed-in shell, no menu): the first visit asks for the device key (typed or pasted) and keeps it on this device only, with a warning to keep the
  screen at the gate; then a full-screen view with one input that stays focused for the scanner and a text box to type a code if a badge will not scan. A scan answers within a second with the
  worker's **name in large type**, "Time in 7:02 am" or "Time out 5:03 pm" (words and an icon, a colour, and an optional beep: never colour alone), or a plain refusal ("This badge isn't
  active. See your supervisor."). **If the server cannot be reached the scan is kept on the device with its real time and a visible "N scans waiting to send", and it sends itself every 30 seconds**
  (a timer is fine here); the page shows nothing but the scan and the time. It shows no other worker data. Plain words, big targets, works at tablet and phone size.
* **Review** (`/time-clock`, reachable from the Workers area and from the project Team tab's attendance section): pick a project and a date (default: the earliest date waiting); a row per
  worker with in, out, break, hours, proposed overtime (a checkbox to approve), exception chips with words, "Already recorded" muted; "Confirm" per row and "Confirm all clean rows" as the one primary action;
  add a missing punch with a reason; void a punch with a reason.
* **Badges** (`/time-clock/badges`): choose workers (or "everyone without a code"), see the cards (company name, worker name, employee number, job title, **a Code 128 barcode and a QR code of the
  code**), print on card-size paper (the page's print layout, 85.6 x 54 mm per card, a sheet of ten per A4). Draw the Code 128 barcode yourself in SVG (it is a small, well-defined encoding; it is the
  code scanners read most widely); for the QR code use one small, MIT-licensed, pinned library (the one new dependency allowed in this slice; no network calls, bundled, exact version); say in your report which
  and why. A worker's page gets "Issue badge code" and "Reissue badge code" (a confirmation that says the old badge stops working).
* **Admin > Time clocks** (`settings.manage`): register a device (shows the key once with a copy button), rotate, revoke, last seen, and the capability line "Time clock: Available / Needs setup (no device has
  connected yet)"; add the capability to the capability registry from the coordination slice **only if it exists in your tree; otherwise show the status on this page and the lead will register it**.

## 6. Needs your action

A group `time-clock-to-review` for `timeclock.review` holders (Administrator, Project Manager, HR Officer), implemented as `TimeClockNeedsActionSource` (class name ends `NeedsActionSource`, in your
Infrastructure folder, implementing `INeedsActionSource`): one row per project with work dates before today that have unconfirmed punches ("12 workers to review for 1 Nov"), exceptions first/urgent, no
amounts, at most two statements. The web side needs its key, heading and link (`/time-clock`) added to `needs-action.models.ts` and `needs-action-presentation.ts` and the permission to the nav list in
`permissions.ts`: those edits are yours in this slice.

## 7. Security and privacy (an independent review follows)

* Device keys: 256-bit random, shown once, stored only as a hash, compared in constant time, per-device rate limit, revoke is immediate, one device can never read or write another company's data.
* The kiosk route needs no user and shows one worker's name at a time: keep it to the name (no code, no employee number, no project list, no pay, no photo).
* Punch codes are random, not derived from anything; reissue invalidates the old one; unknown codes answer the same as revoked ones (no enumeration of which codes exist).
* The import and the push accept bounded input (rows, size, timestamp ranges), never store the file, and report per-row reasons without echoing other workers' data.
* Pay data never appears in any punch, badge, kiosk or review response; punch review needs `timeclock.review`, badges and imports `timeclock.manage`.
* No biometrics, photos or GPS are collected or stored. Say so in the docs.

## 8. What the lead does

Permissions `timeclock.review` (Administrator, Project Manager, HR Officer) and `timeclock.manage` (Administrator, HR Officer) and their web constants exist already; remove them from
`ExpectedUnusedWhileBuilding` as your routes land (the one allowed edit). `app.routes.ts` gets the `kiosk`, `time-clock` and `time-clock/badges` routes from you (that is the one allowed edit there). The
lead regenerates the single migration, wires your demo seed (`TimeClockDemo.SeedAsync`: one registered kiosk device, badge codes for the demo crew, and a day of punches with one exception), updates
`NeedsActionApiTests` group sets, and merges your rules note into `docs/business-rules.md`.
