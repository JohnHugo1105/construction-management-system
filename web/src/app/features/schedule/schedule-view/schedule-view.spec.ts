import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Observable, of, throwError } from 'rxjs';
import type { Mock } from 'vitest';
import { ApiError } from '../../../core/api/api-error';
import { Permission } from '../../../core/session/permissions';
import { SessionState } from '../../../core/session/session-state';
import { buttonNamed, settle, text, texts, type } from '../../../testing/dom';
import { signInForTest } from '../../../testing/session-fixture';
import { projectFixture } from '../../costing/testing/costing-fixtures';
import { ProjectContext } from '../../projects/project-context';
import { ProjectsApi } from '../../projects/projects-api';
import { Project } from '../../projects/projects.models';
import { ScheduleApi } from '../schedule-api';
import { Schedule, ScheduleCurve } from '../schedule.models';
import { OFFICE_LINE, curveFixture, scheduleFixture } from '../testing/schedule-fixtures';
import { ScheduleView } from './schedule-view';

type Call = (...args: unknown[]) => Observable<unknown>;
const stub = (implementation: Call): Mock<Call> => vi.fn<Call>(implementation);

interface Options {
  readonly project?: Project;
  readonly permissions?: readonly string[];
  readonly schedule?: Schedule;
  readonly curve?: ScheduleCurve;
  readonly api?: Record<string, Mock<Call>>;
}

const READER = [Permission.projectsRead, Permission.progressRead, Permission.scheduleRead];
const PLANNER = [...READER, Permission.scheduleWrite];

async function open(options: Options = {}) {
  const schedule = options.schedule ?? scheduleFixture();
  const api = {
    get: stub(() => of(schedule)),
    curve: stub(() => of(options.curve ?? curveFixture())),
    create: stub(() => of(schedule)),
    update: stub(() => of(schedule)),
    remove: stub(() => of(schedule)),
    setBaseline: stub(() => of({ ...schedule, baselineSetAtUtc: '2026-11-02T02:00:00Z' })),
    ...options.api,
  };
  TestBed.configureTestingModule({
    providers: [
      provideHttpClient(),
      provideHttpClientTesting(),
      ProjectContext,
      { provide: ProjectsApi, useValue: {} },
      { provide: ScheduleApi, useValue: api },
    ],
  });
  await signInForTest(TestBed.inject(SessionState), TestBed.inject(HttpTestingController), {
    roles: ['Custom'],
    permissions: options.permissions ?? PLANNER,
  });
  TestBed.inject(ProjectContext).replace(options.project ?? projectFixture());
  const fixture = TestBed.createComponent(ScheduleView);
  await settle(fixture);
  return { fixture, root: fixture.nativeElement as HTMLElement, api };
}

const dialog = () => document.querySelector<HTMLDialogElement>('dialog')!;

