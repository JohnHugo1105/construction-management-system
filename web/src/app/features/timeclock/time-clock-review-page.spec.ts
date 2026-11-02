import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { Observable, of } from 'rxjs';
import type { Mock } from 'vitest';
import { Permission } from '../../core/session/permissions';
import { SessionState } from '../../core/session/session-state';
import { buttonNamed, flushMicrotasks, settle, text, texts } from '../../testing/dom';
import { signInForTest } from '../../testing/session-fixture';
import { TimeClockApi } from './timeclock-api';
import { TimeClockReviewPage } from './time-clock-review-page';
import { ConfirmResult, TimeClockDayList, TimeClockDayRow } from './timeclock.models';

type Call = (...args: unknown[]) => Observable<unknown>;

const row = (overrides: Partial<TimeClockDayRow> = {}): TimeClockDayRow => ({
  workerId: 'w1',
  workerName: 'Juan Dela Cruz',
  firstIn: '2026-10-31T23:02:00Z',
  firstInLocal: '07:02',
  lastOut: '2026-11-01T09:03:00Z',
  lastOutLocal: '17:03',
  breakMinutes: 60,
  regularHours: 8,
  proposedOvertimeHours: 0,
  nightHours: 0,
  exceptions: [],
  alreadyRecorded: false,
  canConfirm: true,
  isClean: true,
  punches: [
    {
      id: 's1',
      punchedAtUtc: '2026-10-31T23:02:00Z',
      localTime: '07:02',
      direction: 'In',
      source: 'Kiosk',
      deviceName: 'Main gate',
      notPostedHere: false,
      reason: null,
      version: 1,
    },
    {
      id: 's2',
      punchedAtUtc: '2026-11-01T09:03:00Z',
      localTime: '17:03',
      direction: 'Out',
      source: 'Manual',
      deviceName: null,
      notPostedHere: false,
      reason: 'Badge left at home',
      version: 1,
    },
  ],
  ...overrides,
});

const day = (
  items: readonly TimeClockDayRow[],
  overrides: Partial<TimeClockDayList> = {},
): TimeClockDayList => ({
  projectId: 'p1',
  projectName: 'Riverside Residences',
  projectArchived: false,
  workDate: '2026-11-01',
  items,
  total: items.length,
  page: 1,
  pageSize: 25,
  summary: {
    projectId: 'p1',
    daysWaiting: 1,
    rowsWaiting: items.length,
    rowsWithExceptions: 0,
    earliestDate: '2026-11-01',
  },
  ...overrides,
});

const crew = [
  row(),
  row({
    workerId: 'w2',
    workerName: 'Ana Bautista',
    proposedOvertimeHours: 2,
    regularHours: 8,
    isClean: false,
  }),
  row({
    workerId: 'w3',
    workerName: 'Pedro Reyes',
    lastOut: null,
    lastOutLocal: null,
    breakMinutes: 0,
    regularHours: 0,
    exceptions: ['MissingTimeOut'],
    canConfirm: false,
    isClean: false,
  }),
  row({
    workerId: 'w4',
    workerName: 'Maria Santos',
    alreadyRecorded: true,
    canConfirm: false,
    isClean: false,
  }),
];

async function open(
  options: {
    url?: string;
    list?: TimeClockDayList;
    confirm?: Mock<Call>;
    permissions?: readonly string[];
  } = {},
) {
  const days = vi.fn<Call>(() => of(options.list ?? day(crew)));
  const confirm =
    options.confirm ?? vi.fn<Call>(() => of<ConfirmResult>({ confirmed: ['w1'], skipped: [] }));
  TestBed.configureTestingModule({
    providers: [
      provideRouter(
        [
          { path: 'time-clock', component: TimeClockReviewPage },
          { path: 'time-clock/:projectId', component: TimeClockReviewPage },
        ],
        withComponentInputBinding(),
      ),
      provideHttpClient(),
      provideHttpClientTesting(),
      {
        provide: TimeClockApi,
        useValue: {
          days,
          confirm,
          projects: () => of([{ id: 'p1', name: 'Riverside Residences', status: 'Active' }]),
        },
      },
    ],
  });
  await signInForTest(TestBed.inject(SessionState), TestBed.inject(HttpTestingController), {
    roles: ['Custom'],
    permissions: options.permissions ?? [Permission.timeClockReview, Permission.projectsRead],
  });
  const harness = await RouterTestingHarness.create();
  await harness.navigateByUrl(options.url ?? '/time-clock/p1', TimeClockReviewPage);
  await settle(harness.fixture);
  return { harness, root: harness.fixture.nativeElement as HTMLElement, days, confirm };
}

const card = (root: HTMLElement, name: string) =>
  Array.from(root.querySelectorAll<HTMLElement>('.review-row')).find(
    (item) => text(item, '.review-row__name') === name,
  )!;

