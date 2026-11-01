import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { Observable, of, throwError } from 'rxjs';
import type { Mock } from 'vitest';
import { SessionState } from '../../../core/session/session-state';
import { addDays, todayIn } from '../../../shared/presentation/business-time';
import { Viewport } from '../../../shared/ui/viewport/viewport';
import { buttonNamed, settle, text, texts } from '../../../testing/dom';
import { VIEWER_PERMISSIONS, signInForTest } from '../../../testing/session-fixture';
import { ProjectContext } from '../../projects/project-context';
import { Project } from '../../projects/projects.models';
import { HR_PERMISSIONS, entry, sheet, validationError } from '../workforce.fixtures';
import { WorkforceApi } from '../workforce-api';
import { AttendanceSheet } from '../workforce.models';
import { ProjectTeamTab } from './project-team-tab';

type Call = (...args: unknown[]) => Observable<unknown>;
const stub = (implementation: Call): Mock<Call> => vi.fn<Call>(implementation);

const today = todayIn('Asia/Manila');

const project: Project = {
  id: 'p1',
  name: 'Riverside Residences',
  client: null,
  location: null,
  contractPriceMinor: null,
  startDate: '2026-03-01',
  targetEndDate: null,
  status: 'Active',
  archiveReason: null,
  archiveNote: null,
  archivedAtUtc: null,
  subProjects: [],
  version: 1,
};

const threeCrew: AttendanceSheet = sheet({
  workDate: today,
  crewWithoutEntry: [
    { id: 'w1', fullName: 'Ana Bautista', employeeNumber: 'W-005', status: 'Active' },
    { id: 'w2', fullName: 'Juan Dela Cruz', employeeNumber: 'W-001', status: 'Active' },
    { id: 'w3', fullName: 'Jose Garcia', employeeNumber: 'W-004', status: 'Active' },
  ],
});

interface Options {
  readonly permissions?: readonly string[];
  readonly project?: Project;
  readonly sheet?: AttendanceSheet;
  readonly api?: Record<string, Mock<Call>>;
  readonly hoursPerDay?: number;
}

async function open(options: Options = {}) {
  const loadSheet = stub((projectId, date) => {
    void projectId;
    return of({ ...(options.sheet ?? threeCrew), workDate: date as string });
  });
  const api: Record<string, Mock<Call> | (() => Observable<unknown>)> = {
    sheet: loadSheet,
    hoursPerDay: () => of(options.hoursPerDay ?? 8),
    recordDay: stub(() => of([])),
    reviseAttendance: stub(() => of(entry())),
    voidAttendance: stub(() => of(entry())),
    ...options.api,
  };
  TestBed.configureTestingModule({
    providers: [
      provideRouter([]),
      provideHttpClient(),
      provideHttpClientTesting(),
      ProjectContext,
      { provide: WorkforceApi, useValue: api },
      { provide: Viewport, useValue: { isTablet: signal(true), isDesktop: signal(true) } },
    ],
  });
  await signInForTest(TestBed.inject(SessionState), TestBed.inject(HttpTestingController), {
    roles: ['Custom'],
    permissions: options.permissions ?? HR_PERMISSIONS,
  });
  TestBed.inject(ProjectContext).replace(options.project ?? project);
  const fixture = TestBed.createComponent(ProjectTeamTab);
  await settle(fixture);
  return {
    fixture,
    root: fixture.nativeElement as HTMLElement,
    api: api as Record<string, Mock<Call>>,
    loadSheet,
  };
}

const lines = (root: HTMLElement) =>
  Array.from(root.querySelectorAll<HTMLElement>('li.line--draft'));
const hours = (line: HTMLElement, which: 'Regular' | 'Overtime') =>
  line.querySelector<HTMLInputElement>(`app-hours-field input[aria-label^="${which} hours"]`)!;
const setHours = (line: HTMLElement, which: 'Regular' | 'Overtime', value: string) => {
  const input = hours(line, which);
  input.value = value;
  input.dispatchEvent(new Event('input', { bubbles: true }));
};
const tickBox = (line: HTMLElement) =>
  line.querySelector<HTMLInputElement>('input[type="checkbox"]')!;
