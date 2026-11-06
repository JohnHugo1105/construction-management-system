import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { Observable, of } from 'rxjs';
import { Permission } from '../../core/session/permissions';
import { SessionState } from '../../core/session/session-state';
import { buttonNamed, settle, text } from '../../testing/dom';
import { signInForTest } from '../../testing/session-fixture';
import { TeamTimeClockNotice } from './team-time-clock-notice';
import { TimeClockApi } from './timeclock-api';
import { TimeClockSummary } from './timeclock.models';

async function open(
  summary: TimeClockSummary,
  permissions: readonly string[] = [Permission.timeClockReview],
) {
  const call = vi.fn<(projectId: string) => Observable<TimeClockSummary>>(() => of(summary));
  TestBed.configureTestingModule({
    providers: [
      provideRouter([]),
      provideHttpClient(),
      provideHttpClientTesting(),
      { provide: TimeClockApi, useValue: { summary: call } },
    ],
  });
  await signInForTest(TestBed.inject(SessionState), TestBed.inject(HttpTestingController), {
    roles: ['Custom'],
    permissions,
  });
  const fixture = TestBed.createComponent(TeamTimeClockNotice);
  fixture.componentRef.setInput('projectId', 'p1');
  await settle(fixture);
  return { root: fixture.nativeElement as HTMLElement, call };
}

describe('TeamTimeClockNotice', () => {
  it('says how many days of scans wait and leads to them', async () => {
    const { root, call } = await open({
      projectId: 'p1',
      daysWaiting: 2,
      rowsWaiting: 9,
      rowsWithExceptions: 1,
      earliestDate: '2026-10-31',
    });

    expect(call).toHaveBeenCalledWith('p1');
    expect(text(root, '.alert__title')).toBe(
      'Time clock scans wait for review on 2 days, from 31 Oct 2026',
    );
    expect(buttonNamed(root, 'Review time clock scans').getAttribute('href')).toBe(
      '/time-clock/p1',
    );
  });

  it('shows nothing when nothing waits, and asks nothing for someone who does not confirm scans', async () => {
    expect(
      (
        await open({
          projectId: 'p1',
          daysWaiting: 0,
          rowsWaiting: 0,
          rowsWithExceptions: 0,
          earliestDate: null,
        })
      ).root.textContent!.trim(),
    ).toBe('');
    TestBed.resetTestingModule();
    const { root, call } = await open(
      {
        projectId: 'p1',
        daysWaiting: 1,
        rowsWaiting: 1,
        rowsWithExceptions: 0,
        earliestDate: '2026-11-01',
      },
      [Permission.attendanceWrite],
    );
    expect(call).not.toHaveBeenCalled();
    expect(root.querySelector('app-alert')).toBeNull();
  });
});
