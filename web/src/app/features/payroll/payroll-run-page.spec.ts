import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { Observable, of, throwError } from 'rxjs';
import type { Mock } from 'vitest';
import { Permission } from '../../core/session/permissions';
import { SessionState } from '../../core/session/session-state';
import { Viewport } from '../../shared/ui/viewport/viewport';
import { buttonNamed, settle, text, texts, type } from '../../testing/dom';
import { signInForTest } from '../../testing/session-fixture';
import { conflict, ruleViolation, validationError } from '../workforce/workforce.fixtures';
import { PayrollApi } from './payroll-api';
import { PayrollRunPage } from './payroll-run-page';
import { run } from './payroll.fixtures';
import { PayrollRun } from './payroll.models';

type Call = (...args: unknown[]) => Observable<unknown>;
const stub = (implementation: Call): Mock<Call> => vi.fn<Call>(implementation);

const READER = [Permission.projectsRead, Permission.workforceRead, Permission.payrollRead];
const PREPARER = [...READER, Permission.payrollRun];
const APPROVER = [...PREPARER, Permission.payrollApprove];

const approved = (): PayrollRun =>
  run({
    status: 'Approved',
    approvedByName: 'Alma Accountant',
    approvedAtUtc: '2026-11-01T09:00:00Z',
    version: 3,
  });

async function open(
  current: PayrollRun,
  permissions: readonly string[],
  api: Record<string, Mock<Call>> = {},
) {
  const get = stub(() => of(current));
  TestBed.configureTestingModule({
    providers: [
      provideRouter(
        [{ path: 'payroll/:id', component: PayrollRunPage }],
        withComponentInputBinding(),
      ),
      provideHttpClient(),
      provideHttpClientTesting(),
      { provide: PayrollApi, useValue: { get, ...api } },
      { provide: Viewport, useValue: { isTablet: signal(true), isDesktop: signal(true) } },
    ],
  });
  await signInForTest(TestBed.inject(SessionState), TestBed.inject(HttpTestingController), {
    roles: ['Custom'],
    permissions,
  });
  const harness = await RouterTestingHarness.create();
  await harness.navigateByUrl(`/payroll/${current.id}`, PayrollRunPage);
  await settle(harness.fixture);
  return {
    harness,
    fixture: harness.fixture,
    root: harness.fixture.nativeElement as HTMLElement,
    get,
  };
}

const actionButtons = (root: HTMLElement) => texts(root, '.head__actions .btn');

describe('PayrollRunPage figures', () => {
  it('shows the period, the state and the API’s totals, formatted in the company currency', async () => {
    const { root } = await open(run(), READER);

    expect(text(root, '.head app-status-chip')).toBe('Draft');
    expect(text(root, '.head__workers')).toBe('2 workers');
    expect(texts(root, '.totals__item')).toEqual([
      'Net pay₱12,475.00',
      'Gross pay₱12,625.00',
      'Deductions₱150.00',
    ]);
    expect(text(root, '.head__history')).toContain('Prepared by Hannah HR-Officer');
    const rows = root.querySelectorAll('tbody tr');
    expect(texts(rows[0]!, 'th, td').slice(0, 5)).toEqual([
      'Juan Dela Cruz W-001 · Foreman',
      '80 h + 4 h overtime',
      '₱8,625.00',
      '₱0.00',
      '₱8,625.00',
    ]);
    expect(texts(rows[1]!, 'td').slice(1, 4)).toEqual(['₱4,000.00', '₱150.00', '₱3,850.00']);
  });

  it('opens a worker to show how the pay is made up, what is deducted and where it is charged', async () => {
    const { root, fixture } = await open(run(), READER);

    root.querySelectorAll<HTMLButtonElement>('tbody button[aria-expanded]')[1]!.click();
    await settle(fixture);

    const detail = root.querySelector('.detail-row')!;
    expect(texts(detail, '.detail__pairs dd')).toEqual(['₱4,000.00', '₱0.00']);
    expect(texts(detail, 'section[aria-label^="Deductions"] li')).toEqual([
      'SSS contribution₱150.00',
    ]);
    expect(texts(detail, 'section[aria-label^="Projects"] li')).toEqual([
      'Harbour Annex 40 h₱4,000.00',
    ]);
  });

  it('shows the reason on a voided payroll and offers nothing to do with it', async () => {
    const { root } = await open(
      run({
        status: 'Voided',
        voidReason: 'Wrong period',
        voidedByName: 'Hannah HR-Officer',
        voidedAtUtc: '2026-11-01T10:00:00Z',
      }),
      APPROVER,
    );

    expect(text(root, 'app-alert')).toContain('Reason: Wrong period');
    expect(actionButtons(root)).toEqual([]);
    expect(texts(root, 'button')).not.toContain('Edit deductions');
  });
});