describe('ScheduleView: the plan', () => {
  it('lists each activity with its dates, percent, budget and a state in words', async () => {
    const { root } = await open();

    expect(texts(root, '.activity .row-card__title')).toEqual([
      'Foundations',
      'Masonry walls',
      'Floor tiles',
    ]);
    expect(texts(root, '.activity app-status-chip')).toEqual(['Done', 'Behind', 'On track']);
    expect(text(root, '.activity .row-card__meta')).toBe(
      '1 Sep 2026 to 30 Sep 2026 · 30 days · 100% complete · ₱1,000,000.00 budget',
    );
  });

  it('sums the plan up in words from the server figures', async () => {
    const { root } = await open();

    expect(texts(root, '.facts__item')).toEqual([
      '₱1,300,000.00 of work planned by today, of ₱2,200,000.00 budgeted',
      '1 of 3 activities is behind',
      "1 budget line (₱200,000.00) is not in any activity, so it is planned evenly over the project's dates.",
    ]);
  });

  it('draws the S-curve with a sentence and the same figures as a table', async () => {
    const { root } = await open();

    const chart = root.querySelector('svg.curve__chart')!;
    expect(chart.getAttribute('role')).toBe('img');
    expect(chart.getAttribute('aria-label')).toContain('By today (2 Nov 2026)');
    expect(root.querySelectorAll('polyline')).toHaveLength(3);
    expect(root.querySelectorAll('.curve__table tbody tr')).toHaveLength(5);
    expect(text(root, '.curve__table tbody tr:nth-child(3) th')).toBe('2 Nov 2026 (today)');
    expect(text(root, '.curve__table tbody tr:nth-child(4) td:nth-of-type(2)')).toBe('Not yet');
  });

  it('flags an activity without budget lines and leaves the S-curve out while nothing has weight', async () => {
    const base = scheduleFixture();
    const { root, api } = await open({
      schedule: {
        ...base,
        usesSchedule: false,
        planWeightMinor: 0,
        activities: [
          {
            ...base.activities[0]!,
            needsLines: true,
            weightMinor: 0,
            percentCompleteBps: null,
            budgetLineIds: [],
          },
        ],
      },
    });

    expect(text(root, '.activity__note')).toBe('Link budget lines to include it in the plan.');
    expect(text(root, '.facts__item')).toContain(
      'Link budget lines to an activity to plan with this schedule.',
    );
    expect(root.querySelector('app-s-curve-chart')).toBeNull();
    expect(api.curve).toHaveBeenCalledTimes(1);
  });

  it('says why the plan is not used when budget lines are outside the activities and the project has no end date', async () => {
    const { root } = await open({
      schedule: scheduleFixture({ usesSchedule: false }),
    });

    const facts = texts(root, '.facts__item').join(' ');
    expect(facts).toContain("the project has no end date, so they can't be planned yet");
    expect(facts).not.toContain('Link budget lines to an activity');
    expect(root.querySelector('app-s-curve-chart')).toBeNull();
  });
});

describe('ScheduleView: before there is a plan', () => {
  it('explains that the budget comes first when the project has no budget lines', async () => {
    const { root } = await open({
      schedule: scheduleFixture({ activities: [], budgetLines: [], usesSchedule: false }),
    });

    expect(text(root, 'app-empty-state')).toContain('Add the budget before planning the schedule');
    expect(root.textContent).not.toContain('Add activity');
  });

  it('invites a planner to add the first activity', async () => {
    const { root } = await open({
      schedule: scheduleFixture({ activities: [], usesSchedule: false }),
    });

    expect(text(root, 'app-empty-state')).toContain('Plan the work over time');
    expect(buttonNamed(root, 'Add activity')).toBeTruthy();
  });

  it('gives a reader the plan without any planning buttons', async () => {
    const { root } = await open({ permissions: READER });

    expect(root.querySelectorAll('.activity')).toHaveLength(3);
    expect(root.textContent).not.toContain('Add activity');
    expect(root.textContent).not.toContain('Set baseline');
    expect(root.querySelector('.activity__actions')).toBeNull();
  });

  it('keeps an archived project read-only and says why', async () => {
    const { root } = await open({ project: projectFixture({ status: 'Archived' }) });

    expect(root.textContent).not.toContain('Add activity');
    expect(root.textContent).toContain(
      "This project is archived, so its schedule can't be changed.",
    );
  });
});

