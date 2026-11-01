import { ApiError } from '../../core/api/api-error';
import { Permission } from '../../core/session/permissions';
import {
  AttendanceEntry,
  AttendanceSheet,
  ProjectChoice,
  WorkerDetail,
  WorkerListItem,
  WorkerRate,
} from './workforce.models';

/** What an HR officer holds: manage workers, record attendance, and see and prepare pay (no approving). */
export const HR_PERMISSIONS: readonly string[] = [
  Permission.projectsRead,
  Permission.workforceRead,
  Permission.workforceManage,
  Permission.attendanceWrite,
  Permission.payrollRead,
  Permission.payrollRun,
];

/** What a project manager holds: sees workers and records attendance, but not pay. */
export const MANAGER_PERMISSIONS: readonly string[] = [
  Permission.projectsRead,
  Permission.projectsManage,
  Permission.costsRead,
  Permission.workforceRead,
  Permission.attendanceWrite,
  Permission.inventoryRead,
];

/** What an accountant holds in payroll: read, prepare and approve. */
export const ACCOUNTANT_PERMISSIONS: readonly string[] = [
  Permission.projectsRead,
  Permission.costsRead,
  Permission.costsWrite,
  Permission.workforceRead,
  Permission.payrollRead,
  Permission.payrollRun,
  Permission.payrollApprove,
];

export function validationError(errors: Record<string, string[]>, status = 422): ApiError {
  return new ApiError({
    status,
    title: 'Some details need your attention',
    detail: 'Check the highlighted fields and try again.',
    code: 'validation_failed',
    errors,
  });
}

export function ruleViolation(detail: string): ApiError {
  return new ApiError({ status: 409, title: "That can't be done", detail, code: 'rule_violation' });
}

export function conflict(): ApiError {
  return new ApiError({
    status: 409,
    title: 'This record changed',
    detail:
      'Someone else changed this record while you were editing. Reload it to see the latest version, then try again.',
    code: 'concurrency_conflict',
  });
}

export function listItem(overrides: Partial<WorkerListItem> = {}): WorkerListItem {
  return {
    id: 'w1',
    employeeNumber: 'W-001',
    fullName: 'Juan Dela Cruz',
    status: 'Active',
    designation: 'Foreman',
    currentProjectId: 'p1',
    currentProjectName: 'Riverside Residences',
    dateHired: '2026-03-01',
    version: 3,
    ...overrides,
  };
}

export const riverside: ProjectChoice = {
  id: 'p1',
  name: 'Riverside Residences',
  status: 'Active',
  subProjects: [{ id: 's1', name: 'Tower A', status: 'Active' }],
};

export const annex: ProjectChoice = {
  id: 'p2',
  name: 'Harbour Annex',
  status: 'Active',
  subProjects: [],
};

export function rate(overrides: Partial<WorkerRate> = {}): WorkerRate {
  return {
    id: 'r1',
    designation: 'Mason',
    rateMinor: 80000,
    rateUnit: 'Daily',
    category: 'Initial',
    effectiveFrom: '2026-03-01',
    reason: 'Hired',
    recordedAtUtc: '2026-03-01T00:00:00Z',
    ...overrides,
  };
}

/** A worker as a payroll person receives them: with a rate and a pay history. */
export function workerWithPay(overrides: Partial<WorkerDetail> = {}): WorkerDetail {
  return {
    id: 'w1',
    employeeNumber: 'W-001',
    firstName: 'Juan',
    middleName: null,
    lastName: 'Dela Cruz',
    fullName: 'Juan Dela Cruz',
    dateOfBirth: null,
    dateHired: '2026-03-01',
    maritalStatus: null,
    spouseName: null,
    religion: null,
    educationalAttainment: null,
    presentAddress: null,
    provincialAddress: null,
    contactNumber: '0917 000 1234',
    reference: null,
    status: 'Active',
    statusChangedOn: null,
    statusReason: null,
    designation: 'Mason',
    currentRateMinor: 80000,
    currentRateUnit: 'Daily',
    currentAssignment: {
      id: 'a1',
      projectId: 'p1',
      projectName: 'Riverside Residences',
      subProjectId: null,
      subProjectName: null,
      startDate: '2026-03-02',
      endDate: null,
      isCurrent: true,
    },
    assignments: [
      {
        id: 'a1',
        projectId: 'p1',
        projectName: 'Riverside Residences',
        subProjectId: null,
        subProjectName: null,
        startDate: '2026-03-02',
        endDate: null,
        isCurrent: true,
      },
    ],
    rates: [rate()],
    cases: [],
    version: 4,
    ...overrides,
  };
}

/** The same worker as everyone without payroll.read receives them: no rate, no history. */
export function workerWithoutPay(overrides: Partial<WorkerDetail> = {}): WorkerDetail {
  return workerWithPay({ currentRateMinor: null, currentRateUnit: null, rates: [], ...overrides });
}

export function entry(overrides: Partial<AttendanceEntry> = {}): AttendanceEntry {
  return {
    id: 'e1',
    workerId: 'w1',
    workerName: 'Juan Dela Cruz',
    employeeNumber: 'W-001',
    projectId: 'p1',
    projectName: 'Riverside Residences',
    subProjectId: null,
    subProjectName: null,
    workDate: '2026-11-01',
    regularHours: 8,
    overtimeHours: 0,
    remarks: null,
    status: 'Recorded',
    isLocked: false,
    payrollRunId: null,
    voidReason: null,
    version: 1,
    ...overrides,
  };
}

export function sheet(overrides: Partial<AttendanceSheet> = {}): AttendanceSheet {
  return {
    projectId: 'p1',
    projectName: 'Riverside Residences',
    workDate: '2026-11-01',
    entries: [],
    totalRegularHours: 0,
    totalOvertimeHours: 0,
    crewWithoutEntry: [
      { id: 'w1', fullName: 'Ana Bautista', employeeNumber: 'W-005', status: 'Active' },
      { id: 'w2', fullName: 'Juan Dela Cruz', employeeNumber: 'W-001', status: 'Active' },
    ],
    ...overrides,
  };
}