describe('PayrollRunPage who can do what', () => {
  it('lets a preparer enter deductions and void a draft, and tells them an accountant approves', async () => {
    const { root } = await open(run(), PREPARER);

    expect(actionButtons(root)).toEqual(['Void payroll']);
    expect(texts(root, 'button')).toContain('Edit deductions');
    expect(text(root, 'app-alert .alert__title')).toBe('An accountant approves payroll');
    expect(text(root, 'app-alert')).toContain('ask an accountant to approve this payroll');
  });

  it('gives an approver the approve button and no note', async () => {
    const { root } = await open(run(), APPROVER);

    expect(actionButtons(root)).toEqual(['Approve payroll', 'Void payroll']);
    expect(root.querySelector('app-alert')).toBeNull();
  });

  it('gives a person who may only read the figures and no action buttons', async () => {
    const { root } = await open(run(), READER);

    expect(actionButtons(root)).toEqual([]);
    expect(texts(root, 'button')).not.toContain('Edit deductions');
    expect(texts(root, 'button')).not.toContain('Approve payroll');
    // They are told what the payroll is waiting for, without being offered it.
    expect(text(root, 'app-alert')).toContain('waiting for an accountant to approve it');
  });

  it('lets only an approver void an approved payroll, and offers the register to everyone who reads', async () => {
    const preparer = await open(approved(), PREPARER);
    expect(actionButtons(preparer.root)).toEqual(['Export register']);
    expect(preparer.root.querySelector('app-alert')).toBeNull();
    TestBed.resetTestingModule();

    const { root } = await open(approved(), APPROVER);
    expect(actionButtons(root)).toEqual(['Export register', 'Void payroll']);
  });

  it('links the register to the export of this run, as a plain download link without any pay in it', async () => {
    const { root } = await open(approved(), READER);

    const link = root.querySelector<HTMLAnchorElement>('a[download]')!;
    expect(link.textContent?.trim()).toBe('Export register');
    expect(link.getAttribute('href')).toBe('/api/payroll-runs/run1/export');
    // A draft has no register to export yet.
    TestBed.resetTestingModule();
    const draft = await open(run(), READER);
    expect(draft.root.querySelector('a[download]')).toBeNull();
  });
});

