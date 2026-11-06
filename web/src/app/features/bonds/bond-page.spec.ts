import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { Observable, of, throwError } from 'rxjs';
import type { Mock } from 'vitest';
import { SessionState } from '../../core/session/session-state';
import { Viewport } from '../../shared/ui/viewport/viewport';
import { buttonNamed, settle, text, texts, type } from '../../testing/dom';
import { signInForTest } from '../../testing/session-fixture';
import { ruleViolation } from '../workforce/workforce.fixtures';
import { BondPage } from './bond-page';
import { BondsApi } from './bonds-api';
import { MANAGER, READER, bond, event } from './bonds.fixtures';
import { Bond } from './bonds.models';

type Call = (...args: unknown[]) => Observable<unknown>;
const stub = (implementation: Call): Mock<Call> => vi.fn<Call>(implementation);

async function open(
  current: Bond,
  permissions: readonly string[],
  api: Record<string, Mock<Call>> = {},
) {
  TestBed.configureTestingModule({
    providers: [
      provideRouter([{ path: 'bonds/:id', component: BondPage }], withComponentInputBinding()),
      provideHttpClient(),
      provideHttpClientTesting(),
      {
        provide: BondsApi,
        useValue: { get: stub(() => of(current)), projects: stub(() => of([])), ...api },
      },
      { provide: Viewport, useValue: { isTablet: signal(false), isDesktop: signal(false) } },
    ],
  });
  await signInForTest(TestBed.inject(SessionState), TestBed.inject(HttpTestingController), {
    roles: ['Custom'],
    permissions,
  });
  const harness = await RouterTestingHarness.create();
  await harness.navigateByUrl(`/bonds/${current.id}`, BondPage);
  await settle(harness.fixture);
  return { fixture: harness.fixture, root: harness.fixture.nativeElement as HTMLElement };
}

const attention = bond({ expiry: 'Expired', daysLeft: -4, validUntil: '2026-10-29' });

