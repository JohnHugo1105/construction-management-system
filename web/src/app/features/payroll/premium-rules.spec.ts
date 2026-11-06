import { line, run } from './payroll.fixtures';
import {
  FACTOR_FIELDS,
  bpsFromPercent,
  checkFactor,
  archivedPostingsNote,
  holidayNote,
  payBreakdown,
  percentText,
  runLabel,
  runTitle,
} from './premium-rules';

const money = (minor: number): string => `P${(minor / 100).toFixed(2)}`;

describe('premium rules presentation', () => {
  it('names a payroll by its period and a 13th-month payroll by its year', () => {
    expect(runTitle(run())).toBe('Payroll 18 Oct to 31 Oct 2026');
    const thirteenth = run({
      kind: 'ThirteenthMonth',
      year: 2026,
      periodStart: '2026-01-01',
      periodEnd: '2026-12-31',
    });
    expect(runTitle(thirteenth)).toBe('13th-month pay 2026');
    expect(runLabel(thirteenth)).toBe('13th-month pay 2026');
  });

  it('says in one line that no holidays are entered, only when the API says so', () => {
    expect(holidayNote(run())).toBeNull();
    expect(holidayNote(run({ holidaysMissingForYear: 2026 }))).toBe(
      'No holidays are entered for 2026. Add them under Payroll, Holidays if any fall in this period.',
    );
  });

  it('counts the workers left out of holiday pay for an archived project, only when there are any', () => {
    expect(archivedPostingsNote(run())).toBeNull();
    expect(archivedPostingsNote(run({ workersOnArchivedProjectsLeftOut: 0 }))).toBeNull();
    expect(archivedPostingsNote(run({ workersOnArchivedProjectsLeftOut: 1 }))).toBe(
      '1 worker posted to an archived project was left out of holiday pay.',
    );
    expect(archivedPostingsNote(run({ workersOnArchivedProjectsLeftOut: 3 }))).toBe(
      '3 workers posted to archived projects were left out of holiday pay.',
    );
  });

  it('writes and reads percentages as basis points', () => {
    expect(percentText(13_000)).toBe('130%');
    expect(percentText(1_250)).toBe('12.5%');
    expect(bpsFromPercent('130')).toBe(13_000);
    expect(bpsFromPercent(' 12.5 ')).toBe(1_250);
    expect(bpsFromPercent('12.345')).toBeNull();
    expect(bpsFromPercent('-5')).toBeNull();
    expect(bpsFromPercent('abc')).toBeNull();
  });

  it('checks each factor against the bounds the server applies', () => {
    const restDay = FACTOR_FIELDS.find((field) => field.key === 'restDayBps')!;
    const night = FACTOR_FIELDS.find((field) => field.key === 'nightDifferentialBps')!;
    expect(checkFactor(restDay, '130')).toBeNull();
    expect(checkFactor(restDay, '99')).toBe('Enter a percentage between 100% and 500%.');
    expect(checkFactor(restDay, '501')).toBe('Enter a percentage between 100% and 500%.');
    expect(checkFactor(night, '0')).toBeNull();
    expect(checkFactor(night, '')).toBe('Enter a percentage, for example 130.');
  });

  it('folds the gross into regular, overtime and premium, night and holiday pay, leaving out what is zero', () => {
    expect(payBreakdown(line({ overtimePayMinor: 0 }), money)).toEqual([
      { label: 'Regular pay', amount: 'P8000.00', note: null },
    ]);

    const rows = payBreakdown(
      line({
        regularPayMinor: 80_000,
        premiumPayMinor: 24_000,
        overtimePayMinor: 33_800,
        nightPayMinor: 2_600,
        nightHours: 2,
        holidayPayMinor: 60_000,
        holidayDates: ['2026-10-07'],
      }),
      money,
    );
    expect(rows).toEqual([
      { label: 'Regular pay', amount: 'P800.00', note: null },
      { label: 'Overtime and premium', amount: 'P578.00', note: null },
      { label: 'Night differential', amount: 'P26.00', note: '2 h at night' },
      { label: 'Holiday pay', amount: 'P600.00', note: 'Not worked: 7 Oct 2026' },
    ]);
  });

  it('shows a 13th-month line as the amount and the basic pay it is a twelfth of', () => {
    expect(
      payBreakdown(
        line({
          regularPayMinor: 0,
          overtimePayMinor: 0,
          thirteenthMonthPayMinor: 20_000,
          thirteenthMonthBasisMinor: 240_000,
        }),
        money,
      ),
    ).toEqual([
      { label: '13th-month pay', amount: 'P200.00', note: 'A twelfth of P2400.00 basic pay' },
    ]);
  });
});
