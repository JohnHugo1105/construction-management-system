import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { Observable, of, throwError } from 'rxjs';
import type { Mock } from 'vitest';
import { ApiError } from '../../core/api/api-error';
import { Permission } from '../../core/session/permissions';
import { SessionState } from '../../core/session/session-state';
import { buttonNamed, settle, text, texts, type } from '../../testing/dom';
import { pageOf, signInForTest } from '../../testing/session-fixture';
import { StatutoryApi } from './statutory-api';
import { StatutoryPage } from './statutory-page';
import { StatutorySchedulePage } from './statutory-schedule-page';
import { StatutorySchedule, StatutoryScheduleListItem, StatutoryStatus } from './statutory.models';

type Call = (...args: unknown[]) => Observable<unknown>;
const stub = (implementation: Call): Mock<Call> => vi.fn<Call>(implementation);

const READER = [Permission.payrollRead];
const MANAGER = [Permission.payrollRead, Permission.statutoryManage];

const status = (available: readonly string[] = []): StatutoryStatus => ({
  today: '2026-11-01',
  agencies: (['Sss', 'PhilHealth', 'PagIbig', 'WithholdingTax'] as const).map((agency) => ({
    agency,
    label: agency === 'PagIbig' ? 'Pag-IBIG' : agency === 'Sss' ? 'SSS' : agency,
    state: available.includes(agency) ? ('Available' as const) : ('NeedsSetup' as const),
    message: available.includes(agency)
      ? 'Worked out from the table in force since 1 Jan 2026.'
      : 'Not calculated.',
    fallback: available.includes(agency) ? null : 'Enter them as deductions.',
    scheduleId: available.includes(agency) ? `t-${agency}` : null,
    scheduleName: available.includes(agency) ? 'Artificial test table' : null,
    effectiveFrom: available.includes(agency) ? '2026-01-01' : null,
  })),
});

// Artificial test figures only.
const listItem = (
  overrides: Partial<StatutoryScheduleListItem> = {},
): StatutoryScheduleListItem => ({
  id: 't-Sss',
  agency: 'Sss',
  agencyLabel: 'SSS',
  name: 'Artificial test table',
  effectiveFrom: '2026-01-01',
  status: 'Active',
  method: 'Brackets',
  periodBasis: 'Monthly',
  rowCount: 3,
  inForce: true,
  version: 1,
  ...overrides,
});

const schedule = (overrides: Partial<StatutorySchedule> = {}): StatutorySchedule => ({
  id: 't-Sss',
  agency: 'Sss',
  agencyLabel: 'SSS',
  name: 'Artificial test table',
  effectiveFrom: '2026-01-01',
  status: 'Active',
  method: 'Brackets',
  periodBasis: 'Monthly',
  employeeRateBps: null,
  employerRateBps: null,
  baseFloorMinor: null,
  baseCeilingMinor: null,
  employeeMinimumMinor: null,
  employeeMaximumMinor: null,
  employerMinimumMinor: null,
  employerMaximumMinor: null,
  rows: [
    {
      fromMinor: 0,
      toMinor: 100000,
      employeeMinor: 1111,
      employerMinor: 2222,
      baseTaxMinor: 0,
      rateBps: 0,
    },
    {
      fromMinor: 100000,
      toMinor: null,
      employeeMinor: 3333,
      employerMinor: 4444,
      baseTaxMinor: 0,
      rateBps: 0,
    },
  ],
  usedByApprovedPayroll: false,
  inForce: true,
  retiredAtUtc: null,
  version: 3,
  ...overrides,
});

async function open(url: string, permissions: readonly string[], api: Record<string, Mock<Call>>) {
  TestBed.configureTestingModule({
    providers: [
      provideRouter(
        [
          { path: 'payroll/statutory', component: StatutoryPage },
          { path: 'payroll/statutory/new', component: StatutorySchedulePage },
          { path: 'payroll/statutory/:id', component: StatutorySchedulePage },
        ],
        withComponentInputBinding(),
      ),
      provideHttpClient(),
      provideHttpClientTesting(),
      { provide: StatutoryApi, useValue: api },
    ],
  });
  await signInForTest(TestBed.inject(SessionState), TestBed.inject(HttpTestingController), {
    roles: ['Custom'],
    permissions,
  });
  const harness = await RouterTestingHarness.create();
  await harness.navigateByUrl(url);
  await settle(harness.fixture);
  return { harness, fixture: harness.fixture, root: harness.fixture.nativeElement as HTMLElement };
}