describe('TimeClockReviewPage', () => {
  it('asks for a project first', async () => {
    const { root, days } = await open({ url: '/time-clock' });

    expect(text(root, '.empty__title')).toBe('Choose a project');
    expect(days).not.toHaveBeenCalled();
  });

  it('opens the earliest day waiting and shows each worker with the scans, hours and exceptions in words', async () => {
    const { root, days } = await open();

    expect(days).toHaveBeenCalledWith('p1', null, 1);
    const juan = card(root, 'Juan Dela Cruz');
    expect(juan.textContent).toContain('In 7:02 am, out 5:03 pm, 60 min break');
    expect(text(juan, '.review-row__hours')).toBe('8 h regular');
    expect(texts(juan, '.review-scans li span')[0]).toContain('Time in 7:02 am');
    expect(juan.textContent).toContain('Added by a supervisor: Badge left at home');

    const pedro = card(root, 'Pedro Reyes');
    expect(texts(pedro, '.chip')).toEqual(['No time out']);
    expect(pedro.textContent).toContain('Add the missing time out, or remove an extra time in.');
    expect(() => buttonNamed(pedro, 'Confirm Pedro Reyes')).toThrow();

    const maria = card(root, 'Maria Santos');
    expect(texts(maria, '.chip')).toEqual(['Already recorded']);
    expect(maria.classList).toContain('review-row--done');
    expect(maria.querySelector('button')).toBeNull();
    // Hours only: no amount anywhere.
    expect(root.textContent).not.toContain('₱');
  });

  it('confirms one row, with its overtime only when the box is ticked', async () => {
    const { harness, root, confirm } = await open();
    const ana = card(root, 'Ana Bautista');
    expect(ana.textContent).toContain('Approve 2 h overtime');

    buttonNamed(ana, 'Confirm Ana Bautista').click();
    await settle(harness.fixture);
    expect(confirm).toHaveBeenLastCalledWith({
      projectId: 'p1',
      workDate: '2026-11-01',
      rows: [{ workerId: 'w2', approveOvertime: false }],
      allClean: false,
    });

    ana.querySelector<HTMLInputElement>('.review-overtime input')!.click();
    buttonNamed(card(root, 'Ana Bautista'), 'Confirm Ana Bautista').click();
    await settle(harness.fixture);
    expect(confirm).toHaveBeenLastCalledWith({
      projectId: 'p1',
      workDate: '2026-11-01',
      rows: [{ workerId: 'w2', approveOvertime: true }],
      allClean: false,
    });
  });

  it('counts the clean rows for the one primary action, counting a row once its overtime is approved', async () => {
    const { harness, root, confirm } = await open();

    expect(text(root, '.review-confirm span')).toBe('1 worker ready to confirm');
    card(root, 'Ana Bautista').querySelector<HTMLInputElement>('.review-overtime input')!.click();
    await settle(harness.fixture);
    expect(text(root, '.review-confirm span')).toBe('2 workers ready to confirm');
    expect(root.querySelectorAll('.btn--primary')).toHaveLength(1);

    buttonNamed(root, 'Confirm all clean rows').click();
    await settle(harness.fixture);
    expect(confirm).toHaveBeenLastCalledWith({
      projectId: 'p1',
      workDate: '2026-11-01',
      rows: [{ workerId: 'w2', approveOvertime: true }],
      allClean: true,
    });
  });

  it('says which workers were not confirmed and why', async () => {
    const confirm = vi.fn<Call>(() =>
      of<ConfirmResult>({
        confirmed: [],
        skipped: [
          {
            workerId: 'w1',
            workerName: 'Juan Dela Cruz',
            reason: 'Juan Dela Cruz already has attendance on Riverside Residences for 1 Nov 2026.',
          },
        ],
      }),
    );
    const { harness, root } = await open({ confirm });

    buttonNamed(card(root, 'Juan Dela Cruz'), 'Confirm Juan Dela Cruz').click();
    await settle(harness.fixture);
    await flushMicrotasks();
    await settle(harness.fixture);

    expect(text(root, '.alert__title')).toBe("Some workers weren't confirmed");
    expect(text(root, '.review-skip')).toBe(
      'Juan Dela Cruz: Juan Dela Cruz already has attendance on Riverside Residences for 1 Nov 2026.',
    );
  });

  it('leads to the earliest day still waiting when another day is shown, and offers nothing to confirm on an archived project', async () => {
    const list = day([row()], {
      workDate: '2026-11-02',
      projectArchived: true,
      summary: {
        projectId: 'p1',
        daysWaiting: 3,
        rowsWaiting: 9,
        rowsWithExceptions: 1,
        earliestDate: '2026-10-30',
      },
    });
    const { root } = await open({ list, url: '/time-clock/p1?date=2026-11-02' });

    expect(texts(root, '.alert__title')).toEqual([
      'This project is archived',
      'Scans wait for review on 3 days, from 30 Oct 2026',
    ]);
    expect(buttonNamed(root, 'Go to 30 Oct 2026')).toBeTruthy();
    expect(root.querySelector('.review-confirm')).toBeNull();
    expect(root.querySelector('.review-row__actions')).toBeNull();
  });

  it('shows the badges and import only to people who issue badges', async () => {
    const { root } = await open();
    expect(() => buttonNamed(root, 'Badges')).toThrow();
    expect(() => buttonNamed(root, "Import a machine's file")).toThrow();
  });
});