const errorsIn = (line: HTMLElement) => texts(line, '.field__error');
const chooseDay = (root: HTMLElement, date: string) => {
  const input = root.querySelector<HTMLInputElement>('input[type="date"]')!;
  input.value = date;
  input.dispatchEvent(new Event('change', { bubbles: true }));
};

describe('ProjectTeamTab daily sheet', () => {
  it('starts on today in the company time zone, with every crew member as a full company day', async () => {
    const { root, loadSheet } = await open({ hoursPerDay: 10 });

    expect(loadSheet).toHaveBeenCalledWith('p1', today);
    expect(root.querySelector<HTMLInputElement>('input[type="date"]')!.value).toBe(today);
    expect(texts(root, '.line--draft .worked__name')).toEqual([
      'Ana Bautista',
      'Juan Dela Cruz',
      'Jose Garcia',
    ]);
    for (const line of lines(root)) {
      expect(tickBox(line).checked).toBe(true);
      // The company's day, not a fixed eight hours.
      expect(hours(line, 'Regular').value).toBe('10');
      expect(hours(line, 'Overtime').value).toBe('0');
      expect(text(line, '.line__badge')).toBe('Not saved yet');
    }
    expect(text(root, '.save-bar__summary')).toBe('3 workers will be recorded.');
  });

  it('refuses a day that has not happened yet: it is not asked of the server and recording is switched off', async () => {
    const { root, fixture, loadSheet } = await open();
    expect(root.querySelector<HTMLInputElement>('input[type="date"]')!.max).toBe(today);
    loadSheet.mockClear();

    chooseDay(root, addDays(today, 1)!);
    await settle(fixture);

    expect(loadSheet).not.toHaveBeenCalled();
    expect(text(root, '.alert__title')).toBe("That day hasn't happened yet");
    expect(root.querySelector('li.line')).toBeNull();
    expect(root.querySelector('.save-bar')).toBeNull();

    // One tap brings the person back to today, and the sheet is asked for again.
    buttonNamed(root, 'Go to today').click();
    await settle(fixture);
    expect(loadSheet).toHaveBeenCalledWith('p1', today);
    expect(lines(root)).toHaveLength(3);
  });

  it('asks for an earlier day when the person chooses one', async () => {
    const { root, fixture, loadSheet } = await open();
    const earlier = addDays(today, -3)!;

    chooseDay(root, earlier);
    await settle(fixture);

    expect(loadSheet).toHaveBeenLastCalledWith('p1', earlier);
    expect(buttonNamed(root, 'Today')).toBeTruthy();
  });

  it('records the ticked workers in one request, then asks for the sheet again', async () => {
    const { root, fixture, api, loadSheet } = await open();
    const [first, second] = lines(root) as [HTMLElement, HTMLElement];
    tickBox(first).click();
    setHours(second, 'Overtime', '2.5');
    await settle(fixture);
    expect(text(root, '.save-bar__summary')).toBe(
      '2 workers will be recorded. 1 marked as not working.',
    );

    buttonNamed(root, 'Save the day').click();
    await settle(fixture);

    expect(api['recordDay']).toHaveBeenCalledTimes(1);
    const sent = api['recordDay']!.mock.calls[0]![0] as Record<string, unknown>;
    expect(sent).toMatchObject({
      projectId: 'p1',
      subProjectId: null,
      workDate: today,
      entries: [
        { workerId: 'w2', regularHours: 8, overtimeHours: 2.5, remarks: null },
        { workerId: 'w3', regularHours: 8, overtimeHours: 0, remarks: null },
      ],
    });
    expect(loadSheet).toHaveBeenCalledTimes(2);
  });

  it('keeps the request id when a save fails and makes a new one after a save succeeds', async () => {
    const recordDay = stub(() => of([]));
    recordDay.mockReturnValueOnce(
      throwError(() => validationError({ 'entries[0].regularHours': ['Too many.'] })),
    );
    const { root, fixture } = await open({ api: { recordDay } });
    const idOf = (call: number) =>
      (recordDay.mock.calls[call]![0] as { clientRequestId: string }).clientRequestId;

    buttonNamed(root, 'Save the day').click();
    await settle(fixture);
    buttonNamed(root, 'Save the day').click();
    await settle(fixture);
    // The first save failed, so nothing was recorded: the retry carries the same id and cannot record the day twice.
    expect(recordDay).toHaveBeenCalledTimes(2);
    expect(idOf(1)).toBe(idOf(0));

    // That one succeeded. The next save of this sheet carries a fresh id.
    buttonNamed(root, 'Save the day').click();
    await settle(fixture);
    expect(recordDay).toHaveBeenCalledTimes(3);
    expect(idOf(2)).not.toBe(idOf(1));
    expect(idOf(2).length).toBeLessThanOrEqual(40);
  });

  it('shows entries[i].field errors on the right worker, counting only the workers that were sent', async () => {
    const recordDay = stub(() =>
      throwError(() =>
        validationError({
          // Ana is ticked off, so position 0 is Juan and position 1 is Jose.
          'entries[0].overtimeHours': ['A day has 24 hours. Check the hours entered.'],
          'entries[1].regularHours': [
            'Regular hours are at most 8 a day. Enter the rest as overtime.',
          ],
          'entries[1].workerId': ['Jose Garcia already has attendance on Riverside Residences.'],
        }),
      ),
    );
    const { root, fixture } = await open({ api: { recordDay } });
    const [ana, juan, jose] = lines(root) as [HTMLElement, HTMLElement, HTMLElement];
    tickBox(ana).click();
    await settle(fixture);

    buttonNamed(root, 'Save the day').click();
    await settle(fixture);

    expect(errorsIn(juan)).toEqual(['A day has 24 hours. Check the hours entered.']);
    expect(errorsIn(jose)).toEqual([
      'Regular hours are at most 8 a day. Enter the rest as overtime.',
      'Jose Garcia already has attendance on Riverside Residences.',
    ]);
    expect(errorsIn(ana)).toEqual([]);
    // The keyboard (and the screen) go to the first box with a problem.
    expect(document.activeElement).toBe(hours(juan, 'Overtime'));
    expect(hours(juan, 'Overtime').getAttribute('aria-invalid')).toBe('true');
    expect(hours(jose, 'Regular').getAttribute('aria-invalid')).toBe('true');
    // Nothing the person entered was lost, and a message goes away when its line is touched.
    expect(hours(jose, 'Regular').value).toBe('8');
    setHours(jose, 'Regular', '6');
    await settle(fixture);
    expect(errorsIn(jose)).toEqual([]);
  });

  it('shows a message about the whole day above the lines', async () => {
    const recordDay = stub(() =>
      throwError(() => validationError({ workDate: ["The work date can't be in the future."] })),
    );
    const { root, fixture } = await open({ api: { recordDay } });

    buttonNamed(root, 'Save the day').click();
    await settle(fixture);

    expect(text(root, 'app-alert')).toContain("The work date can't be in the future.");
  });

  it('does not send a blank box: it says which one and what to do', async () => {
    const { root, fixture, api } = await open();
    const [first] = lines(root) as [HTMLElement];

    setHours(first, 'Regular', '');
    buttonNamed(root, 'Save the day').click();
    await settle(fixture);

    expect(api['recordDay']).not.toHaveBeenCalled();
    expect(errorsIn(first)).toEqual(['Enter the regular hours, or 0 if there are none.']);
  });

  it('stops regular hours above the company day', async () => {
    const { root, fixture, api } = await open({ hoursPerDay: 8 });
    const [first] = lines(root) as [HTMLElement];

    setHours(first, 'Regular', '9');
    buttonNamed(root, 'Save the day').click();
    await settle(fixture);

    expect(api['recordDay']).not.toHaveBeenCalled();
    expect(errorsIn(first)[0]).toContain('at most 8 a day');
  });

  it('asks before leaving a day with unsaved hours, and keeps them if the person says so', async () => {
    const { root, fixture, loadSheet } = await open();
    setHours(lines(root)[0]!, 'Regular', '6');
    loadSheet.mockClear();

    chooseDay(root, addDays(today, -2)!);
    await settle(fixture);
    expect(text(root, 'app-confirm-dialog .dialog__title')).toBe('Leave this day without saving?');
    expect(loadSheet).not.toHaveBeenCalled();

    buttonNamed(root, 'Keep editing').click();
    await settle(fixture);
    expect(root.querySelector<HTMLInputElement>('input[type="date"]')!.value).toBe(today);
    expect(hours(lines(root)[0]!, 'Regular').value).toBe('6');

    chooseDay(root, addDays(today, -2)!);
    await settle(fixture);
    buttonNamed(root, 'Discard and open that day').click();
    await settle(fixture);
    expect(loadSheet).toHaveBeenCalledWith('p1', addDays(today, -2));
  });
});

