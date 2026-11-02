import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { Observable, of, throwError } from 'rxjs';
import type { Mock } from 'vitest';
import { ApiError } from '../../../core/api/api-error';
import { SessionState } from '../../../core/session/session-state';
import { buttonNamed, flushMicrotasks, settle, text, texts, type } from '../../../testing/dom';
import { sessionFixture, signInForTest } from '../../../testing/session-fixture';
import { CompanySettingsPage } from './company-settings-page';
import { OrganizationApi } from './organization-api';
import { Organization } from './organization.models';

const company: Organization = {
  id: 'org-1',
  name: 'Acme Builders',
  currencyCode: 'PHP',
  vatRateBps: 1200,
  timeZoneId: 'Asia/Manila',
  hoursPerDay: 8,
  overtimeMultiplierBps: 12500,
  varianceBasis: 'IncludingVat',
  payrollRunsPerMonth: 2,
  timeClockDayStartMinutes: 240,
  unpaidBreakMinutes: 60,
  punchRoundingMinutes: 15,
  version: 3,
};

type Call = (...args: unknown[]) => Observable<unknown>;
const stub = (implementation: Call): Mock<Call> => vi.fn<Call>(implementation);

async function open(update: Mock<Call> = stub(() => of({ ...company, version: 4 }))) {
  TestBed.configureTestingModule({
    providers: [
      provideRouter([{ path: 'admin/settings', component: CompanySettingsPage }]),
      provideHttpClient(),
      provideHttpClientTesting(),
      { provide: OrganizationApi, useValue: { get: stub(() => of(company)), update } },
    ],
  });
  const backend = TestBed.inject(HttpTestingController);
  await signInForTest(TestBed.inject(SessionState), backend);
  const harness = await RouterTestingHarness.create();
  await harness.navigateByUrl('/admin/settings', CompanySettingsPage);
  await settle(harness.fixture);
  return { harness, backend, update, root: harness.fixture.nativeElement as HTMLElement };
}

const save = (root: HTMLElement) => buttonNamed(root, 'Save settings');