describe('StatutoryPage', () => {
  it('says what payroll works out per agency and lists the tables, with Add a table for managers', async () => {
    const { root } = await open('/payroll/statutory', MANAGER, {
      status: stub(() => of(status(['Sss']))),
      list: stub(() => of(pageOf([listItem()]))),
    });

    expect(texts(root, '.agencies__row app-status-chip')).toEqual([
      'Worked out',
      'Needs setup',
      'Needs setup',
      'Needs setup',
    ]);
    expect(texts(root, '.agencies__text .text-muted')[1]).toBe(
      'Entered by hand as a deduction until a table is set up.',
    );
    expect(text(root, '.row-card__title')).toBe('SSS');
    expect(text(root, '.row-card app-status-chip')).toBe('In force');
    expect(texts(root, '.row-card .text-muted')).toEqual([
      'From 1 Jan 2026 · Brackets · per month',
    ]);
    expect(texts(root, 'a.btn')).toContain('Add a table');
  });

  it('gives a reader the tables and the status but nothing to add', async () => {
    const { root } = await open('/payroll/statutory', READER, {
      status: stub(() => of(status())),
      list: stub(() => of(pageOf<StatutoryScheduleListItem>([]))),
    });

    expect(text(root, 'app-empty-state')).toContain('An accountant adds these tables.');
    expect(texts(root, 'a.btn')).not.toContain('Add a table');
  });
});