describe('ProjectTeamTab recorded entries', () => {
  const recorded = sheet({
    workDate: today,
    crewWithoutEntry: [],
    entries: [
      entry({
        id: 'e1',
        workerId: 'w1',
        workerName: 'Ana Bautista',
        employeeNumber: 'W-005',
        overtimeHours: 1.5,
        version: 3,
      }),
      entry({
        id: 'e2',
        workerId: 'w2',
        workerName: 'Juan Dela Cruz',
        isLocked: true,
        payrollRunId: 'run-1',
      }),
    ],
    totalRegularHours: 16,
    totalOvertimeHours: 1.5,
  });

  it('shows a locked entry as being in a payroll run, with no way to change or remove it', async () => {
    const { root } = await open({ sheet: recorded });

    const rows = Array.from(root.querySelectorAll<HTMLElement>('li.line--saved'));
    expect(rows).toHaveLength(2);
    // Recorded hours need no badge of their own: only a lock is worth saying.
    expect(rows[0]!.querySelector('app-status-chip')).toBeNull();
    expect(texts(rows[0]!, 'button')).toEqual(['Change hours', 'Remove']);
    expect(text(rows[1]!, 'app-status-chip')).toBe('In a payroll run');
    expect(texts(rows[1]!, 'button')).toEqual([]);
    expect(text(rows[1]!, '.line__hours')).toBe('8 h');
    expect(text(root, '#saved-heading + p')).toBe('2 workers recorded: 16 h + 1.5 h overtime');
    expect(text(root, 'section[aria-labelledby="saved-heading"] > p:last-child')).toContain(
      "can't be changed",
    );
    // Everyone has been recorded, so there is no more to do: no save bar, and a line that says so.
    expect(root.querySelector('.save-bar')).toBeNull();
    expect(text(root, '.all-done')).toContain('Everyone posted here has been recorded');
  });

  it('names the holiday of the day and marks a rest day or holiday per worker, with the night hours', async () => {
    const holiday = sheet({
      ...recorded,
      holidayName: 'Founders day',
      holidayType: 'Regular',
      entries: [
        entry({
          id: 'e1',
          workerId: 'w1',
          workerName: 'Ana Bautista',
          dayType: 'RegularHoliday',
          nightHours: 3,
          version: 3,
        }),
        entry({ id: 'e2', workerId: 'w2', workerName: 'Juan Dela Cruz', dayType: 'Ordinary' }),
      ],
    });
    const { root } = await open({ sheet: holiday });

    expect(text(root, '.holiday-line')).toBe('Regular holiday: Founders day');
    const rows = Array.from(root.querySelectorAll<HTMLElement>('li.line--saved'));
    expect(text(rows[0]!, 'app-status-chip')).toBe('Regular holiday');
    expect(text(rows[0]!, '.line__hours')).toBe('8 h, 3 h at night');
    expect(rows[1]!.querySelector('app-status-chip')).toBeNull();
  });

  it('sends the night hours typed when a saved entry is changed', async () => {
    const reviseAttendance = stub(() => of(entry()));
    const { root, fixture, api } = await open({ sheet: recorded, api: { reviseAttendance } });
    const row = root.querySelector<HTMLElement>('li.line--saved')!;

    buttonNamed(row, 'Change hours').click();
    await settle(fixture);
    const night = row.querySelector<HTMLInputElement>('input[aria-label^="Night hours"]')!;
    night.value = '2';
    night.dispatchEvent(new Event('input', { bubbles: true }));
    buttonNamed(row, 'Save hours').click();
    await settle(fixture);

    expect(api['reviseAttendance']).toHaveBeenCalledWith(
      'e1',
      expect.objectContaining({ nightHours: 2, version: 3 }),
    );
  });

  it('changes the hours of a saved entry with its own version, and shows the API’s message under the box', async () => {
    const reviseAttendance = stub(() =>
      throwError(() =>
        validationError({ overtimeHours: ['A day has 24 hours. Check the hours entered.'] }),
      ),
    );
    const { root, fixture, api } = await open({ sheet: recorded, api: { reviseAttendance } });
    const row = root.querySelector<HTMLElement>('li.line--saved')!;

    buttonNamed(row, 'Change hours').click();
    await settle(fixture);
    const overtime = row.querySelector<HTMLInputElement>('input[aria-label^="Overtime"]')!;
    overtime.value = '23';
    overtime.dispatchEvent(new Event('input', { bubbles: true }));
    buttonNamed(row, 'Save hours').click();
    await settle(fixture);

    expect(api['reviseAttendance']).toHaveBeenCalledWith('e1', {
      subProjectId: null,
      regularHours: 8,
      overtimeHours: 23,
      nightHours: 0,
      remarks: null,
      version: 3,
    });
    expect(texts(row, '.field__error')).toEqual(['A day has 24 hours. Check the hours entered.']);
    // The person can step out of it.
    buttonNamed(row, 'Cancel').click();
    await settle(fixture);
    expect(text(row, '.line__hours')).toBe('8 h + 1.5 h overtime');
  });

  it('removes an entry only with a reason', async () => {
    const { root, fixture, api } = await open({ sheet: recorded });
    const row = root.querySelector<HTMLElement>('li.line--saved')!;

    buttonNamed(row, 'Remove').click();
    await settle(fixture);
    buttonNamed(root, 'Remove entry').click();
    await settle(fixture);
    expect(api['voidAttendance']).not.toHaveBeenCalled();
    expect(text(root, 'app-void-attendance-dialog .field__error')).toBe(
      'Say why the entry is being removed.',
    );

    const reason = root.querySelector<HTMLTextAreaElement>('app-void-attendance-dialog textarea')!;
    reason.value = 'Entered on the wrong day';
    reason.dispatchEvent(new Event('input', { bubbles: true }));
    buttonNamed(root, 'Remove entry').click();
    await settle(fixture);

    expect(api['voidAttendance']).toHaveBeenCalledWith('e1', {
      reason: 'Entered on the wrong day',
      version: 3,
    });
    expect(root.querySelector('app-void-attendance-dialog')).toBeNull();
  });
});

