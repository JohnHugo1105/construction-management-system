import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Type } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { of, throwError } from 'rxjs';
import { Permission } from '../../core/session/permissions';
import { SessionState } from '../../core/session/session-state';
import { buttonNamed, settle, text, texts } from '../../testing/dom';
import { signInForTest } from '../../testing/session-fixture';
import { HolidaysPage, holidayYear } from './holidays-page';
import { PayRulesApi } from './pay-rules-api';
import { PayRulesPage } from './pay-rules-page';
import { PayrollApi } from './payroll-api';
import { run } from './payroll.fixtures';
import { CompanyHoliday, PayRules } from './payroll.models';
import { ThirteenthMonthPage, thirteenthMonthYears } from './thirteenth-month-page';
import { validationError } from '../workforce/workforce.fixtures';

const READER = [Permission.projectsRead, Permission.payrollRead];
const KEEPER = [...READER, Permission.payrollRulesManage, Permission.payrollRun];

const RULES: PayRules = {
  restDayBps: 13_000,
  specialDayBps: 13_000,
  specialRestDayBps: 15_000,
  regularHolidayBps: 20_000,
  regularHolidayRestDayBps: 26_000,
  premiumDayOvertimeExtraBps: 3_000,
  nightDifferentialBps: 1_000,
  overtimeMultiplierBps: 12_500,
  isDefault: true,
  version: 0,
};

function holiday(overrides: Partial<CompanyHoliday> = {}): CompanyHoliday {
  return {
    id: 'h1',
    date: '2026-12-25',
    name: 'Christmas Day',
    type: 'Regular',
    coveredByApprovedPayroll: false,
    version: 0,
    ...overrides,
  };
}

async function open<T>(
  component: Type<T>,
  url: string,
  permissions: readonly string[],
  api: Record<string, unknown>,
  payroll: Record<string, unknown> = {},
) {
  TestBed.configureTestingModule({
    providers: [
      provideRouter([
        { path: 'payroll/rules', component: PayRulesPage },
        { path: 'payroll/holidays', component: HolidaysPage },
        { path: 'payroll/thirteenth-month', component: ThirteenthMonthPage },
        { path: 'payroll/:id', children: [] },
        { path: 'payroll', children: [] },
      ]),
      provideHttpClient(),
      provideHttpClientTesting(),
      { provide: PayRulesApi, useValue: api },
      { provide: PayrollApi, useValue: payroll },
    ],
  });
  await signInForTest(TestBed.inject(SessionState), TestBed.inject(HttpTestingController), {
    roles: ['Custom'],
    permissions,
  });
  const harness = await RouterTestingHarness.create();
  await harness.navigateByUrl(url, component);
  await settle(harness.fixture);
  return { root: harness.fixture.nativeElement as HTMLElement, fixture: harness.fixture };
}

function type(input: HTMLInputElement, value: string): void {
  input.value = value;
  input.dispatchEvent(new Event('input', { bubbles: true }));
}

describe('PayRulesPage', () => {
  it('lets a keeper of the rules change a factor in percent and saves it as basis points with the version seen', async () => {
    const saveRules = vi.fn(() =>
      of({ ...RULES, restDayBps: 15_000, isDefault: false, version: 1 }),
    );
    const { root, fixture } = await open(PayRulesPage, '/payroll/rules', KEEPER, {
      rules: () => of(RULES),
      saveRules,
    });

    const inputs = Array.from(root.querySelectorAll<HTMLInputElement>('input'));
    expect(inputs.map((input) => input.value)).toEqual([
      '130',
      '130',
      '150',
      '200',
      '260',
      '30',
      '10',
    ]);
    expect(text(root, 'form')).toContain(
      'Overtime on an ordinary day: 125%, set in company settings.',
    );
    expect(text(root, '.card')).toContain('These are the usual rates.');

    type(inputs[0]!, '150');
    buttonNamed(root, 'Save pay rules').click();
    await settle(fixture);

    expect(saveRules).toHaveBeenCalledWith(
      {
        restDayBps: 15_000,
        specialDayBps: 13_000,
        specialRestDayBps: 15_000,
        regularHolidayBps: 20_000,
        regularHolidayRestDayBps: 26_000,
        premiumDayOvertimeExtraBps: 3_000,
        nightDifferentialBps: 1_000,
      },
      0,
    );
    expect(text(root, '.card')).not.toContain('These are the usual rates.');
  });

  it('checks the bounds before sending and shows the server’s field message under the box', async () => {
    const saveRules = vi.fn(() =>
      throwError(() =>
        validationError({
          nightDifferentialBps: ['Enter the night differential between 0% and 100%.'],
        }),
      ),
    );
    const { root, fixture } = await open(PayRulesPage, '/payroll/rules', KEEPER, {
      rules: () => of(RULES),
      saveRules,
    });
    const inputs = Array.from(root.querySelectorAll<HTMLInputElement>('input'));

    type(inputs[0]!, '90');
    buttonNamed(root, 'Save pay rules').click();
    await settle(fixture);
    expect(saveRules).not.toHaveBeenCalled();
    expect(texts(root, '.field__error')).toEqual(['Enter a percentage between 100% and 500%.']);

    type(inputs[0]!, '130');
    buttonNamed(root, 'Save pay rules').click();
    await settle(fixture);
    expect(texts(root, '.field__error')).toEqual([
      'Enter the night differential between 0% and 100%.',
    ]);
  });

  it('shows the rules without a form to someone who only reads payroll', async () => {
    const { root } = await open(PayRulesPage, '/payroll/rules', READER, {
      rules: () => of({ ...RULES, isDefault: false }),
    });

    expect(root.querySelector('form')).toBeNull();
    expect(texts(root, '.rules__row')).toContain('Regular holiday on a rest day260%');
    expect(texts(root, '.rules__row')).toContain('Overtime on an ordinary day125%');
  });
});

