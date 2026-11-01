import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { Observable, of } from 'rxjs';
import type { Mock } from 'vitest';
import { SessionState } from '../../core/session/session-state';
import { Viewport } from '../../shared/ui/viewport/viewport';
import { buttonNamed, settle, text, texts } from '../../testing/dom';
import { VIEWER_PERMISSIONS, pageOf, signInForTest } from '../../testing/session-fixture';
import {
  HR_PERMISSIONS,
  MANAGER_PERMISSIONS,
  entry,
  rate,
  riverside,
  workerWithPay,
  workerWithoutPay,
} from './workforce.fixtures';
import { WorkerDetailPage } from './worker-detail-page';
import { WorkforceApi } from './workforce-api';
import { WorkerDetail } from './workforce.models';

type Call = (...args: unknown[]) => Observable<unknown>;
const stub = (implementation: Call): Mock<Call> => vi.fn<Call>(implementation);

async function open(
  worker: WorkerDetail,
  permissions: readonly string[],
  api: Record<string, Mock<Call>> = {},
) {
  TestBed.configureTestingModule({
    providers: [
      provideRouter(
        [{ path: 'workers/:id', component: WorkerDetailPage }],
        withComponentInputBinding(),
      ),
      provideHttpClient(),
      provideHttpClientTesting(),
      {
        provide: WorkforceApi,
        useValue: {
          get: stub(() => of(worker)),
          recentAttendance: stub(() =>
            of(pageOf([entry({ regularHours: 8, overtimeHours: 1, isLocked: true })])),
          ),
          projectChoices: () => of([riverside]),
          ...api,
        },
      },
      { provide: Viewport, useValue: { isTablet: signal(true), isDesktop: signal(true) } },
    ],
  });
  await signInForTest(TestBed.inject(SessionState), TestBed.inject(HttpTestingController), {
    roles: ['Custom'],
    permissions,
  });
  const harness = await RouterTestingHarness.create();
  await harness.navigateByUrl(`/workers/${worker.id}`, WorkerDetailPage);
  await settle(harness.fixture);
  return { harness, root: harness.fixture.nativeElement as HTMLElement };
}

describe('WorkerDetailPage and pay', () => {
  it('shows the rate, and a way to change it, to a person who may see and change pay', async () => {
    const { root } = await open(workerWithPay(), HR_PERMISSIONS);

    expect(text(root, '#pay-heading')).toBe('Pay');
    expect(text(root, '.pay__rate')).toBe('₱800.00 per day');
    expect(buttonNamed(root, 'Change pay rate')).toBeTruthy();
  });

  it('shows pay and no way to change it to a person who may see pay but not manage workers', async () => {
    const accountant = [...VIEWER_PERMISSIONS, 'payroll.read'];
    const { root } = await open(workerWithPay(), accountant);

    expect(text(root, '.pay__rate')).toBe('₱800.00 per day');
    expect(texts(root, 'button, a').filter((label) => label === 'Change pay rate')).toEqual([]);
  });

  it('shows no pay anywhere to a person who may not see it, only that it is limited to payroll staff', async () => {
    const { root } = await open(workerWithoutPay(), MANAGER_PERMISSIONS);

    expect(
      texts(
        root,
        'section[aria-labelledby="pay-heading"] h2, section[aria-labelledby="pay-heading"] p',
      ),
    ).toEqual(['Pay', 'Pay details are limited to payroll staff.']);
    expect(root.textContent).not.toMatch(/₱|per day|per hour|Change pay rate|Pay history/);
    // They still see who the worker is, where they are posted and how they have been turning up.
    expect(text(root, 'section[aria-labelledby="posting-heading"]')).toContain(
      'Riverside Residences',
    );
    expect(text(root, 'section[aria-labelledby="attendance-heading"]')).toContain(
      '8 h + 1 h overtime',
    );
  });

  it('never offers to change pay when the API sent none, even to a person who manages workers', async () => {
    // A person who manages workers but whose response carries no pay (for example the permission changed): the page follows the data.
    const { root } = await open(workerWithoutPay(), HR_PERMISSIONS);

    expect(root.textContent).not.toContain('Change pay rate');
    expect(text(root, 'section[aria-labelledby="pay-heading"] p')).toBe(
      'Pay details are limited to payroll staff.',
    );
  });

  it('shows pay history only once there is more than the rate the worker was hired at', async () => {
    const single = await open(workerWithPay(), HR_PERMISSIONS);
    expect(single.root.querySelector('.pay__history')).toBeNull();
    TestBed.resetTestingModule();

    const raised = workerWithPay({
      rates: [
        rate(),
        rate({
          id: 'r2',
          designation: 'Foreman',
          rateMinor: 95000,
          category: 'Promotion',
          effectiveFrom: '2026-10-01',
          reason: 'Led the structure crew',
        }),
      ],
      currentRateMinor: 95000,
      designation: 'Foreman',
    });
    const { root } = await open(raised, HR_PERMISSIONS);

    expect(text(root, '.pay__history summary')).toBe('Pay history (2)');
    const rows = root.querySelectorAll('.pay__history tbody tr');
    expect(texts(rows[0]!, 'th, td')).toEqual([
      '1 Oct 2026',
      'Foreman',
      '₱950.00 per day',
      'Promotion',
      'Led the structure crew',
    ]);
    // "Hired" says the same as the change beside it, so no reason is repeated for the first line.
    expect(texts(rows[1]!, 'th, td')).toEqual([
      '1 Mar 2026',
      'Mason',
      '₱800.00 per day',
      'Hired',
      '',
    ]);
  });

  it('says when a recorded change has not started yet', async () => {
    const future = workerWithPay({
      rates: [
        rate(),
        rate({
          id: 'r2',
          designation: 'Foreman',
          rateMinor: 95000,
          category: 'Promotion',
          effectiveFrom: '2099-01-01',
        }),
      ],
    });
    const { root } = await open(future, HR_PERMISSIONS);

    expect(text(root, '.pay__rate')).toBe('₱800.00 per day');
    expect(text(root, '.pay__next')).toBe('From 1 Jan 2099: Foreman, ₱950.00 per day');
  });
});