describe('PayrollRunPage approving', () => {
  it('names what approving does: the projects and the amount each is charged, and the total', async () => {
    const { root, fixture } = await open(run(), APPROVER);

    buttonNamed(root, 'Approve payroll').click();
    await settle(fixture);

    const dialog = root.querySelector('app-approve-run-dialog')!;
    expect(text(dialog, '.dialog__title')).toBe('Approve this payroll?');
    expect(text(dialog, '.confirm-message')).toBe(
      'Approving locks this attendance and posts the labor cost to:',
    );
    expect(texts(dialog, '.postings__row')).toEqual([
      'Harbour Annex₱4,000.00',
      'Riverside Residences₱4,312.50',
      'Riverside Residences, Tower A₱4,312.50',
      'Total₱12,625.00',
    ]);
    expect(text(dialog, '.postings__note')).toBe(
      'Nobody can change this attendance until the payroll is voided.',
    );
  });

  it('approves the version the person opened, then shows the approved payroll', async () => {
    const approve = stub(() => of(approved()));
    const { root, fixture } = await open(run({ version: 2 }), APPROVER, { approve });

    buttonNamed(root, 'Approve payroll').click();
    await settle(fixture);
    buttonNamed(root.querySelector('app-approve-run-dialog')!, 'Approve payroll').click();
    await settle(fixture);

    expect(approve).toHaveBeenCalledWith('run1', 2);
    expect(root.querySelector('app-approve-run-dialog')).toBeNull();
    expect(text(root, '.head app-status-chip')).toBe('Approved');
    expect(actionButtons(root)).toEqual(['Export register', 'Void payroll']);
  });

  it('keeps the dialog open on a refusal, says it in the API’s words and offers to reload', async () => {
    const approve = stub(() =>
      throwError(() => ruleViolation('This payroll is already approved.')),
    );
    const { root, fixture, get } = await open(run(), APPROVER, { approve });

    buttonNamed(root, 'Approve payroll').click();
    await settle(fixture);
    const dialog = () => root.querySelector('app-approve-run-dialog')!;
    buttonNamed(dialog(), 'Approve payroll').click();
    await settle(fixture);

    expect(dialog()).not.toBeNull();
    expect(text(dialog(), 'app-api-error-alert')).toContain('This payroll is already approved.');
    expect(root.querySelector('app-approve-run-dialog dialog[open]')).not.toBeNull();

    get.mockReturnValue(of(approved()));
    buttonNamed(dialog(), 'Reload payroll').click();
    await settle(fixture);

    expect(get).toHaveBeenCalledTimes(2);
    expect(root.querySelector('app-approve-run-dialog')).toBeNull();
    expect(text(root, '.head app-status-chip')).toBe('Approved');
  });

  it('offers the same reload when someone else changed the payroll first', async () => {
    const approve = stub(() => throwError(() => conflict()));
    const { root, fixture } = await open(run(), APPROVER, { approve });

    buttonNamed(root, 'Approve payroll').click();
    await settle(fixture);
    buttonNamed(root.querySelector('app-approve-run-dialog')!, 'Approve payroll').click();
    await settle(fixture);

    expect(text(root, 'app-approve-run-dialog app-api-error-alert')).toContain(
      'Someone else changed this record',
    );
    expect(texts(root, 'app-approve-run-dialog app-api-error-alert button')).toEqual([
      'Reload payroll',
    ]);
  });
});

describe('PayrollRunPage voiding', () => {
  it('says a draft only releases its attendance, and asks for a reason', async () => {
    const voidRun = stub(() => of(run({ status: 'Voided', voidReason: 'Prepared by mistake' })));
    const { root, fixture } = await open(run(), PREPARER, { void: voidRun });

    buttonNamed(root, 'Void payroll').click();
    await settle(fixture);
    const dialog = root.querySelector('app-void-run-dialog')!;
    expect(text(dialog, '.dialog__body p')).toBe(
      'Voiding this draft releases its attendance so it can be paid in a new payroll. The draft stays on record.',
    );
    expect(dialog.querySelector('.postings')).toBeNull();

    buttonNamed(dialog, 'Void payroll').click();
    await settle(fixture);
    expect(voidRun).not.toHaveBeenCalled();
    expect(text(dialog, '.field__error')).toBe('Say why the payroll is being voided.');

    type(dialog, 'textarea', 'Prepared by mistake');
    buttonNamed(dialog, 'Void payroll').click();
    await settle(fixture);

    expect(voidRun).toHaveBeenCalledWith('run1', 'Prepared by mistake', 2);
    expect(text(root, '.head app-status-chip')).toBe('Voided');
    expect(actionButtons(root)).toEqual([]);
  });

  it('says an approved payroll also reverses the labor cost already posted, and to which projects', async () => {
    const { root, fixture } = await open(approved(), APPROVER);

    buttonNamed(root, 'Void payroll').click();
    await settle(fixture);

    const dialog = root.querySelector('app-void-run-dialog')!;
    expect(text(dialog, '.dialog__body p')).toBe(
      'Voiding an approved payroll unlocks the attendance and reverses the labor cost posted to the projects:',
    );
    expect(texts(dialog, '.postings__row')).toEqual([
      'Harbour Annex₱4,000.00',
      'Riverside Residences₱4,312.50',
      'Riverside Residences, Tower A₱4,312.50',
    ]);
  });

  it('shows a stale version as a reload, not as a failure', async () => {
    const voidRun = stub(() => throwError(() => conflict()));
    const { root, fixture } = await open(run(), PREPARER, { void: voidRun });

    buttonNamed(root, 'Void payroll').click();
    await settle(fixture);
    const dialog = root.querySelector('app-void-run-dialog')!;
    type(dialog, 'textarea', 'Wrong period');
    buttonNamed(dialog, 'Void payroll').click();
    await settle(fixture);

    expect(texts(dialog, 'app-api-error-alert button')).toEqual(['Reload payroll']);
  });
});