describe('StatutorySchedulePage', () => {
  it('adds a bracket table, each row starting where the one above ends, and sends centavos', async () => {
    const create = stub(() => of(schedule({ id: 'new1' })));
    const { root, fixture } = await open('/payroll/statutory/new', MANAGER, {
      status: stub(() => of(status())),
      create,
      get: stub(() => of(schedule({ id: 'new1' }))),
    });

    root.querySelector<HTMLInputElement>('input[value="Sss"]')!.click();
    await settle(fixture);
    type(root, '[formControlName="name"]', 'Artificial test table');
    type(root, '[formControlName="effectiveFrom"]', '2027-01-01');
    root.querySelector<HTMLInputElement>('input[value="Brackets"]')!.click();
    await settle(fixture);

    // The first row is there; fill it, add a second, and give the first an end.
    const rowInputs = () => root.querySelectorAll<HTMLInputElement>('.row app-money-input input');
    const set = (input: HTMLInputElement, value: string) => {
      input.value = value;
      input.dispatchEvent(new Event('input'));
      input.dispatchEvent(new Event('blur'));
    };
    set(rowInputs()[0]!, '11.11');
    set(rowInputs()[1]!, '22.22');
    buttonNamed(root, 'Add a row').click();
    await settle(fixture);
    // Row 1 now has an end; the inputs are: row 1 end, employee, employer, row 2 employee, employer.
    set(rowInputs()[0]!, '1,000.00');
    set(rowInputs()[3]!, '33.33');
    set(rowInputs()[4]!, '44.44');
    await settle(fixture);
    expect(texts(root, '.row__title')).toEqual([
      'Row 1: from ₱0.00',
      'Row 2: from ₱1,000.00 and above',
    ]);

    buttonNamed(root, 'Add table').click();
    await settle(fixture);

    expect(create).toHaveBeenCalledTimes(1);
    expect(create.mock.calls[0]![0]).toMatchObject({
      agency: 'Sss',
      name: 'Artificial test table',
      effectiveFrom: '2027-01-01',
      method: 'Brackets',
      periodBasis: 'Monthly',
      employeeRateBps: null,
      rows: [
        {
          fromMinor: 0,
          toMinor: 100000,
          employeeMinor: 1111,
          employerMinor: 2222,
          baseTaxMinor: 0,
          rateBps: 0,
        },
        {
          fromMinor: 100000,
          toMinor: null,
          employeeMinor: 3333,
          employerMinor: 4444,
          baseTaxMinor: 0,
          rateBps: 0,
        },
      ],
      version: 0,
    });
  });

  it('asks for what is missing before sending, and shows the API’s row problems', async () => {
    const create = stub(() =>
      throwError(
        () =>
          new ApiError({
            status: 422,
            title: 'Some details need your attention',
            detail: 'Check the highlighted fields and try again.',
            errors: {
              rows: [
                'Row 2 must start at ₱1.00, where row 1 ends, so there are no gaps or overlaps.',
              ],
            },
          }),
      ),
    );
    const { root, fixture } = await open('/payroll/statutory/new', MANAGER, {
      status: stub(() => of(status())),
      create,
    });

    root.querySelector<HTMLInputElement>('input[value="WithholdingTax"]')!.click();
    await settle(fixture);
    buttonNamed(root, 'Add table').click();
    await settle(fixture);
    expect(create).not.toHaveBeenCalled();
    expect(texts(root, '.field__error')).toEqual([
      'Enter a name for the table.',
      'Choose the date the table takes effect.',
      'Enter the rate (0% when there is none).',
    ]);

    type(root, '[formControlName="name"]', 'Artificial tax');
    type(root, '[formControlName="effectiveFrom"]', '2027-01-01');
    type(root, '.row app-percent-input input', '0');
    buttonNamed(root, 'Add table').click();
    await settle(fixture);
    expect(create.mock.calls[0]![0]).toMatchObject({
      method: 'TaxBrackets',
      agency: 'WithholdingTax',
    });
    expect(text(root, 'app-api-error-alert')).toContain('Row 2 must start at ₱1.00');
  });

  it('offers to copy the table in force for the chosen agency', async () => {
    const get = stub(() => of(schedule()));
    const { root, fixture } = await open('/payroll/statutory/new', MANAGER, {
      status: stub(() => of(status(['Sss']))),
      get,
    });

    root.querySelector<HTMLInputElement>('input[value="Sss"]')!.click();
    await settle(fixture);
    buttonNamed(root, 'Copy from the table in force').click();
    await settle(fixture);

    expect(get).toHaveBeenCalledWith('t-Sss');
    expect(root.querySelector<HTMLInputElement>('[formControlName="name"]')!.value).toBe(
      'Artificial test table',
    );
    expect(root.querySelector<HTMLInputElement>('[formControlName="effectiveFrom"]')!.value).toBe(
      '',
    );
    expect(root.querySelectorAll('.row')).toHaveLength(2);
  });

  it('shows a table an approved payroll used instead of editing it, with copy and retire for managers', async () => {
    const retire = stub(() => of(schedule({ status: 'Retired' })));
    const { root, fixture } = await open('/payroll/statutory/t-Sss', MANAGER, {
      get: stub(() => of(schedule({ usedByApprovedPayroll: true }))),
      retire,
    });

    expect(root.querySelector('form')).toBeNull();
    expect(texts(root, '.view__rows li .view__range')).toEqual([
      '₱0.00 to under ₱1,000.00',
      '₱1,000.00 and above',
    ]);
    expect(text(root, '.card')).toContain(
      "An approved payroll used this table, so it can't be changed.",
    );
    expect(texts(root, '.form__actions .btn')).toEqual(['Retire table', 'Copy to a new table']);

    buttonNamed(root, 'Retire table').click();
    await settle(fixture);
    buttonNamed(root.querySelector('app-dialog')!, 'Retire table').click();
    await settle(fixture);
    expect(retire).toHaveBeenCalledWith('t-Sss', 3);
  });

  it('gives HR the table to read with no way to change it', async () => {
    const { root } = await open('/payroll/statutory/t-Sss', READER, {
      get: stub(() => of(schedule())),
    });

    expect(root.querySelector('form')).toBeNull();
    expect(root.querySelector('.form__actions')).toBeNull();
    expect(text(root, '.text-muted')).toBe('In force from 1 Jan 2026 · Brackets · per month');
  });
});