describe('WorkerDetailPage actions', () => {
  it('offers posting, releasing, changing status, cases and editing to a person who manages workers', async () => {
    const { root } = await open(workerWithPay(), HR_PERMISSIONS);

    const labels = texts(root, 'main button, main a, button, a');
    for (const label of [
      'Move to another project',
      'Release from the project',
      'Change status',
      'Add a case',
      'Edit profile',
    ])
      expect(labels).toContain(label);
  });

  it('offers none of them to a person who can only look', async () => {
    const { root } = await open(workerWithoutPay(), VIEWER_PERMISSIONS);

    const labels = texts(root, 'button, a');
    for (const label of [
      'Move to another project',
      'Release from the project',
      'Change status',
      'Add a case',
      'Edit profile',
    ])
      expect(labels).not.toContain(label);
  });

  it('offers to assign a worker who is not posted anywhere, as the one next step', async () => {
    const free = workerWithPay({ currentAssignment: null, assignments: [] });
    const { root } = await open(free, HR_PERMISSIONS);

    expect(text(root, 'section[aria-labelledby="posting-heading"]')).toContain(
      'Not posted to a project.',
    );
    const assign = buttonNamed(root, 'Assign to a project');
    expect(assign.classList.contains('btn--primary')).toBe(true);
    expect(texts(root, 'button')).not.toContain('Release from the project');
  });

  it('explains why a worker on leave cannot be posted, instead of offering it', async () => {
    const onLeave = workerWithPay({
      status: 'OnLeave',
      statusChangedOn: '2026-10-20',
      currentAssignment: null,
      assignments: [],
    });
    const { root } = await open(onLeave, HR_PERMISSIONS);

    expect(text(root, 'section[aria-labelledby="posting-heading"]')).toContain(
      'Only an active worker can be posted to a project.',
    );
    expect(texts(root, 'button')).not.toContain('Assign to a project');
  });

  it('offers one next step for a worker who left: reinstate them, with no way to change status or pay', async () => {
    const gone = workerWithPay({
      status: 'Resigned',
      statusChangedOn: '2026-10-20',
      statusReason: 'Moved to another province',
      currentAssignment: null,
    });
    const { root } = await open(gone, HR_PERMISSIONS);

    const labels = texts(root, 'button');
    expect(labels).toContain('Reinstate worker');
    expect(labels).not.toContain('Change status');
    expect(labels).not.toContain('Change pay rate');
    expect(text(root, 'app-alert')).toContain('Since 20 Oct 2026.');
    expect(text(root, 'app-alert')).toContain('Reason: Moved to another province');
  });

  it('keeps cases append-only: they can be added but not edited or removed', async () => {
    const withCase = workerWithPay({
      cases: [
        {
          id: 'c1',
          caseDate: '2026-10-10',
          title: 'Late three days in a row',
          remarks: null,
          projectId: null,
          projectName: null,
          recordedAtUtc: '2026-10-10T00:00:00Z',
        },
      ],
    });
    const { root } = await open(withCase, HR_PERMISSIONS);

    const panel = root.querySelector('section[aria-labelledby="cases-heading"]')!;
    expect(texts(panel, '.cases__title')).toEqual(['Late three days in a row']);
    expect(texts(panel, 'button')).toEqual(['Add a case']);
  });
});
