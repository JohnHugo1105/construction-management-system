import { IconName } from '../../shared/ui/icon/icon';
import { ChipTone } from '../../shared/ui/status-chip/status-chip';
import {
  AttendanceBatchLine,
  AttendanceBatchRequest,
  AttendanceSheet,
  WorkerLookup,
} from './workforce.models';

/** The company day the sheet assumes when the company settings cannot be read. */
export const FALLBACK_HOURS_PER_DAY = 8;

/** What the API allows for the whole day, whatever the company's paid hours: used only to bound the plus button. */
export const MOST_HOURS_IN_A_DAY = 24;

/** One crew member's line on the sheet that has not been saved yet. */
export interface DraftRow {
  readonly workerId: string;
  readonly name: string;
  readonly employeeNumber: string;
  /** Unticked means the worker did not work this day: the line is left out of what is saved. */
  readonly worked: boolean;
  /** Regular hours, or null while the box is blank. */
  readonly regular: number | null;
  readonly overtime: number | null;
  /** Hours of the day that fell in the night; blank is none. */
  readonly night: number | null;
  readonly remarks: string;
  /** The night hours and remarks boxes are open (they stay closed until someone needs them). */
  readonly remarksOpen: boolean;
}

/** What is wrong on a row, in words for the person, keyed by the part of the row it is about. */
export interface RowProblems {
  readonly worker?: string;
  readonly regular?: string;
  readonly overtime?: string;
  readonly night?: string;
  readonly remarks?: string;
}

/**
 * One line for each crew member with no entry yet. Everyone starts as having worked the company's paid hours with no overtime,
 * which is the usual day; the person unticks whoever was away and changes whoever worked differently.
 */
export function draftRowsFor(crew: readonly WorkerLookup[], hoursPerDay: number): DraftRow[] {
  return crew.map((member) => ({
    workerId: member.id,
    name: member.fullName,
    employeeNumber: member.employeeNumber,
    worked: true,
    regular: hoursPerDay,
    overtime: 0,
    night: 0,
    remarks: '',
    remarksOpen: false,
  }));
}

/** The rows that will be saved: the ones ticked as having worked. */
export function rowsToSave(rows: readonly DraftRow[]): DraftRow[] {
  return rows.filter((row) => row.worked);
}

/**
 * Checks what only the browser can see before anything is sent: a box left blank or unreadable, and regular hours above the
 * company's day. Every other rule (the 24 hours in a day across projects, a worker who left, a locked day) belongs to the
 * server, whose messages are shown under the same boxes.
 */
export function checkRows(
  rows: readonly DraftRow[],
  hoursPerDay: number,
): ReadonlyMap<string, RowProblems> {
  const problems = new Map<string, RowProblems>();
  for (const row of rowsToSave(rows)) {
    const found: { regular?: string; overtime?: string } = {};
    if (row.regular === null) found.regular = 'Enter the regular hours, or 0 if there are none.';
    else if (row.regular > hoursPerDay)
      found.regular = `Regular hours are at most ${hoursPerDay} a day. Enter the rest as overtime.`;
    if (row.overtime === null) found.overtime = 'Enter the overtime hours, or 0 if there are none.';
    if (row.regular !== null && row.overtime !== null && row.regular + row.overtime <= 0)
      found.regular = 'Enter the hours worked, or untick this worker.';
    if (found.regular || found.overtime) problems.set(row.workerId, found);
  }
  return problems;
}

/** What is sent for a sheet, and the order of the workers in it (the API reports problems by position). */
export interface BatchPlan {
  readonly request: AttendanceBatchRequest;
  /** The worker at each position of `request.entries`. */
  readonly order: readonly string[];
}

export function planBatch(
  projectId: string,
  subProjectId: string | null,
  workDate: string,
  rows: readonly DraftRow[],
  clientRequestId: string,
): BatchPlan {
  const chosen = rowsToSave(rows);
  const entries: AttendanceBatchLine[] = chosen.map((row) => ({
    workerId: row.workerId,
    regularHours: row.regular ?? 0,
    overtimeHours: row.overtime ?? 0,
    nightHours: row.night ?? 0,
    remarks: row.remarks.trim() || null,
  }));
  return {
    request: { projectId, subProjectId, workDate, entries, clientRequestId },
    order: chosen.map((row) => row.workerId),
  };
}

