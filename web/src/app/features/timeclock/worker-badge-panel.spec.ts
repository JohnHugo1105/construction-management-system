import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { Observable, of } from 'rxjs';
import { Permission } from '../../core/session/permissions';
import { SessionState } from '../../core/session/session-state';
import { buttonNamed, flushMicrotasks, settle, text } from '../../testing/dom';
import { signInForTest } from '../../testing/session-fixture';
import { WorkerDetailState } from '../workforce/worker-detail-state';
import { WorkforceApi } from '../workforce/workforce-api';
import { WorkerDetail } from '../workforce/workforce.models';
import { TimeClockApi } from './timeclock-api';
import { WorkerBadgePanel } from './worker-badge-panel';
import { NEEDS_ACTION_PERMISSIONS } from '../../core/session/permissions';
import { GROUP_HEADINGS, rowLink, urgentChip } from '../needs-action/needs-action-presentation';

type Call = (...args: unknown[]) => Observable<unknown>;

const worker = (overrides: Partial<WorkerDetail> = {}): WorkerDetail =>
  ({
    id: 'w1',
    employeeNumber: 'W-001',
    firstName: 'Juan',
    middleName: null,
    lastName: 'Dela Cruz',
    fullName: 'Juan Dela Cruz',
    status: 'Active',
    version: 3,
    hasPunchCode: false,
    rates: [],
    currentRateMinor: null,
    ...overrides,
  }) as WorkerDetail;

async function open(options: { worker?: WorkerDetail; permissions?: readonly string[] } = {}) {
  const issueCode = vi.fn<Call>(() =>
    of({
      workerId: 'w1',
      code: 'ABCDEFGHJKMN',
      reissued: options.worker?.hasPunchCode === true,
      version: 4,
    }),
  );
  const get = vi.fn<Call>(() =>
    of({ ...(options.worker ?? worker()), hasPunchCode: true, version: 4 }),
  );
  TestBed.configureTestingModule({
    providers: [
      provideRouter([]),
      provideHttpClient(),
      provideHttpClientTesting(),
      WorkerDetailState,
      { provide: TimeClockApi, useValue: { issueCode } },
      { provide: WorkforceApi, useValue: { get } },
    ],
  });
  await signInForTest(TestBed.inject(SessionState), TestBed.inject(HttpTestingController), {
    roles: ['Custom'],
    permissions: options.permissions ?? [Permission.timeClockManage, Permission.workforceRead],
  });
  const state = TestBed.inject(WorkerDetailState);
  state.replace(options.worker ?? worker());
  const fixture = TestBed.createComponent(WorkerBadgePanel);
  await settle(fixture);
  return { fixture, root: fixture.nativeElement as HTMLElement, issueCode };
}

describe('WorkerBadgePanel', () => {
  it('issues a first code at once and shows it with a way to print the badge', async () => {
    const { fixture, root, issueCode } = await open();

    expect(text(root, '.card__title')).toBe('Time clock badge');
    buttonNamed(root, 'Issue badge code').click();
    await settle(fixture);
    await flushMicrotasks();
    await settle(fixture);

    expect(issueCode).toHaveBeenCalledWith('w1', 3);
    expect(text(root, '.badge-code')).toBe('ABCD-EFGH-JKMN');
    expect(buttonNamed(root, 'Print the badge').getAttribute('href')).toBe(
      '/time-clock/badges?workers=w1',
    );
  });

  it('asks before reissuing, saying the old badge stops working at once', async () => {
    const { fixture, root, issueCode } = await open({ worker: worker({ hasPunchCode: true }) });

    buttonNamed(root, 'Reissue badge code').click();
    await settle(fixture);
    expect(document.body.textContent).toContain(
      "Juan Dela Cruz's current badge stops working at once.",
    );
    buttonNamed(document.body, 'Keep the current badge').click();
    await settle(fixture);
    expect(issueCode).not.toHaveBeenCalled();
  });

  it('is absent for people who do not issue badges and for a worker who left', async () => {
    expect(
      (await open({ permissions: [Permission.workforceRead] })).root.querySelector('section'),
    ).toBeNull();
    TestBed.resetTestingModule();
    expect(
      (await open({ worker: worker({ status: 'Resigned' }) })).root.querySelector('section'),
    ).toBeNull();
  });
});

describe('Time clock on the Needs your action page', () => {
  it('has a heading, opens the project on the review page, and words an urgent row as a check, not as late', () => {
    expect(GROUP_HEADINGS['time-clock-to-review']).toBe('Time clock scans to review');
    expect(
      rowLink(
        'time-clock-to-review',
        {
          id: 'p1',
          parentId: null,
          label: 'Riverside',
          projectId: 'p1',
          projectName: 'Riverside',
          detail: '',
          sinceUtc: null,
          dueDate: null,
          isUrgent: true,
        },
        { can: () => true },
      ),
    ).toEqual(['/time-clock', 'p1']);
    expect(urgentChip('time-clock-to-review')).toEqual({
      label: 'Check scans',
      tone: 'warning',
      icon: 'warning',
    });
    expect(NEEDS_ACTION_PERMISSIONS).toContain(Permission.timeClockReview);
  });
});
