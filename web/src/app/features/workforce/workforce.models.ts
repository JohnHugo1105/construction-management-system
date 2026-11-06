/** A worker's state, as the API writes it. */
export type WorkerStatus =
  'Active' | 'OnLeave' | 'Suspended' | 'Resigned' | 'Retired' | 'Terminated';

/** Whether a pay rate is for a day or for an hour (the API writes "Daily" and "Hourly"). */
export type RateUnit = 'Daily' | 'Hourly';

/** Why a pay rate changed. "Initial" is the rate a worker was hired at and is never chosen by a person. */
export type RateCategory = 'Initial' | 'Promotion' | 'Demotion' | 'RateAdjustment';

/** One row of the worker list. It never carries pay. */
export interface WorkerListItem {
  readonly id: string;
  readonly employeeNumber: string;
  readonly fullName: string;
  readonly status: WorkerStatus;
  readonly designation: string | null;
  readonly currentProjectId: string | null;
  readonly currentProjectName: string | null;
  /** A business date, "2026-11-02". */
  readonly dateHired: string;
  readonly version: number;
}

/** A person in a picker. */
export interface WorkerLookup {
  readonly id: string;
  readonly fullName: string;
  readonly employeeNumber: string;
  readonly status: WorkerStatus;
}

/** One line of pay history. The API returns these only to people who may see pay. */
export interface WorkerRate {
  readonly id: string;
  readonly designation: string;
  /** Minor units (centavos). */
  readonly rateMinor: number;
  readonly rateUnit: RateUnit;
  readonly category: RateCategory;
  readonly effectiveFrom: string;
  readonly reason: string | null;
  readonly recordedAtUtc: string;
}

export interface WorkerAssignment {
  readonly id: string;
  readonly projectId: string;
  readonly projectName: string;
  readonly subProjectId: string | null;
  readonly subProjectName: string | null;
  readonly startDate: string;
  readonly endDate: string | null;
  readonly isCurrent: boolean;
}

export interface WorkerCase {
  readonly id: string;
  readonly caseDate: string;
  readonly title: string;
  readonly remarks: string | null;
  readonly projectId: string | null;
  readonly projectName: string | null;
  readonly recordedAtUtc: string;
}

/**
 * A whole worker. `currentRateMinor` and `currentRateUnit` are null and `rates` is empty for anyone without the payroll.read
 * permission: that is how the screen knows not to show pay.
 */
export interface WorkerDetail {
  readonly id: string;
  readonly employeeNumber: string;
  readonly firstName: string;
  readonly middleName: string | null;
  readonly lastName: string;
  readonly fullName: string;
  readonly dateOfBirth: string | null;
  readonly dateHired: string;
  readonly maritalStatus: string | null;
  readonly spouseName: string | null;
  readonly religion: string | null;
  readonly educationalAttainment: string | null;
  readonly presentAddress: string | null;
  readonly provincialAddress: string | null;
  readonly contactNumber: string | null;
  readonly reference: string | null;
  readonly status: WorkerStatus;
  readonly statusChangedOn: string | null;
  readonly statusReason: string | null;
  readonly designation: string | null;
  readonly currentRateMinor: number | null;
  readonly currentRateUnit: RateUnit | null;
  readonly currentAssignment: WorkerAssignment | null;
  readonly assignments: readonly WorkerAssignment[];
  readonly rates: readonly WorkerRate[];
  readonly cases: readonly WorkerCase[];
  readonly version: number;
  /** Which contributions the worker pays; null unless the person may see pay. */
  readonly statutory: WorkerStatutory | null;
  /** The weekly rest day; null when the worker has no fixed rest day (every day is then an ordinary day for pay). */
  readonly restDay?: RestDay | null;
  /** The worker has a time clock badge code (the code itself is only on the badge pages). */
  readonly hasPunchCode?: boolean;
}

export type RestDay =
  'Sunday' | 'Monday' | 'Tuesday' | 'Wednesday' | 'Thursday' | 'Friday' | 'Saturday';

/** Which contributions a worker pays and whether tax is withheld. */
export interface WorkerStatutory {
  readonly sssMember: boolean;
  readonly philHealthMember: boolean;
  readonly pagIbigMember: boolean;
  readonly taxExempt: boolean;
}

export interface WorkerListParams {
  readonly search: string;
  /** Empty for every status. */
  readonly status: string;
  /** Empty for every project. */
  readonly projectId: string;
  readonly page: number;
  readonly pageSize: number;
}

