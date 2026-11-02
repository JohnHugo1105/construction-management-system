import { ChipTone } from '../../shared/ui/status-chip/status-chip';
import { IconName } from '../../shared/ui/icon/icon';
import { PunchDirection, TimeClockDayRow, TimeClockException } from './timeclock.models';

/** "07:02" (the company's time of day, as the server sends it) as people read it: "7:02 am". "13:05" is "1:05 pm", "00:10" is "12:10 am". */
export function clockTime(value: string | null | undefined): string {
  const match = /^(\d{2}):(\d{2})$/.exec(value ?? '');
  if (!match) return '';
  const hour = Number(match[1]);
  const period = hour < 12 ? 'am' : 'pm';
  const twelve = hour % 12 === 0 ? 12 : hour % 12;
  return `${twelve}:${match[2]} ${period}`;
}

/** "Time in" or "Time out". */
export function directionWords(direction: PunchDirection | null | undefined): string {
  return direction === 'Out' ? 'Time out' : 'Time in';
}

/** Hours as people say them: 8 is "8 h", 1.5 is "1.5 h", 0.25 is "0.25 h". */
export function hoursText(hours: number): string {
  return `${Number(hours.toFixed(2))} h`;
}

/** A day's hours in one line: "8 h regular", "8 h regular, 2 h night". Overtime is shown on its own, because it waits for approval. */
export function hoursLine(row: TimeClockDayRow): string {
  const parts = [`${hoursText(row.regularHours)} regular`];
  if (row.nightHours > 0) parts.push(`${hoursText(row.nightHours)} at night`);
  return parts.join(', ');
}

/** "In 7:02 am, out 5:03 pm, 60 min break", leaving out what isn't there. */
export function scanLine(row: TimeClockDayRow): string {
  const parts: string[] = [];
  if (row.firstInLocal) parts.push(`In ${clockTime(row.firstInLocal)}`);
  if (row.lastOutLocal)
    parts.push(`${parts.length ? 'out' : 'Out'} ${clockTime(row.lastOutLocal)}`);
  if (row.breakMinutes > 0) parts.push(`${row.breakMinutes} min break`);
  return parts.join(', ');
}

export interface ExceptionChip {
  readonly label: string;
  readonly tone: ChipTone;
  readonly icon: IconName;
  /** What the reviewer can do about it. */
  readonly help: string;
}

/** Each exception in words with an icon, never colour alone. */
export const EXCEPTION_CHIPS: Readonly<Record<TimeClockException, ExceptionChip>> = {
  MissingTimeOut: {
    label: 'No time out',
    tone: 'danger',
    icon: 'warning',
    help: 'Add the missing time out, or remove an extra time in.',
  },
  MissingTimeIn: {
    label: 'No time in',
    tone: 'danger',
    icon: 'warning',
    help: 'Add the missing time in, or remove an extra time out.',
  },
  NotPostedHere: {
    label: 'Not posted here',
    tone: 'warning',
    icon: 'info',
    help: 'This worker was not posted to this project that day. Check before confirming.',
  },
  TooLong: {
    label: 'Over 16 hours',
    tone: 'warning',
    icon: 'clock',
    help: 'More than 16 hours in one day. Check the scans before confirming.',
  },
};

/** "Time in" and "Time out" on the kiosk: words, an icon and a tone together. */
export function kioskDirection(direction: PunchDirection | null): {
  readonly words: string;
  readonly tone: 'in' | 'out';
} {
  return direction === 'Out'
    ? { words: 'Time out', tone: 'out' }
    : { words: 'Time in', tone: 'in' };
}

/** "5 workers", "1 worker". */
export function workers(count: number): string {
  return count === 1 ? '1 worker' : `${count} workers`;
}

/** A random id for a scan or a request. */
export function newId(): string {
  const crypto = globalThis.crypto;
  if (crypto?.randomUUID) return crypto.randomUUID();
  return `id-${Date.now().toString(36)}-${Math.random().toString(36).slice(2)}`;
}
