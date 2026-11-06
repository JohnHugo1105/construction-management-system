import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { DestroyRef } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Permission } from '../../core/session/permissions';
import { SessionState } from '../../core/session/session-state';
import { signInForTest } from '../../testing/session-fixture';
import { NeedsActionState, badgeText } from './needs-action-state';

const COUNT = '/api/needs-action/count';

async function setup(permissions?: readonly string[]) {
  TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
  const http = TestBed.inject(HttpTestingController);
  await signInForTest(
    TestBed.inject(SessionState),
    http,
    permissions ? { roles: ['Custom'], permissions } : {},
  );
  return { state: TestBed.inject(NeedsActionState), http };
}

/** A DestroyRef the test ends by hand, standing in for the shell going away. */
function fakeDestroyRef() {
  const callbacks: (() => void)[] = [];
  const ref = {
    onDestroy: (callback: () => void) => {
      callbacks.push(callback);
      return () => undefined;
    },
  } as unknown as DestroyRef;
  return { ref, destroy: () => callbacks.forEach((callback) => callback()) };
}

function becomeVisible(state: DocumentVisibilityState = 'visible'): void {
  Object.defineProperty(document, 'visibilityState', { configurable: true, get: () => state });
  document.dispatchEvent(new Event('visibilitychange'));
}

describe('badgeText', () => {
  it('is empty at 0, the number up to 99, then 99+', () => {
    expect([0, -1, 1, 6, 99, 100, 250].map(badgeText)).toEqual([
      '',
      '',
      '1',
      '6',
      '99',
      '99+',
      '99+',
    ]);
  });
});

describe('NeedsActionState', () => {
  afterEach(() => {
    Object.defineProperty(document, 'visibilityState', {
      configurable: true,
      get: () => 'visible',
    });
  });

  it('holds the number the server counts, as a signal', async () => {
    const { state, http } = await setup();
    expect(state.count()).toBe(0);

    state.refresh();
    http.expectOne(COUNT).flush({ total: 6 });

    expect(state.count()).toBe(6);
    expect(state.badge()).toBe('6');
  });

  it('never asks for someone whose role acts on nothing (a Viewer)', async () => {
    const { state, http } = await setup([
      Permission.projectsRead,
      Permission.costsRead,
      Permission.actionsRead,
    ]);

    state.refresh();

    http.expectNone(COUNT);
    expect(state.applies()).toBe(false);
    expect(state.count()).toBe(0);
  });

  it('keeps the last number when the count cannot be read', async () => {
    const { state, http } = await setup();
    state.refresh();
    http.expectOne(COUNT).flush({ total: 4 });

    state.refresh();
    http.expectOne(COUNT).flush(null, { status: 500, statusText: 'Server Error' });

    expect(state.count()).toBe(4);
  });

  it('takes the page total, so the badge and the page agree without another request', async () => {
    const { state, http } = await setup();

    state.setTotal(132);

    http.expectNone(COUNT);
    expect(state.count()).toBe(132);
    expect(state.badge()).toBe('99+');
  });

  it('refreshes when the browser tab becomes visible again, and stops once the shell is gone', async () => {
    vi.useFakeTimers({ toFake: ['Date'] });
    vi.setSystemTime(new Date('2026-11-02T08:00:00Z'));
    const { state, http } = await setup();
    const { ref, destroy } = fakeDestroyRef();
    state.refreshWhenVisible(ref);

    becomeVisible('hidden');
    http.expectNone(COUNT);

    becomeVisible('visible');
    http.expectOne(COUNT).flush({ total: 2 });
    expect(state.count()).toBe(2);

    // Coming back again straight away does not ask again; after 30 seconds it does.
    becomeVisible('visible');
    http.expectNone(COUNT);
    vi.setSystemTime(Date.now() + 31_000);
    becomeVisible('visible');
    http.expectOne(COUNT).flush({ total: 3 });
    expect(state.count()).toBe(3);

    destroy();
    vi.setSystemTime(Date.now() + 31_000);
    becomeVisible('visible');
    http.expectNone(COUNT);
    vi.useRealTimers();
  });

  it('reloads the open page instead of reading the number alone, under the same 30 seconds, and goes back to the number once the page is gone', async () => {
    vi.useFakeTimers({ toFake: ['Date'] });
    vi.setSystemTime(new Date('2026-11-02T08:00:00Z'));
    const { state, http } = await setup();
    const shell = fakeDestroyRef();
    const page = fakeDestroyRef();
    const reload = vi.fn();
    state.refreshWhenVisible(shell.ref);
    state.reloadPageWhenVisible(reload, page.ref);

    // The page is open: the tab coming back reloads the page, and the badge waits for the page's answer.
    becomeVisible('visible');
    expect(reload).toHaveBeenCalledTimes(1);
    http.expectNone(COUNT);

    // The reload started the clock, so coming back straight away neither reloads nor asks.
    becomeVisible('visible');
    expect(reload).toHaveBeenCalledTimes(1);
    http.expectNone(COUNT);

    vi.setSystemTime(Date.now() + 31_000);
    becomeVisible('visible');
    expect(reload).toHaveBeenCalledTimes(2);

    // With the page gone, the badge reads its own number again.
    page.destroy();
    vi.setSystemTime(Date.now() + 31_000);
    becomeVisible('visible');
    expect(reload).toHaveBeenCalledTimes(2);
    http.expectOne(COUNT).flush({ total: 7 });
    expect(state.count()).toBe(7);
    shell.destroy();
    vi.useRealTimers();
  });

  it('shows nobody else the previous person’s number', async () => {
    const { state, http } = await setup();
    state.refresh();
    http.expectOne(COUNT).flush({ total: 3 });

    const session = TestBed.inject(SessionState);
    await signInForTest(session, http, { userId: 'user-2' });

    expect(state.count()).toBe(0);
  });
});
