import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter, withComponentInputBinding } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { Observable, of } from 'rxjs';
import { Permission } from '../../core/session/permissions';
import { SessionState } from '../../core/session/session-state';
import { buttonNamed, settle, text, texts } from '../../testing/dom';
import { signInForTest } from '../../testing/session-fixture';
import { BADGES_PER_SHEET, BadgesPage } from './badges-page';
import { TimeClockApi } from './timeclock-api';
import { Badge, BadgeSheet } from './timeclock.models';

type Call = (...args: unknown[]) => Observable<unknown>;

const badge = (index: number, code: string | null = 'ABCDEFGHJKMN'): Badge => ({
  workerId: `w${index}`,
  fullName: `Worker ${index}`,
  employeeNumber: `W-${String(index).padStart(3, '0')}`,
  jobTitle: 'Mason',
  code,
});

async function open(url: string, sheet: (ids: readonly string[]) => BadgeSheet) {
  const badges = vi.fn<Call>((ids) => of(sheet(ids as readonly string[])));
  const issueMissing = vi.fn<Call>(() =>
    of({ issued: 2, workerIds: ['w8', 'w9'], stillWithoutCode: 0 }),
  );
  const findWorkers = vi.fn<Call>(() =>
    of([
      { id: 'w5', fullName: 'Lito Abad', employeeNumber: 'W-005', status: 'Active' },
      { id: 'w6', fullName: 'Gone Away', employeeNumber: 'W-006', status: 'Resigned' },
    ]),
  );
  TestBed.configureTestingModule({
    providers: [
      provideRouter(
        [{ path: 'time-clock/badges', component: BadgesPage }],
        withComponentInputBinding(),
      ),
      provideHttpClient(),
      provideHttpClientTesting(),
      { provide: TimeClockApi, useValue: { badges, issueMissing, findWorkers } },
    ],
  });
  await signInForTest(TestBed.inject(SessionState), TestBed.inject(HttpTestingController), {
    roles: ['Custom'],
    permissions: [Permission.timeClockManage, Permission.workforceRead],
  });
  const harness = await RouterTestingHarness.create();
  await harness.navigateByUrl(url, BadgesPage);
  await settle(harness.fixture);
  return {
    harness,
    root: harness.fixture.nativeElement as HTMLElement,
    badges,
    issueMissing,
    findWorkers,
  };
}

describe('BadgesPage', () => {
  it('says what to do when no badge is chosen, and offers codes to the workers who have none', async () => {
    const { harness, root, issueMissing } = await open('/time-clock/badges', () => ({
      companyName: 'Acme',
      items: [],
      activeWithoutCode: 2,
    }));

    expect(text(root, '.empty__title')).toBe('No badges chosen yet');
    expect(text(root, '.alert__title')).toBe('2 active workers have no badge code');

    buttonNamed(root, 'Issue codes and show their badges').click();
    await settle(harness.fixture);
    expect(issueMissing).toHaveBeenCalledTimes(1);
    expect(TestBed.inject(Router).url).toBe('/time-clock/badges?workers=w8,w9');
  });

  it('shows a card per chosen worker with a barcode and a QR code, ten to a printed sheet', async () => {
    const { root, badges } = await open(
      `/time-clock/badges?workers=${Array.from({ length: 12 }, (_, index) => `w${index + 1}`).join(',')}`,
      (ids) => ({
        companyName: 'Acme Builders',
        items: ids.map((_, index) => badge(index + 1, index === 11 ? null : 'ABCDEFGHJKMN')),
        activeWithoutCode: 0,
      }),
    );

    expect(badges).toHaveBeenLastCalledWith(
      Array.from({ length: 12 }, (_, index) => `w${index + 1}`),
    );
    const sheets = root.querySelectorAll('.badges-sheet');
    expect(sheets).toHaveLength(2);
    expect(sheets[0]!.querySelectorAll('.badges-slot')).toHaveLength(BADGES_PER_SHEET);
    expect(root.querySelectorAll('app-badge-card svg.code128')).toHaveLength(11);
    expect(root.querySelectorAll('app-badge-card svg.qr')).toHaveLength(11);
    expect(text(root, '.badges-missing')).toBe('Worker 12 has no badge code yet.');
    expect(buttonNamed(root, 'Print badges').classList).toContain('btn--primary');
    expect(root.textContent).not.toMatch(/₱|rate/i);
  });

  it('finds active workers by name and adds them to the list in the address', async () => {
    const { harness, root, findWorkers } = await open('/time-clock/badges', () => ({
      companyName: 'Acme',
      items: [],
      activeWithoutCode: 0,
    }));

    const search = root.querySelector<HTMLInputElement>('input[name="search"]')!;
    search.value = 'Lito';
    search.dispatchEvent(new Event('input', { bubbles: true }));
    buttonNamed(root, 'Find workers').click();
    await settle(harness.fixture);

    expect(findWorkers).toHaveBeenCalledWith('Lito');
    expect(texts(root, '.badges-found li > span:first-child')).toEqual(['Lito Abad W-005']);
    root.querySelector<HTMLButtonElement>('button[aria-label="Add Lito Abad"]')!.click();
    await settle(harness.fixture);
    expect(TestBed.inject(Router).url).toBe('/time-clock/badges?workers=w5');
  });
});