describe('ScheduleView: planning', () => {
  it('asks before setting the baseline, says what it means, and sends the activities on screen', async () => {
    const { fixture, root, api } = await open();

    buttonNamed(root, 'Set baseline').click();
    await settle(fixture);
    expect(text(dialog(), 'h2')).toBe('Set the baseline?');
    expect(text(dialog(), '.confirm-message')).toContain(
      'saves the planned dates of all 3 activities as the baseline',
    );

    buttonNamed(dialog(), 'Set baseline').click();
    await settle(fixture);

    expect(api.setBaseline).toHaveBeenCalledTimes(1);
    expect(api.setBaseline.mock.calls[0]![1]).toHaveLength(3);
    expect(document.querySelector('dialog')).toBeNull();
    expect(text(root, '.panel-head .text-muted')).toContain('Baseline set 2 Nov 2026');
  });

  it('shows a schedule that changed meanwhile as a warning with a way to reload', async () => {
    const conflict = new ApiError({
      status: 409,
      title: 'Someone else changed this',
      detail: 'Reload to see the latest version.',
      code: 'concurrency_conflict',
    });
    const { fixture, root, api } = await open({
      api: { setBaseline: stub(() => throwError(() => conflict)) },
    });

    buttonNamed(root, 'Set baseline').click();
    await settle(fixture);
    buttonNamed(dialog(), 'Set baseline').click();
    await settle(fixture);

    expect(text(root, 'app-api-error-alert')).toContain('Reload to see the latest version.');
    buttonNamed(root, 'Reload schedule').click();
    await settle(fixture);
    expect(api.get).toHaveBeenCalledTimes(2);
  });

  it('adds an activity with the lines picked, offering only lines no other activity holds', async () => {
    const { fixture, root, api } = await open();

    buttonNamed(root, 'Add activity').click();
    await settle(fixture);
    const form = dialog();
    expect(texts(form, '.choice__label')).toEqual(['Site office']);
    expect(form.textContent).toContain('3 lines are already in other activities and not listed.');

    type(form, 'input[formcontrolname="name"]', 'Site office set-up');
    type(form, 'input[formcontrolname="plannedStart"]', '2026-11-03');
    type(form, 'input[formcontrolname="plannedFinish"]', '2026-11-10');
    form.querySelector<HTMLInputElement>('.choice input')!.click();
    await settle(fixture);
    buttonNamed(form, 'Add activity').click();
    await settle(fixture);

    expect(api.create).toHaveBeenCalledTimes(1);
    const [, details, requestId] = api.create.mock.calls[0]!;
    expect(details).toEqual({
      name: 'Site office set-up',
      plannedStart: '2026-11-03',
      plannedFinish: '2026-11-10',
      budgetLineIds: [OFFICE_LINE],
    });
    expect(typeof requestId).toBe('string');
    expect(document.querySelector('dialog')).toBeNull();
  });

  it("keeps the form open and puts the server's message about a line under the picker", async () => {
    const refusal = new ApiError({
      status: 422,
      title: 'Check the highlighted fields',
      detail: 'Some details need fixing.',
      code: 'validation_failed',
      errors: { 'budgetLineIds[0]': ['"Site office" is already in the activity "Mobilization".'] },
    });
    const { fixture, root, api } = await open({
      api: { create: stub(() => throwError(() => refusal)) },
    });

    buttonNamed(root, 'Add activity').click();
    await settle(fixture);
    const form = dialog();
    type(form, 'input[formcontrolname="name"]', 'Site office set-up');
    type(form, 'input[formcontrolname="plannedFinish"]', '2026-11-30');
    form.querySelector<HTMLInputElement>('.choice input')!.click();
    await settle(fixture);
    buttonNamed(form, 'Add activity').click();
    await settle(fixture);

    expect(api.create).toHaveBeenCalledTimes(1);
    expect(text(form, 'app-field-group .field__error')).toBe(
      '"Site office" is already in the activity "Mobilization".',
    );
    expect(form.querySelector<HTMLInputElement>('input[formcontrolname="name"]')!.value).toBe(
      'Site office set-up',
    );
  });

  it('changes an activity with its version, keeping its own lines ticked', async () => {
    const { fixture, root, api } = await open();

    buttonNamed(root, 'Change').click();
    await settle(fixture);
    const form = dialog();
    expect(texts(form, '.choice__label')).toEqual(['Footings', 'Site office']);
    expect(form.querySelector<HTMLInputElement>('.choice input')!.checked).toBe(true);
    buttonNamed(form, 'Save activity').click();
    await settle(fixture);

    expect(api.update).toHaveBeenCalledTimes(1);
    const [, activityId, details] = api.update.mock.calls[0]!;
    expect(activityId).toBe(scheduleFixture().activities[0]!.id);
    expect(details).toMatchObject({
      name: 'Foundations',
      budgetLineIds: [scheduleFixture().budgetLines[0]!.id],
    });
  });

  it('removes an activity only with a reason', async () => {
    const { fixture, root, api } = await open();

    root.querySelector<HTMLButtonElement>('.activity app-menu button')!.click();
    await settle(fixture);
    buttonNamed(root, 'Remove activity').click();
    await settle(fixture);
    const form = dialog();
    expect(text(form, 'h2')).toBe('Remove this activity?');

    type(form, 'textarea', 'Merged into Masonry walls');
    buttonNamed(form, 'Remove activity').click();
    await settle(fixture);

    expect(api.remove).toHaveBeenCalledTimes(1);
    expect(api.remove.mock.calls[0]![2]).toBe('Merged into Masonry walls');
  });
});