describe('BondPage', () => {
  it('shows where the bond stands, the amount, the dates and the history, and a reader no buttons', async () => {
    const { root } = await open(bond(), READER);

    expect(text(root, '.page-header__back')).toBe('All bonds');
    expect(text(root, '.head app-status-chip')).toBe('Valid');
    expect(text(root, '.head__who')).toBe('Posted to the client · Riverside Residences');
    expect(texts(root, '.figures__item')).toEqual([
      'Amount₱5,000,000.00 Guaranteed by Metro Surety. Not counted in any cost or bill.',
      'Valid until10 Feb 2027',
      'Issued on1 Jun 2026',
    ]);
    expect(root.textContent).toContain('Bond number PB-2026-014');
    expect(root.textContent).toContain('Original is in the safe');
    expect(texts(root, '.history__item p')).toEqual([
      'Recorded',
      'Alma Accountant, 2 Nov 2026, 10:15 am',
    ]);
    expect(root.querySelector('.actions')).toBeNull();
    expect(root.querySelector('app-alert')).toBeNull();
  });

  it('leads an expired bond with Extend bond and says what to do', async () => {
    const { root } = await open(attention, MANAGER);

    expect(text(root, 'app-alert')).toContain('This bond has expired');
    expect(text(root, 'app-alert')).toContain('valid until 29 Oct 2026 and is still active');
    expect(text(root, '.figures__item:nth-child(2)')).toBe(
      'Valid until29 Oct 2026Expired 4 days ago',
    );
    expect(root.querySelector('.actions .btn--primary')!.textContent?.trim()).toBe('Extend bond');
    expect(texts(root, '.actions > button, .actions app-menu > .menu > button')).toEqual([
      'Extend bond',
      'Release bond',
      'Change details',
      'More',
    ]);
  });

  it('keeps every step secondary on a bond that is not about to lapse', async () => {
    const { root } = await open(bond(), MANAGER);

    expect(root.querySelector('.actions .btn--primary')).toBeNull();
    expect(root.querySelector('app-alert')).toBeNull();
  });

  it('shows an expiring bond with the days left', async () => {
    const { root } = await open(
      bond({ expiry: 'ExpiringSoon', daysLeft: 12, validUntil: '2026-11-14' }),
      MANAGER,
    );

    expect(text(root, '.head app-status-chip')).toBe('Expires in 12 days');
    expect(text(root, 'app-alert')).toContain('Expires in 12 days');
  });

  it('extends the bond to a later date and shows the server’s answer', async () => {
    const extended = bond({
      validUntil: '2027-05-10',
      version: 2,
      history: [
        event({
          id: 'e2',
          kind: 'Extended',
          previousValidUntil: '2026-10-29',
          newValidUntil: '2027-05-10',
        }),
        event(),
      ],
    });
    const extend = stub(() => of(extended));
    const { fixture, root } = await open(attention, MANAGER, { extend });

    buttonNamed(root, 'Extend bond').click();
    await settle(fixture);
    expect(document.body.textContent).toContain('It is valid until 29 Oct 2026 now.');
    type(document.body, 'input[type="date"]', '2027-05-10');
    type(document.body, 'textarea', 'Bank renewed it for 6 months');
    document.body.querySelector<HTMLButtonElement>('button[type="submit"]')!.click();
    await settle(fixture);

    expect(extend).toHaveBeenCalledWith('b1', '2027-05-10', 'Bank renewed it for 6 months', 0);
    expect(root.querySelector('app-alert')).toBeNull();
    expect(text(root, '.head app-status-chip')).toBe('Valid');
    expect(texts(root, '.history__item > p:first-child')).toEqual([
      'Extended from 29 Oct 2026 to 10 May 2027',
      'Recorded',
    ]);
  });

  it('needs a note to mark a bond as called and sends the date and note', async () => {
    const called = bond({
      status: 'Called',
      expiry: null,
      daysLeft: null,
      calledOn: '2026-11-02',
      callNote: 'Claimed in full',
      version: 1,
    });
    const markCalled = stub(() => of(called));
    const { fixture, root } = await open(bond(), MANAGER, { markCalled });

    buttonNamed(root, 'More').click();
    await settle(fixture);
    buttonNamed(root, 'Mark as called').click();
    await settle(fixture);
    document.body.querySelector<HTMLButtonElement>('button[type="submit"]')!.click();
    await settle(fixture);
    expect(markCalled).not.toHaveBeenCalled();
    expect(document.body.textContent).toContain('Say what was claimed.');

    type(document.body, 'textarea', 'Claimed in full');
    document.body.querySelector<HTMLButtonElement>('button[type="submit"]')!.click();
    await settle(fixture);

    expect(markCalled).toHaveBeenCalledWith(
      'b1',
      expect.stringMatching(/^\d{4}-\d{2}-\d{2}$/),
      'Claimed in full',
      0,
    );
    expect(text(root, '.head app-status-chip')).toBe('Called');
    expect(root.textContent).toContain('Called on 2 Nov 2026. Claimed in full');
    expect(root.querySelector('.actions .btn:not(.btn--ghost)')).toBeNull();
    expect(texts(root, '.actions button')).toEqual(['Void bond']);
  });

  it('releases a bond and voids one with a reason', async () => {
    const released = bond({
      status: 'Released',
      expiry: null,
      daysLeft: null,
      releasedOn: '2026-11-02',
      version: 1,
    });
    const release = stub(() => of(released));
    const { fixture, root } = await open(bond(), MANAGER, { release });

    buttonNamed(root, 'Release bond').click();
    await settle(fixture);
    document.body.querySelector<HTMLButtonElement>('button[type="submit"]')!.click();
    await settle(fixture);
    expect(release).toHaveBeenCalledWith(
      'b1',
      expect.stringMatching(/^\d{4}-\d{2}-\d{2}$/),
      null,
      0,
    );
    expect(text(root, '.head app-status-chip')).toBe('Released');
  });

  it('voids a bond only with a reason', async () => {
    const voided = bond({
      status: 'Void',
      expiry: null,
      daysLeft: null,
      voidReason: 'Entered twice',
      version: 1,
    });
    const voidBond = stub(() => of(voided));
    const { fixture, root } = await open(bond(), MANAGER, { voidBond });

    buttonNamed(root, 'More').click();
    await settle(fixture);
    buttonNamed(root, 'Void bond').click();
    await settle(fixture);
    document.body.querySelector<HTMLButtonElement>('button[type="submit"]')!.click();
    await settle(fixture);
    expect(voidBond).not.toHaveBeenCalled();
    expect(document.body.textContent).toContain('Give a reason.');

    type(document.body, 'textarea', 'Entered twice');
    document.body.querySelector<HTMLButtonElement>('button[type="submit"]')!.click();
    await settle(fixture);

    expect(voidBond).toHaveBeenCalledWith('b1', 'Entered twice', 0);
    expect(text(root, 'app-alert')).toContain('Reason: Entered twice');
    expect(root.querySelector('.actions')).toBeNull();
  });

  it('keeps the dialog open with the server’s words when someone else changed the bond first', async () => {
    const release = stub(() =>
      throwError(() => ruleViolation('This bond was released, so it can’t be released.')),
    );
    const { fixture, root } = await open(bond(), MANAGER, { release });

    buttonNamed(root, 'Release bond').click();
    await settle(fixture);
    document.body.querySelector<HTMLButtonElement>('button[type="submit"]')!.click();
    await settle(fixture);

    expect(document.body.textContent).toContain('This bond was released, so it can’t be released.');
    expect(document.body.querySelector('dialog')).not.toBeNull();
  });

  it('offers no step on a bond whose project is archived, and says why', async () => {
    const { root } = await open(bond({ projectArchived: true }), MANAGER);

    expect(root.querySelector('.actions')).toBeNull();
    expect(root.textContent).toContain(
      'This project is archived, so this bond can be read but not changed.',
    );
  });

  it('leaves a released bond only the void step', async () => {
    const { root } = await open(
      bond({
        status: 'Released',
        expiry: null,
        daysLeft: null,
        releasedOn: '2026-11-02',
        releaseNote: 'Returned by the client',
      }),
      MANAGER,
    );

    expect(texts(root, '.actions button')).toEqual(['Void bond']);
    expect(root.textContent).toContain('Released on 2 Nov 2026. Returned by the client');
  });
});