describe('HolidaysPage', () => {
  it('reads the year from the address, and falls back to this year', () => {
    expect(holidayYear('2027', '2026-11-02')).toBe(2027);
    expect(holidayYear('', '2026-11-02')).toBe(2026);
    expect(holidayYear('20x7', '2026-11-02')).toBe(2026);
  });

  it('lists the year’s holidays; one an approved payroll covers says why it can only be renamed', async () => {
    const holidays = vi.fn(() =>
      of({
        year: 2026,
        holidays: [
          holiday({
            id: 'h0',
            date: '2026-10-07',
            name: 'Founders day',
            coveredByApprovedPayroll: true,
          }),
          holiday({ type: 'Special' }),
        ],
      }),
    );
    const { root } = await open(HolidaysPage, '/payroll/holidays?year=2026', KEEPER, { holidays });

    expect(holidays).toHaveBeenCalledWith(2026);
    const cards = root.querySelectorAll('.row-card');
    expect(text(cards[0]!, '.row-card__title')).toBe('Founders day');
    expect(text(cards[0]!, 'app-status-chip')).toBe('Regular holiday');
    expect(text(cards[0]!, '.row-card__note')).toContain('An approved payroll covers this day.');
    expect(cards[1]!.querySelector('.row-card__note')).toBeNull();
    expect(texts(cards[0]!, 'button')).toEqual(['Rename']);
    expect(texts(cards[1]!, 'button')).toEqual(['Change', 'Remove']);
    expect(text(cards[1]!, 'app-status-chip')).toBe('Special day');
  });

  it('adds a holiday through the dialog and reads the year again', async () => {
    const holidays = vi.fn(() => of({ year: 2026, holidays: [] }));
    const addHoliday = vi.fn(() => of(holiday()));
    const { root, fixture } = await open(HolidaysPage, '/payroll/holidays?year=2026', KEEPER, {
      holidays,
      addHoliday,
    });

    expect(text(root, 'app-empty-state')).toContain('No holidays entered for 2026');
    buttonNamed(root, 'Add holiday').click();
    await settle(fixture);
    const dialog = document.querySelector<HTMLElement>('app-holiday-dialog')!;
    const date = dialog.querySelector<HTMLInputElement>('input[type="date"]')!;
    date.value = '2026-12-25';
    date.dispatchEvent(new Event('input', { bubbles: true }));
    type(dialog.querySelector<HTMLInputElement>('input[formcontrolname="name"]')!, 'Christmas Day');
    dialog.querySelectorAll<HTMLInputElement>('input[type="radio"]')[0]!.click();
    buttonNamed(dialog, 'Add holiday').click();
    await settle(fixture);

    expect(addHoliday).toHaveBeenCalledWith('2026-12-25', 'Christmas Day', 'Regular');
    expect(holidays).toHaveBeenCalledTimes(2);
  });

  it('shows the calendar without changes to someone who only reads payroll', async () => {
    const { root } = await open(HolidaysPage, '/payroll/holidays?year=2026', READER, {
      holidays: () => of({ year: 2026, holidays: [] }),
    });

    expect(text(root, 'app-empty-state')).toContain(
      'Someone who keeps the pay rules can add this year',
    );
    expect(texts(root, 'button')).not.toContain('Add holiday');
  });
});

describe('ThirteenthMonthPage', () => {
  it('offers this year and last year, never a future one', () => {
    expect(thirteenthMonthYears('2026-11-02')).toEqual([2026, 2025]);
  });

  it('prepares the 13th-month pay for the chosen year with one request id and opens it', async () => {
    const createThirteenthMonth = vi.fn(() =>
      of(run({ id: 'run13', kind: 'ThirteenthMonth', year: 2025 })),
    );
    const { root, fixture } = await open(
      ThirteenthMonthPage,
      '/payroll/thirteenth-month',
      KEEPER,
      {},
      { createThirteenthMonth },
    );
    const router = TestBed.inject(Router);

    const years = texts(root, '.choice__label').map(Number);
    expect(years).toHaveLength(2);
    expect(years[0]! - years[1]!).toBe(1);
    root.querySelectorAll<HTMLInputElement>('input[type="radio"]')[1]!.click();
    buttonNamed(root, 'Prepare 13th-month pay').click();
    await settle(fixture);

    const calls = createThirteenthMonth.mock.calls as unknown as [number, string][];
    expect(calls[0]![0]).toBe(years[1]);
    expect(calls[0]![1]).toMatch(/\S{8,}/);
    expect(router.url).toBe('/payroll/run13');
  });
});