describe('PayrollRunPage deductions', () => {
  it('opens with the worker’s deductions, and sends what is typed in centavos with the run’s version', async () => {
    const setDeductions = stub(() => of(run({ version: 3 })));
    const { root, fixture } = await open(run({ version: 2 }), PREPARER, { setDeductions });

    root.querySelectorAll<HTMLButtonElement>('tbody button')[0]!.click();
    // The first button on the first row is the worker's "Edit deductions".
    await settle(fixture);
    const dialog = root.querySelector('app-deductions-dialog')!;
    expect(text(dialog, '.dialog__body p')).toContain(
      'Juan Dela Cruz earned ₱8,625.00 in this payroll.',
    );
    expect(text(dialog, '.dialog__body p:nth-of-type(2)')).toContain("aren't calculated for you");
    expect(dialog.querySelectorAll('.row')).toHaveLength(0);

    buttonNamed(dialog, 'Add a deduction').click();
    await settle(fixture);
    type(dialog, '[formControlName="description"]', ' PhilHealth ');
    type(dialog, 'app-money-input input', '1,250.50');
    buttonNamed(dialog, 'Save deductions').click();
    await settle(fixture);

    expect(setDeductions).toHaveBeenCalledWith(
      'run1',
      'w1',
      [{ description: 'PhilHealth', amountMinor: 125050 }],
      2,
    );
    expect(root.querySelector('app-deductions-dialog')).toBeNull();
  });

  it('shows the API’s refusal when deductions are more than the pay, and keeps what was typed', async () => {
    const setDeductions = stub(() =>
      throwError(() =>
        validationError({
          deductions: ['Deductions for Juan Dela Cruz are more than the pay. Check the amounts.'],
        }),
      ),
    );
    const { root, fixture } = await open(run(), PREPARER, { setDeductions });

    root.querySelectorAll<HTMLButtonElement>('tbody button')[0]!.click();
    await settle(fixture);
    const dialog = root.querySelector('app-deductions-dialog')!;
    buttonNamed(dialog, 'Add a deduction').click();
    await settle(fixture);
    type(dialog, '[formControlName="description"]', 'Advance');
    type(dialog, 'app-money-input input', '99999');
    buttonNamed(dialog, 'Save deductions').click();
    await settle(fixture);

    expect(text(dialog, 'app-alert .alert__title')).toBe("The deductions couldn't be saved");
    expect(text(dialog, 'app-alert')).toContain(
      'Deductions for Juan Dela Cruz are more than the pay.',
    );
    expect(dialog.querySelector<HTMLInputElement>('app-money-input input')!.value).toBe('99999');
    expect(root.querySelector('app-deductions-dialog dialog[open]')).not.toBeNull();
  });

  it('asks for what is missing in a half-filled row before sending anything', async () => {
    const setDeductions = stub(() => of(run()));
    const { root, fixture } = await open(run(), PREPARER, { setDeductions });

    root.querySelectorAll<HTMLButtonElement>('tbody button')[0]!.click();
    await settle(fixture);
    const dialog = root.querySelector('app-deductions-dialog')!;
    buttonNamed(dialog, 'Add a deduction').click();
    await settle(fixture);
    type(dialog, 'app-money-input input', '100');
    buttonNamed(dialog, 'Save deductions').click();
    await settle(fixture);

    expect(setDeductions).not.toHaveBeenCalled();
    expect(text(dialog, '.field__error')).toBe(
      'Describe the deduction, for example SSS contribution.',
    );
  });

  it('gives a person who may not prepare payroll no way to change deductions', async () => {
    const { root } = await open(run(), READER);
    expect(texts(root, 'button')).not.toContain('Edit deductions');
  });
});