describe('CompanySettingsPage', () => {
  it('shows rates as percentages, not basis points', async () => {
    const { root } = await open();

    expect(root.querySelector<HTMLInputElement>('[formControlName="name"]')!.value).toBe(
      'Acme Builders',
    );
    expect(root.querySelector<HTMLInputElement>('app-percent-input input')!.value).toBe('12');
    expect(root.querySelectorAll<HTMLInputElement>('app-percent-input input')[1]!.value).toBe(
      '125',
    );
    expect(root.querySelector<HTMLInputElement>('app-number-input input')!.value).toBe('8');
    expect(root.querySelector<HTMLInputElement>('input[value="IncludingVat"]')!.checked).toBe(true);
  });

  it('explains each way of comparing amounts in one line', async () => {
    const { root } = await open();

    expect(texts(root, '.choice__label')).toEqual(['Include VAT', 'Exclude VAT']);
    expect(texts(root, '.choice__hint')).toEqual([
      'Compare budget and actual cost with VAT added.',
      'Compare amounts before VAT. Choose this if your company gets back the VAT it pays.',
    ]);
  });

  it('sends the VAT rate and overtime rate as basis points, with the version it loaded', async () => {
    const { harness, root, update, backend } = await open();

    type(root, 'app-percent-input input', '7.5');
    root.querySelector<HTMLInputElement>('input[value="ExcludingVat"]')!.click();
    save(root).click();
    await settle(harness.fixture);

    expect(update).toHaveBeenCalledWith({
      name: 'Acme Builders',
      currencyCode: 'PHP',
      vatRateBps: 750,
      timeZoneId: 'Asia/Manila',
      hoursPerDay: 8,
      overtimeMultiplierBps: 12500,
      varianceBasis: 'ExcludingVat',
      payrollRunsPerMonth: 2,
      timeClockDayStartMinutes: 240,
      unpaidBreakMinutes: 60,
      punchRoundingMinutes: 15,
      version: 3,
    });
    // The session is read again so the new currency, time zone and VAT rate apply at once.
    await flushMicrotasks(6);
    backend.expectOne('/api/identity/session').flush(sessionFixture({ vatRateBps: 750 }));
    await settle(harness.fixture);
    expect(TestBed.inject(SessionState).vatRateBps()).toBe(750);
  });

  it('upper-cases the currency code and trims the name', async () => {
    const { harness, root, update, backend } = await open();

    type(root, '[formControlName="name"]', '  Acme Builders Inc  ');
    type(root, '[formControlName="currencyCode"]', 'usd');
    save(root).click();
    await settle(harness.fixture);

    expect(update.mock.calls[0]![0]).toMatchObject({
      name: 'Acme Builders Inc',
      currencyCode: 'USD',
    });
    await flushMicrotasks(6);
    backend.expectOne('/api/identity/session').flush(sessionFixture());
  });

  it('refuses a VAT rate above 100% in plain words, and sends nothing', async () => {
    const { harness, root, update } = await open();

    type(root, 'app-percent-input input', '120');
    save(root).click();
    await settle(harness.fixture);

    expect(texts(root, '.field__error')).toEqual(['Enter a VAT rate between 0% and 100%.']);
    expect(update).not.toHaveBeenCalled();
    expect(text(root, '.alert__title')).toBe('Some details need your attention');
  });

  it('refuses paid hours outside 1 to 24', async () => {
    const { harness, root, update } = await open();

    type(root, 'app-number-input input', '30');
    save(root).click();
    await settle(harness.fixture);

    expect(texts(root, '.field__error')).toEqual(['Enter paid hours per day between 1 and 24.']);
    expect(update).not.toHaveBeenCalled();
  });

  it('shows and sends the pay runs a month, and refuses more than 4', async () => {
    const { harness, root, update, backend } = await open();
    const runs = root.querySelectorAll<HTMLInputElement>('app-number-input input')[1]!;
    expect(runs.value).toBe('2');

    type(root, '[formControlName="payrollRunsPerMonth"] input', '5');
    save(root).click();
    await settle(harness.fixture);
    expect(texts(root, '.field__error')).toEqual([
      'Enter how many pay runs you make in a month, from 1 to 4.',
    ]);
    expect(update).not.toHaveBeenCalled();

    type(root, '[formControlName="payrollRunsPerMonth"] input', '4');
    save(root).click();
    await settle(harness.fixture);
    expect(update.mock.calls[0]![0]).toMatchObject({ payrollRunsPerMonth: 4 });
    await flushMicrotasks(6);
    backend.expectOne('/api/identity/session').flush(sessionFixture());
  });

  it('shows the time clock day start as a time and sends it, the break and the rounding in minutes', async () => {
    const { harness, root, update, backend } = await open();
    const dayStart = root.querySelector<HTMLInputElement>('[formControlName="timeClockDayStart"]')!;
    expect(dayStart.value).toBe('04:00');
    expect(
      root.querySelector<HTMLInputElement>('[formControlName="unpaidBreakMinutes"] input')!.value,
    ).toBe('60');
    expect(
      root.querySelector<HTMLInputElement>('[formControlName="punchRoundingMinutes"] input')!.value,
    ).toBe('15');

    type(root, '[formControlName="timeClockDayStart"]', '05:30');
    type(root, '[formControlName="unpaidBreakMinutes"] input', '30');
    type(root, '[formControlName="punchRoundingMinutes"] input', '0');
    save(root).click();
    await settle(harness.fixture);

    expect(update.mock.calls[0]![0]).toMatchObject({
      timeClockDayStartMinutes: 330,
      unpaidBreakMinutes: 30,
      punchRoundingMinutes: 0,
    });
    await flushMicrotasks(6);
    backend.expectOne('/api/identity/session').flush(sessionFixture());
  });

  it('refuses a day start after noon and a break or rounding out of range, in plain words', async () => {
    const { harness, root, update } = await open();

    type(root, '[formControlName="timeClockDayStart"]', '13:00');
    type(root, '[formControlName="unpaidBreakMinutes"] input', '200');
    type(root, '[formControlName="punchRoundingMinutes"] input', '61');
    save(root).click();
    await settle(harness.fixture);

    expect(texts(root, '.field__error')).toEqual([
      'Choose a day start between midnight and noon.',
      'Enter an unpaid break from 0 to 180 minutes. Use 0 to take no break off.',
      'Enter rounding from 0 to 60 minutes. Use 0 to keep the exact minutes.',
    ]);
    expect(update).not.toHaveBeenCalled();
  });

  it('puts the API message for the VAT rate under the VAT field, whatever unit the API uses', async () => {
    const update = stub(() =>
      throwError(
        () =>
          new ApiError({
            status: 422,
            title: 'Some details need your attention',
            detail: 'Check the highlighted fields and try again.',
            errors: {
              vatRateBps: ['Enter a VAT rate between 0% and 100%.'],
              timeZoneId: ['Choose a valid time zone.'],
            },
          }),
      ),
    );
    const { harness, root } = await open(update);

    save(root).click();
    await settle(harness.fixture);

    const vatField = root.querySelector('app-percent-input')!.closest('app-form-field')!;
    expect(text(vatField, '.field__error')).toBe('Enter a VAT rate between 0% and 100%.');
    const zoneField = root
      .querySelector('[formControlName="timeZoneId"]')!
      .closest('app-form-field')!;
    expect(text(zoneField, '.field__error')).toBe('Choose a valid time zone.');
  });
});