export interface HireRequest {
  readonly employeeNumber: string;
  readonly firstName: string;
  readonly middleName: string | null;
  readonly lastName: string;
  readonly dateHired: string;
  readonly designation: string;
  readonly rateMinor: number;
  readonly rateUnit: RateUnit;
  readonly clientRequestId: string;
  readonly sssMember?: boolean;
  readonly philHealthMember?: boolean;
  readonly pagIbigMember?: boolean;
  readonly taxExempt?: boolean;
  /** A day of the week, or "None" for no fixed rest day. */
  readonly restDay?: string;
}

export interface ProfileRequest {
  readonly firstName: string;
  readonly middleName: string | null;
  readonly lastName: string;
  readonly dateOfBirth: string | null;
  readonly maritalStatus: string | null;
  readonly spouseName: string | null;
  readonly religion: string | null;
  readonly educationalAttainment: string | null;
  readonly presentAddress: string | null;
  readonly provincialAddress: string | null;
  readonly contactNumber: string | null;
  readonly reference: string | null;
  readonly version: number;
  /** A day of the week, or "None"; left out, the rest day stays as it is. */
  readonly restDay?: string;
}

export interface ChangeRateRequest {
  readonly category: Exclude<RateCategory, 'Initial'>;
  readonly designation: string;
  readonly rateMinor: number;
  readonly rateUnit: RateUnit;
  readonly effectiveFrom: string;
  readonly reason: string | null;
  readonly version: number;
}

export interface AssignRequest {
  readonly projectId: string;
  readonly subProjectId: string | null;
  readonly startDate: string;
  readonly version: number;
}

export interface BulkAssignRequest {
  readonly workerIds: readonly string[];
  readonly projectId: string;
  readonly subProjectId: string | null;
  readonly startDate: string;
}

export interface ReleaseRequest {
  readonly endDate: string;
  readonly version: number;
}

export interface ChangeStatusRequest {
  readonly status: WorkerStatus;
  readonly effectiveDate: string;
  readonly reason: string | null;
  readonly version: number;
}

export interface ReinstateRequest {
  readonly effectiveDate: string;
  readonly reason: string;
  readonly version: number;
}

export interface CaseRequest {
  readonly title: string;
  readonly remarks: string | null;
  readonly caseDate: string;
  readonly projectId: string | null;
  readonly version: number;
}

/** A project in a picker, with the sub-projects a worker can be posted to. */
export interface ProjectChoice {
  readonly id: string;
  readonly name: string;
  readonly status: string;
  readonly subProjects: readonly {
    readonly id: string;
    readonly name: string;
    readonly status: string;
  }[];
}

/** Attendance carries hours only, never pay. `isLocked` means a payroll run has taken the entry. */
export interface AttendanceEntry {
  readonly id: string;
  readonly workerId: string;
  readonly workerName: string;
  readonly employeeNumber: string;
  readonly projectId: string;
  readonly projectName: string;
  readonly subProjectId: string | null;
  readonly subProjectName: string | null;
  readonly workDate: string;
  readonly regularHours: number;
  readonly overtimeHours: number;
  readonly remarks: string | null;
  readonly status: 'Recorded' | 'Voided';
  readonly isLocked: boolean;
  readonly payrollRunId: string | null;
  readonly voidReason: string | null;
  readonly version: number;
  /** The hours of the day that fell in the night (they earn the night differential). */
  readonly nightHours?: number;
  /** What the day is for this worker, decided by the server: Ordinary, RestDay, Special, RegularHoliday and the rest-day mixes. */
  readonly dayType?: string;
  /** Manual (typed) or TimeClock (a confirmed day of scans, which keeps its first in, last out and break). */
  readonly source?: 'Manual' | 'TimeClock';
}

/** A project's day: the hours recorded and the crew posted there with no entry yet, and the company holiday on it if any. */
export interface AttendanceSheet {
  readonly projectId: string;
  readonly projectName: string;
  readonly workDate: string;
  readonly entries: readonly AttendanceEntry[];
  readonly totalRegularHours: number;
  readonly totalOvertimeHours: number;
  readonly crewWithoutEntry: readonly WorkerLookup[];
  readonly holidayName?: string | null;
  readonly holidayType?: 'Regular' | 'Special' | null;
}

export interface AttendanceBatchLine {
  readonly workerId: string;
  readonly regularHours: number;
  readonly overtimeHours: number;
  readonly remarks: string | null;
  readonly nightHours?: number;
}

export interface AttendanceBatchRequest {
  readonly projectId: string;
  readonly subProjectId: string | null;
  readonly workDate: string;
  readonly entries: readonly AttendanceBatchLine[];
  readonly clientRequestId: string;
}

export interface ReviseAttendanceRequest {
  readonly subProjectId: string | null;
  readonly regularHours: number;
  readonly overtimeHours: number;
  readonly remarks: string | null;
  readonly version: number;
  readonly nightHours?: number;
}

export interface VoidAttendanceRequest {
  readonly reason: string;
  readonly version: number;
}