describe('ProjectTeamTab for people who cannot record', () => {
  it('is read-only on an archived project: it says so and offers no way to record', async () => {
    const { root } = await open({
      project: { ...project, status: 'Archived' },
      sheet: sheet({
        workDate: today,
        entries: [entry({ workerId: 'w2', workerName: 'Juan Dela Cruz' })],
        crewWithoutEntry: [
          { id: 'w1', fullName: 'Ana Bautista', employeeNumber: 'W-005', status: 'Active' },
        ],
        totalRegularHours: 8,
      }),
    });

    expect(text(root, '.alert__title')).toBe('This project is archived');
    expect(root.querySelector('app-hours-field')).toBeNull();
    expect(root.querySelector('.save-bar')).toBeNull();
    expect(texts(root, 'li.line--saved button')).toEqual([]);
    // The crew still shows, and so do the hours already recorded.
    expect(texts(root, '.line--read .worked__name')).toEqual(['Ana Bautista']);
    expect(texts(root, 'li.line--saved .worked__name')).toEqual(['Juan Dela Cruz']);
  });

  it('is read-only for a person without the right to record attendance', async () => {
    const { root } = await open({ permissions: VIEWER_PERMISSIONS });

    expect(root.querySelector('app-hours-field')).toBeNull();
    expect(root.querySelector('.save-bar')).toBeNull();
    expect(texts(root, '.line--read .worked__name')).toEqual([
      'Ana Bautista',
      'Juan Dela Cruz',
      'Jose Garcia',
    ]);
    expect(root.querySelector('input[type="checkbox"]')).toBeNull();
  });

  it('says plainly when no one is posted to the project that day', async () => {
    const { root } = await open({
      sheet: sheet({ workDate: today, crewWithoutEntry: [], entries: [] }),
    });

    expect(text(root, 'app-empty-state')).toContain('No one is posted here on');
    expect(buttonNamed(root, 'Go to workers').getAttribute('href')).toBe('/workers');
  });
});
