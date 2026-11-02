/**
 * What the time clock endpoints send and take (see docs/api/timeclock.md). Hours come worked out by the server from the scans; the page only
 * shows them. Nothing here carries pay.
 */

export type PunchDirection = 'In' | 'Out';
export type PunchSource = 'Kiosk' | 'Terminal' | 'Import' | 'Manual';
export type TimeClockException = 'MissingTimeIn' | 'MissingTimeOut' | 'NotPostedHere' | 'TooLong';

/** One scan on the review page. `localTime` is the company's time of day, "07:02". */
export interface TimeClockPunch {
  readonly id: string;
  readonly punchedAtUtc: string;
  readonly localTime: string;
  readonly direction: PunchDirection;
  readonly source: PunchSource;
  readonly deviceName: string | null;
  readonly notPostedHere: boolean;
  /** A supervisor's reason for a scan they added. */
  readonly reason: string | null;
  readonly version: number;
}

/** A worker's day on one project, worked out from the scans. */
export interface TimeClockDayRow {
  readonly workerId: string;
  readonly workerName: string;
  readonly firstIn: string | null;
  readonly firstInLocal: string | null;
  readonly lastOut: string | null;
  readonly lastOutLocal: string | null;
  readonly breakMinutes: number;
  readonly regularHours: number;
  /** Overtime the scans show; it is recorded only when the reviewer approves it. */
  readonly proposedOvertimeHours: number;
  readonly nightHours: number;
  readonly exceptions: readonly TimeClockException[];
  /** The worker already has attendance on this project that day; the row is never confirmed over it. */
  readonly alreadyRecorded: boolean;
  readonly canConfirm: boolean;
  /** No exception and no overtime waiting: "Confirm all clean rows" takes it. */
  readonly isClean: boolean;
  readonly punches: readonly TimeClockPunch[];
}

export interface TimeClockSummary {
  readonly projectId: string;
  readonly daysWaiting: number;
  readonly rowsWaiting: number;
  readonly rowsWithExceptions: number;
  readonly earliestDate: string | null;
}

export interface TimeClockDayList {
  readonly projectId: string;
  readonly projectName: string;
  readonly projectArchived: boolean;
  readonly workDate: string;
  readonly items: readonly TimeClockDayRow[];
  readonly total: number;
  readonly page: number;
  readonly pageSize: number;
  readonly summary: TimeClockSummary;
}

export interface ConfirmRow {
  readonly workerId: string;
  readonly approveOvertime: boolean;
}

export interface ConfirmRequest {
  readonly projectId: string;
  readonly workDate: string;
  readonly rows: readonly ConfirmRow[];
  readonly allClean: boolean;
}

export interface ConfirmResult {
  readonly confirmed: readonly string[];
  readonly skipped: readonly {
    readonly workerId: string;
    readonly workerName: string;
    readonly reason: string;
  }[];
}

export interface ManualPunchRequest {
  readonly projectId: string;
  readonly workerId: string;
  /** The company's wall-clock time, "2026-11-01T17:00". */
  readonly at: string;
  readonly direction: PunchDirection;
  readonly reason: string;
  readonly clientRequestId: string;
}

export interface ImportResult {
  readonly rows: number;
  readonly accepted: number;
  readonly alreadyRecorded: number;
  readonly rejected: number;
  readonly rejectedRows: readonly { readonly row: number; readonly reason: string }[];
}

// ---- Devices -------------------------------------------------------------------------------------------------------------------

export type DeviceKind = 'Kiosk' | 'Terminal';

export interface TimeClockDevice {
  readonly id: string;
  readonly name: string;
  readonly projectId: string;
  readonly projectName: string;
  readonly kind: DeviceKind;
  readonly status: 'Active' | 'Revoked';
  readonly lastSeenAtUtc: string | null;
  readonly createdAtUtc: string;
  readonly version: number;
}

/** A device with its key: the only answer that ever carries the key. */
export interface DeviceWithKey {
  readonly device: TimeClockDevice;
  readonly key: string;
}

export interface TimeClockCapability {
  readonly status: 'Available' | 'NeedsSetup';
  readonly message: string;
}

export interface TimeClockDeviceList {
  readonly items: readonly TimeClockDevice[];
  readonly capability: TimeClockCapability;
}

// ---- Badges --------------------------------------------------------------------------------------------------------------------

export interface Badge {
  readonly workerId: string;
  readonly fullName: string;
  readonly employeeNumber: string;
  readonly jobTitle: string | null;
  /** Null until a code is issued. */
  readonly code: string | null;
}

export interface BadgeSheet {
  readonly companyName: string;
  readonly items: readonly Badge[];
  readonly activeWithoutCode: number;
}

export interface PunchCodeIssued {
  readonly workerId: string;
  readonly code: string;
  readonly reissued: boolean;
  readonly version: number;
}

export interface IssueMissingResult {
  readonly issued: number;
  readonly workerIds: readonly string[];
  readonly stillWithoutCode: number;
}

export interface ProjectOption {
  readonly id: string;
  readonly name: string;
  readonly status: string;
}

export interface WorkerOption {
  readonly id: string;
  readonly fullName: string;
  readonly employeeNumber: string;
  readonly status: string;
}

// ---- The kiosk -----------------------------------------------------------------------------------------------------------------

/** A scan as the kiosk sends it (and keeps it while offline). */
export interface KioskScan {
  readonly clientPunchId: string;
  readonly workerCode: string;
  /** The instant the badge was scanned, ISO with offset. */
  readonly punchedAt: string;
}

export type PunchOutcome = 'Accepted' | 'AlreadyRecorded' | 'Rejected';

export interface PunchResult {
  readonly clientPunchId: string | null;
  readonly outcome: PunchOutcome;
  readonly displayName: string | null;
  readonly direction: PunchDirection | null;
  readonly punchedAt: string | null;
  readonly localTime: string | null;
  readonly notPostedHere: boolean;
  readonly reasonCode: string | null;
  readonly reason: string | null;
}

export interface PushResult {
  readonly results: readonly PunchResult[];
}