export interface MappedErrors {
  /** Problems for each worker's row, by worker id. */
  readonly rows: ReadonlyMap<string, RowProblems>;
  /** Messages that belong to the whole sheet (the date, the sub-project, a general rule). */
  readonly general: readonly string[];
}

const ENTRY_KEY = /^entries\[(\d+)\]\.(\w+)$/;

/**
 * Turns the API's field errors into row problems. The batch endpoint keys an error by the position of the line it is about and
 * the field: `entries[3].regularHours`. Position 3 is the fourth worker we sent, so it is looked up in `order`.
 */
export function mapBatchErrors(
  errors: Readonly<Record<string, readonly string[]>>,
  order: readonly string[],
): MappedErrors {
  const rows = new Map<
    string,
    { worker?: string; regular?: string; overtime?: string; night?: string; remarks?: string }
  >();
  const general: string[] = [];

  for (const [key, messages] of Object.entries(errors)) {
    const text = messages.join(' ');
    const match = ENTRY_KEY.exec(key);
    const workerId = match ? order[Number(match[1])] : undefined;
    if (!match || !workerId) {
      general.push(text);
      continue;
    }
    const row = rows.get(workerId) ?? {};
    switch (match[2]) {
      case 'regularHours':
        row.regular = text;
        break;
      case 'overtimeHours':
        row.overtime = text;
        break;
      case 'nightHours':
        row.night = text;
        break;
      case 'remarks':
        row.remarks = text;
        break;
      default:
        // The worker id itself (a duplicate, or someone who already has an entry) is about the whole line.
        row.worker = text;
    }
    rows.set(workerId, row);
  }
  return { rows, general };
}

/** "8 h", "8 h + 2 h overtime", "8 h + 2 h overtime, 3 h at night". Hours as recorded, never pay. */
export function hoursWords(regular: number, overtime: number, night = 0): string {
  const worked = overtime > 0 ? `${regular} h + ${overtime} h overtime` : `${regular} h`;
  return night > 0 ? `${worked}, ${night} h at night` : worked;
}

/** What a day is for a worker, as the API decides it from the holiday calendar and the worker's rest day. */
export type DayType =
  | 'Ordinary'
  | 'RestDay'
  | 'Special'
  | 'SpecialRestDay'
  | 'RegularHoliday'
  | 'RegularHolidayRestDay';

export interface DayChip {
  readonly label: string;
  readonly tone: ChipTone;
  readonly icon: IconName;
}

/** The chip next to a recorded day. An ordinary day has none: there is nothing to say about it. */
export const DAY_TYPE_CHIP: Record<Exclude<DayType, 'Ordinary'>, DayChip> = {
  RestDay: { label: 'Rest day', tone: 'info', icon: 'calendar' },
  Special: { label: 'Special day', tone: 'info', icon: 'calendar' },
  SpecialRestDay: { label: 'Special day, rest day', tone: 'info', icon: 'calendar' },
  RegularHoliday: { label: 'Regular holiday', tone: 'warning', icon: 'calendar' },
  RegularHolidayRestDay: { label: 'Regular holiday, rest day', tone: 'warning', icon: 'calendar' },
};

export function dayTypeChip(dayType: string | null | undefined): DayChip | null {
  if (!dayType || dayType === 'Ordinary') return null;
  return DAY_TYPE_CHIP[dayType as Exclude<DayType, 'Ordinary'>] ?? null;
}

/** The one line about the company holiday on a sheet's day: "Regular holiday: Founders day". Null on any other day. */
export function holidayLine(
  sheet: Pick<AttendanceSheet, 'holidayName' | 'holidayType'>,
): string | null {
  if (!sheet.holidayName || !sheet.holidayType) return null;
  return `${sheet.holidayType === 'Regular' ? 'Regular holiday' : 'Special day'}: ${sheet.holidayName}`;
}

/** A date is in the future when it sorts after today (business dates are written year-month-day). */
export function isFutureDate(date: string, today: string): boolean {
  return date > today;
}
