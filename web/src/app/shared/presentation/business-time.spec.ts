import { addDays, formatDate, formatDateTime, startOfDayUtc, todayIn } from './business-time';

describe('formatDate', () => {
  it('writes a business date as day, short month, year', () => {
    expect(formatDate('2026-12-03')).toBe('3 Dec 2026');
    expect(formatDate('2026-01-31')).toBe('31 Jan 2026');
    expect(formatDate('2026-11-02')).toBe('2 Nov 2026');
  });

  it('never shifts the date by the viewer time zone', () => {
    // A date-only value must read the same everywhere, including from a timestamp-shaped string.
    expect(formatDate('2026-12-03T23:30:00+08:00')).toBe('3 Dec 2026');
  });

  it('gives nothing for missing or malformed input', () => {
    expect(formatDate(null)).toBe('');
    expect(formatDate(undefined)).toBe('');
    expect(formatDate('')).toBe('');
    expect(formatDate('soon')).toBe('');
    expect(formatDate('2026-13-01')).toBe('');
  });
});

describe('formatDateTime', () => {
  it('shows an instant in the company time zone', () => {
    expect(formatDateTime('2026-12-03T06:15:00Z', 'Asia/Manila')).toBe('3 Dec 2026, 2:15 pm');
    expect(formatDateTime('2026-12-03T06:15:00Z', 'UTC')).toBe('3 Dec 2026, 6:15 am');
  });

  it('moves to the next day when the zone is ahead of UTC', () => {
    expect(formatDateTime('2026-12-03T18:00:00Z', 'Asia/Manila')).toBe('4 Dec 2026, 2:00 am');
  });

  it('copes with a missing or unknown zone and bad input', () => {
    expect(formatDateTime('2026-12-03T06:15:00Z', 'Not/AZone')).toBe('3 Dec 2026, 2:15 pm');
    expect(formatDateTime('nonsense', 'UTC')).toBe('');
    expect(formatDateTime(null, 'UTC')).toBe('');
  });
});

describe('todayIn', () => {
  it('uses the business day, not the UTC day', () => {
    const lateInUtc = new Date('2026-12-03T20:00:00Z');
    expect(todayIn('UTC', lateInUtc)).toBe('2026-12-03');
    expect(todayIn('Asia/Manila', lateInUtc)).toBe('2026-12-04');
  });

  it('goes back a day for zones behind UTC', () => {
    expect(todayIn('America/Los_Angeles', new Date('2026-12-03T05:00:00Z'))).toBe('2026-12-02');
  });
});

describe('startOfDayUtc and addDays', () => {
  it('finds the instant a business day begins', () => {
    expect(startOfDayUtc('2026-12-03', 'Asia/Manila')).toBe('2026-12-02T16:00:00.000Z');
    expect(startOfDayUtc('2026-12-03', 'UTC')).toBe('2026-12-03T00:00:00.000Z');
    expect(startOfDayUtc('2026-12-03', 'America/Los_Angeles')).toBe('2026-12-03T08:00:00.000Z');
  });

  it('handles a day when the clocks change', () => {
    // US clocks go forward on 8 Mar 2026 (PST to PDT): midnight that day is still PST (08:00Z), the next one is PDT (07:00Z).
    expect(startOfDayUtc('2026-03-08', 'America/Los_Angeles')).toBe('2026-03-08T08:00:00.000Z');
    expect(startOfDayUtc('2026-03-09', 'America/Los_Angeles')).toBe('2026-03-09T07:00:00.000Z');
  });

  it('rejects text that is not a date', () => {
    expect(startOfDayUtc('x', 'UTC')).toBeNull();
    expect(addDays('x', 1)).toBeNull();
  });

  it('moves a business date by whole days across month and year ends', () => {
    expect(addDays('2026-12-31', 1)).toBe('2027-01-01');
    expect(addDays('2026-03-01', -1)).toBe('2026-02-28');
    expect(addDays('2026-12-03', 0)).toBe('2026-12-03');
  });
});
