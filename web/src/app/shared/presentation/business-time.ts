// Dates and times. Business dates ("2026-12-03", no time) are shown as written and never shifted by the browser's time
// zone; instants are shown in the company's time zone. One style everywhere: "3 Dec 2026", "3 Dec 2026, 2:15 pm".

const MONTHS = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];

/** The time zone used when the company's is missing or unknown. */
export const DEFAULT_TIME_ZONE = 'Asia/Manila';

const DATE_ONLY = /^(\d{4})-(\d{2})-(\d{2})/;

/** "2026-12-03" gives "3 Dec 2026". Empty, missing or malformed input gives an empty string. */
export function formatDate(value: string | null | undefined): string {
  if (!value) return '';
  const match = DATE_ONLY.exec(value);
  if (!match) return '';
  const month = Number(match[2]);
  const day = Number(match[3]);
  if (month < 1 || month > 12 || day < 1 || day > 31) return '';
  return `${day} ${MONTHS[month - 1]} ${match[1]}`;
}

/** An instant in the company's time zone: "2026-12-03T06:15:00Z" in Manila gives "3 Dec 2026, 2:15 pm". */
export function formatDateTime(instant: string | null | undefined, timeZone: string): string {
  if (!instant) return '';
  const date = new Date(instant);
  if (Number.isNaN(date.getTime())) return '';

  const parts = new Map<string, string>();
  for (const part of dateTimeFormat(timeZone).formatToParts(date)) parts.set(part.type, part.value);
  const period = (parts.get('dayPeriod') ?? '').toLowerCase();
  return `${parts.get('day')} ${parts.get('month')} ${parts.get('year')}, ${parts.get('hour')}:${parts.get('minute')} ${period}`.trim();
}

/** Today's calendar date ("2026-12-03") where the company works, never the UTC day. */
export function todayIn(timeZone: string, now: Date = new Date()): string {
  const parts = wallClock(now.getTime(), timeZone);
  return `${pad(parts.year, 4)}-${pad(parts.month, 2)}-${pad(parts.day, 2)}`;
}

/** The instant (ISO, UTC) at which a business date begins in the given time zone. */
export function startOfDayUtc(date: string, timeZone: string): string | null {
  const match = DATE_ONLY.exec(date);
  if (!match) return null;
  const midnight = Date.UTC(Number(match[1]), Number(match[2]) - 1, Number(match[3]));

  // Two passes settle the offset even on a day when the clocks change.
  let instant = midnight;
  for (let pass = 0; pass < 2; pass++) instant = midnight - zoneOffsetMs(instant, timeZone);
  return new Date(instant).toISOString();
}

/** A business date moved by whole days: ("2026-12-31", 1) gives "2027-01-01". */
export function addDays(date: string, days: number): string | null {
  const match = DATE_ONLY.exec(date);
  if (!match) return null;
  const moved = new Date(Date.UTC(Number(match[1]), Number(match[2]) - 1, Number(match[3]) + days));
  return `${pad(moved.getUTCFullYear(), 4)}-${pad(moved.getUTCMonth() + 1, 2)}-${pad(moved.getUTCDate(), 2)}`;
}

function pad(value: number, length: number): string {
  return String(value).padStart(length, '0');
}

const formatters = new Map<string, Intl.DateTimeFormat>();

function dateTimeFormat(timeZone: string): Intl.DateTimeFormat {
  const key = `dt|${timeZone}`;
  let format = formatters.get(key);
  if (!format) {
    const options: Intl.DateTimeFormatOptions = {
      day: 'numeric',
      month: 'short',
      year: 'numeric',
      hour: 'numeric',
      minute: '2-digit',
      hour12: true,
    };
    format = createFormatter(timeZone, options);
    formatters.set(key, format);
  }
  return format;
}

function createFormatter(
  timeZone: string,
  options: Intl.DateTimeFormatOptions,
): Intl.DateTimeFormat {
  try {
    return new Intl.DateTimeFormat('en-US', { ...options, timeZone });
  } catch {
    return new Intl.DateTimeFormat('en-US', { ...options, timeZone: DEFAULT_TIME_ZONE });
  }
}

interface WallClock {
  readonly year: number;
  readonly month: number;
  readonly day: number;
  readonly hour: number;
  readonly minute: number;
  readonly second: number;
}

function wallClock(instant: number, timeZone: string): WallClock {
  const key = `wall|${timeZone}`;
  let format = formatters.get(key);
  if (!format) {
    format = createFormatter(timeZone, {
      year: 'numeric',
      month: 'numeric',
      day: 'numeric',
      hour: 'numeric',
      minute: 'numeric',
      second: 'numeric',
      hourCycle: 'h23',
    });
    formatters.set(key, format);
  }
  const values: Record<string, number> = {};
  for (const part of format.formatToParts(new Date(instant))) {
    if (part.type !== 'literal') values[part.type] = Number(part.value);
  }
  return {
    year: values['year'] ?? 1970,
    month: values['month'] ?? 1,
    day: values['day'] ?? 1,
    hour: (values['hour'] ?? 0) % 24,
    minute: values['minute'] ?? 0,
    second: values['second'] ?? 0,
  };
}

/** How far ahead of UTC the wall clock in a time zone is at a given instant. */
function zoneOffsetMs(instant: number, timeZone: string): number {
  const clock = wallClock(instant, timeZone);
  const asUtc = Date.UTC(
    clock.year,
    clock.month - 1,
    clock.day,
    clock.hour,
    clock.minute,
    clock.second,
  );
  return asUtc - Math.floor(instant / 1000) * 1000;
}
