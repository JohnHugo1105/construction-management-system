import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Observable, of, throwError } from 'rxjs';
import { SessionState } from '../../core/session/session-state';
import { settle, text, texts, type } from '../../testing/dom';
import { signInForTest } from '../../testing/session-fixture';
import { validationError } from '../workforce/workforce.fixtures';
import { BondFormDialog } from './bond-form-dialog';
import { BondsApi } from './bonds-api';
import { MANAGER, bond } from './bonds.fixtures';
import { Bond } from './bonds.models';

type Call = (...args: unknown[]) => Observable<unknown>;

async function open(options: { bond?: Bond; projectId?: string; api?: Record<string, Call> } = {}) {
  const saved = vi.fn();
  TestBed.configureTestingModule({
    providers: [
      provideHttpClient(),
      provideHttpClientTesting(),
      {
        provide: BondsApi,
        useValue: {
          projects: () =>
            of([
              { id: 'p1', name: 'Riverside Residences', status: 'Active' },
              { id: 'p2', name: 'Harbor Warehouse', status: 'Active' },
            ]),
          ...options.api,
        },
      },
    ],
  });
  await signInForTest(TestBed.inject(SessionState), TestBed.inject(HttpTestingController), {
    roles: ['Custom'],
    permissions: MANAGER,
  });
  const fixture = TestBed.createComponent(BondFormDialog);
  if (options.bond) fixture.componentRef.setInput('bond', options.bond);
  if (options.projectId) fixture.componentRef.setInput('projectId', options.projectId);
  fixture.componentInstance.saved.subscribe(saved);
  await settle(fixture);
  return { fixture, saved };
}

const submit = () =>
  document.body.querySelector<HTMLButtonElement>('button[type="submit"]')!.click();

function fillRequired(): void {
  type(document.body, 'input[formcontrolname="issuerName"]', ' Metro Surety ');
  type(document.body, 'input[formcontrolname="bondNumber"]', 'PB-1');
  type(document.body, 'app-money-input input', '5,000,000');
  type(document.body, 'input[formcontrolname="issuedOn"]', '2026-06-01');
  type(document.body, 'input[formcontrolname="validUntil"]', '2027-06-01');
}

describe('BondFormDialog', () => {
  it('asks which project, whose bond it is and the details, and says it is a record only', async () => {
    await open();

    const labels = texts(document.body, 'app-form-field label, app-field-group legend');
    expect(labels).toEqual(
      expect.arrayContaining([
        'Project',
        'Kind of bond',
        'Bank or surety',
        'Bond number',
        'Amount',
        'Issued on',
        'Valid until',
      ]),
    );
    expect(document.body.textContent).toContain('Whose bond is it?');
    expect(texts(document.body, 'select[formcontrolname="projectId"] option')).toEqual([
      'Choose a project',
      'Riverside Residences',
      'Harbor Warehouse',
    ]);
    expect(document.body.textContent).toContain(
      'It is a record only: it is not added to any cost or bill.',
    );
    // A posted bond names no firm.
    expect(document.body.querySelector('input[formcontrolname="otherPartyName"]')).toBeNull();
  });

  it('shows what is missing and sends nothing until the form is complete', async () => {
    const create = vi.fn(() => of(bond()));
    const { fixture } = await open({ api: { create } });

    submit();
    await settle(fixture);

    expect(create).not.toHaveBeenCalled();
    expect(document.body.textContent).toContain('Choose the project the bond is for.');
    expect(document.body.textContent).toContain('Enter the bank or surety that issued it.');
    expect(document.body.textContent).toContain('Enter the bond amount.');
  });

  it('creates a bond received from a firm with the firm named, trimmed, and one request id', async () => {
    const create = vi.fn(() => of(bond()));
    const { fixture, saved } = await open({ api: { create } });

    const project = document.body.querySelector<HTMLSelectElement>(
      'select[formcontrolname="projectId"]',
    )!;
    project.value = 'p2';
    project.dispatchEvent(new Event('change'));
    const received = Array.from(
      document.body.querySelectorAll<HTMLLabelElement>('label.choice'),
    ).find((label) => label.textContent?.includes('Received from a firm'))!;
    received.querySelector('input')!.click();
    await settle(fixture);
    fillRequired();
    type(document.body, 'input[formcontrolname="otherPartyName"]', ' Dela Cruz Electrical ');
    submit();
    await settle(fixture);

    expect(create).toHaveBeenCalledWith(
      'p2',
      {
        kind: 'Performance',
        issuerName: 'Metro Surety',
        bondNumber: 'PB-1',
        amountMinor: 500_000_000,
        issuedOn: '2026-06-01',
        validUntil: '2027-06-01',
        otherPartyName: 'Dela Cruz Electrical',
        note: null,
        direction: 'Received',
      },
      expect.any(String),
    );
    expect(saved).toHaveBeenCalledOnce();
  });

  it('keeps what was typed and shows the server’s message under the field when a date is refused', async () => {
    const create = vi.fn(() =>
      throwError(() =>
        validationError({
          validUntil: ['The bond can’t be valid until a date before it was issued.'],
        }),
      ),
    );
    const { fixture, saved } = await open({ projectId: 'p1', api: { create } });

    fillRequired();
    submit();
    await settle(fixture);

    expect(saved).not.toHaveBeenCalled();
    expect(
      text(document.body, 'app-form-field:has(input[formcontrolname="validUntil"]) .field__error'),
    ).toContain('can’t be valid until a date before it was issued');
    expect(
      document.body.querySelector<HTMLInputElement>('input[formcontrolname="issuerName"]')!.value,
    ).toBe(' Metro Surety ');
  });

  it('edits an active bond: the details are filled in, the project and direction are not asked, and the version is sent', async () => {
    const update = vi.fn(() => of(bond({ version: 1 })));
    const { fixture, saved } = await open({ bond: bond({ version: 3 }), api: { update } });

    expect(document.body.textContent).not.toContain('Whose bond is it?');
    expect(document.body.querySelector('select[formcontrolname="projectId"]')).toBeNull();
    expect(
      document.body.querySelector<HTMLInputElement>('input[formcontrolname="issuerName"]')!.value,
    ).toBe('Metro Surety');
    expect(
      document.body.querySelector<HTMLInputElement>('input[formcontrolname="validUntil"]')!.value,
    ).toBe('2027-02-10');
    expect(document.body.textContent).toContain('To move it later, use Extend bond.');

    type(document.body, 'input[formcontrolname="issuerName"]', 'Capital Bank');
    submit();
    await settle(fixture);

    expect(update).toHaveBeenCalledWith(
      'b1',
      expect.objectContaining({
        kind: 'Performance',
        issuerName: 'Capital Bank',
        bondNumber: 'PB-2026-014',
        amountMinor: 500_000_000,
        note: 'Original is in the safe',
      }),
      3,
    );
    expect(saved).toHaveBeenCalledOnce();
  });

  it('offers the firm’s name when editing a bond that was received', async () => {
    await open({ bond: bond({ direction: 'Received', otherPartyName: 'Dela Cruz Electrical' }) });

    expect(
      document.body.querySelector<HTMLInputElement>('input[formcontrolname="otherPartyName"]')!
        .value,
    ).toBe('Dela Cruz Electrical');
  });
});
