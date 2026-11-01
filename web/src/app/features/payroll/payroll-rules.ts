import { addDays, formatDate } from '../../shared/presentation/business-time';
import { IconName } from '../../shared/ui/icon/icon';
import { ChipTone } from '../../shared/ui/status-chip/status-chip';
import { PayrollRun, PayrollStatus } from './payroll.models';

export interface StatusPresentation {
  readonly label: string;
  readonly tone: ChipTone;
  readonly icon: IconName;
}

/** How each payroll state looks, in one place: the list and the run page describe a state the same way. */
export const PAYROLL_STATUS: Record<PayrollStatus, StatusPresentation> = {
  Draft: { label: 'Draft', tone: 'warning', icon: 'pencil' },
  Approved: { label: 'Approved', tone: 'success', icon: 'check-circle' },
  Voided: { label: 'Voided', tone: 'neutral', icon: 'minus-circle' },
};

/** The most days a pay period may span (the API's own limit, counted from the first day to the last). */
export const LONGEST_PERIOD_DAYS = 31;

/** The pay period offered first: the 14 days that ended yesterday. Today is not yet worked, so it is left out. */
export function defaultPeriod(today: string): { readonly start: string; readonly end: string } {
  const end = addDays(today, -1) ?? today;
  return { start: addDays(end, -13) ?? end, end };
}

/** Whole days from one business date to a later one. */
export function daysBetween(from: string, to: string): number {
  const read = (date: string): number => {
    const [year, month, day] = date.split('-').map(Number);
    return Date.UTC(year ?? 1970, (month ?? 1) - 1, day ?? 1);
  };
  return Math.round((read(to) - read(from)) / 86_400_000);
}

export interface PeriodProblems {
  readonly start?: string;
  readonly end?: string;
}

/**
 * What can be said about a pay period before asking the server: both days chosen, in order, not ending in the future, and no
 * longer than the company can pay in one run. The server checks the same things and has the last word.
 */
export function checkPeriod(start: string, end: string, today: string): PeriodProblems {
  if (start === '') return { start: 'Choose the first day of the pay period.' };
  if (end === '') return { end: 'Choose the last day of the pay period.' };
  if (end < start) return { end: "The last day can't be before the first day." };
  if (end > today) return { end: "The pay period can't end in the future." };
  if (daysBetween(start, end) > LONGEST_PERIOD_DAYS)
    return { end: `A pay period is at most ${LONGEST_PERIOD_DAYS} days.` };
  return {};
}

/** "1 Nov to 14 Nov 2026"; both years are written when the period crosses a year end. */
export function periodLabel(start: string, end: string): string {
  const first = formatDate(start);
  const last = formatDate(end);
  if (start.slice(0, 4) === end.slice(0, 4)) {
    // "1 Nov 2026" without its year, so the pair reads "1 Nov to 14 Nov 2026".
    return `${first.replace(/ \d{4}$/, '')} to ${last}`;
  }
  return `${first} to ${last}`;
}

/** One place the approved payroll posts labor cost: a project, or a sub-project of it, and what it is charged. */
export interface Posting {
  readonly key: string;
  readonly projectName: string;
  readonly subProjectName: string | null;
  readonly label: string;
  readonly amountMinor: number;
}

/**
 * Where approving this run will post its labor cost, exactly as the API groups it: one amount for each project and sub-project,
 * the sum of what every worker earned there. The amounts are the API's own shares added up (whole minor units, no rounding), and
 * they always add up to the run's gross pay. Used to tell the person what approving does, and what voiding would reverse.
 */
export function postingsOf(run: PayrollRun): readonly Posting[] {
  const groups = new Map<
    string,
    { projectName: string; subProjectName: string | null; amountMinor: number }
  >();
  for (const line of run.lines) {
    for (const share of line.allocations) {
      const key = `${share.projectId}|${share.subProjectId ?? ''}`;
      const group = groups.get(key) ?? {
        projectName: share.projectName,
        subProjectName: share.subProjectName,
        amountMinor: 0,
      };
      group.amountMinor += share.grossMinor;
      groups.set(key, group);
    }
  }
  return Array.from(groups.entries())
    .filter(([, group]) => group.amountMinor > 0)
    .map(([key, group]) => ({
      key,
      projectName: group.projectName,
      subProjectName: group.subProjectName,
      label: group.subProjectName
        ? `${group.projectName}, ${group.subProjectName}`
        : group.projectName,
      amountMinor: group.amountMinor,
    }))
    .sort((a, b) => a.label.localeCompare(b.label));
}

/** What the person holds, for deciding which buttons exist. */
export interface PayrollRights {
  /** payroll.run: prepare a payroll, enter deductions, void a draft. */
  readonly canRun: boolean;
  /** payroll.approve: approve a payroll, and void one that was approved. */
  readonly canApprove: boolean;
}

export interface RunActions {
  readonly editDeductions: boolean;
  readonly approve: boolean;
  readonly void: boolean;
  readonly exportRegister: boolean;
  /** Tell the person who approves, because they cannot. Only a draft is waiting for someone. */
  readonly approverNote: boolean;
}

/**
 * Which actions a run offers a person. An action they cannot use is absent, never greyed out. A draft is changed by whoever can
 * prepare payroll and approved by whoever can approve it; an approved payroll can only be voided by an approver; a voided one
 * is only read. A person who may only read sees no actions at all.
 */
export function runActions(status: PayrollStatus, rights: PayrollRights): RunActions {
  const draft = status === 'Draft';
  const approved = status === 'Approved';
  return {
    editDeductions: draft && rights.canRun,
    approve: draft && rights.canApprove,
    void: (draft && rights.canRun) || (approved && rights.canApprove),
    exportRegister: approved,
    approverNote: draft && !rights.canApprove,
  };
}
