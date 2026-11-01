import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { Observable, of, throwError } from 'rxjs';
import type { Mock } from 'vitest';
import { ApiError } from '../../../core/api/api-error';
import { ToastService } from '../../../core/notifications/toast-service';
import { Permission } from '../../../core/session/permissions';
import { SessionState } from '../../../core/session/session-state';
import { buttonNamed, settle, text, texts, type } from '../../../testing/dom';
import { signInForTest } from '../../../testing/session-fixture';
import { PROJECT_ID, projectFixture } from '../../costing/testing/costing-fixtures';
import { ProjectContext } from '../../projects/project-context';
import { ProjectsApi } from '../../projects/projects-api';
import { Project } from '../../projects/projects.models';
import { ProgressApi } from '../progress-api';
import { Progress } from '../progress.models';
import {
  COLUMNS_ID,
  FOOTINGS_ID,
  manyLinesFixture,
  progressFixture,
  untouchedFixture,
} from '../testing/progress-fixtures';
import { ProjectProgressTab } from './project-progress-tab';

type Call = (...args: unknown[]) => Observable<unknown>;
const stub = (implementation: Call): Mock<Call> => vi.fn<Call>(implementation);

interface Options {
  readonly project?: Project;
  readonly permissions?: readonly string[];
  readonly report?: Progress;
  readonly api?: Record<string, Mock<Call>>;
}

const READER = [Permission.projectsRead, Permission.progressRead];
const WRITER = [...READER, Permission.progressWrite];

async function open(options: Options = {}) {
  const api = {
    get: stub(() => of(options.report ?? progressFixture())),
    history: stub(() => of({ items: [], total: 0, page: 1, pageSize: 10 })),
    record: stub(() => of({ asOfDate: '2026-11-02', linesUpdated: 1, recordedAtUtc: '2026-11-02T03:00:00Z' })),
    ...options.api,
  };
  TestBed.configureTestingModule({
    providers: [
      provideRouter([{ path: 'projects/:id/progress', component: ProjectProgressTab }]),
      provideHttpClient(),
      provideHttpClientTesting(),
      ProjectContext,
      { provide: ProjectsApi, useValue: {} },
      { provide: ProgressApi, useValue: api },
    ],
  });
  await signInForTest(
    TestBed.inject(SessionState),
    TestBed.inject(HttpTestingController),
    { roles: ['Custom'], permissions: options.permissions ?? WRITER },
  );
  TestBed.inject(ProjectContext).replace(options.project ?? projectFixture());
  const harness = await RouterTestingHarness.create();
  await harness.navigateByUrl(`/projects/${PROJECT_ID}/progress`, ProjectProgressTab);
  await settle(harness.fixture);
  return { harness, root: harness.fixture.nativeElement as HTMLElement, api };
}

const percentField = (root: HTMLElement, description: string) =>
  root.querySelector<HTMLInputElement>(`input[appControl][type="text"]`)
    ? Array.from(root.querySelectorAll<HTMLElement>('app-form-field'))
        .find((field) => field.textContent?.includes(`Percent complete, ${description}`))!
        .querySelector<HTMLInputElement>('input')!
    : null;

async function typePercent(
  harness: RouterTestingHarness,
  root: HTMLElement,
  description: string,
  value: string,
) {
  const input = percentField(root, description)!;
  input.value = value;
  input.dispatchEvent(new Event('input', { bubbles: true }));
  await settle(harness.fixture);
}

describe('ProjectProgressTab: the summary', () => {
  it('leads with percent complete, then what was spent for the work, the forecast and the margin, from the server figures', async () => {
    const { root } = await open();

    expect(text(root, '.headline')).toBe('47% complete');
    expect(texts(root, '.facts__item')).toEqual([
      'Spent ₱400,000.00 for work worth ₱800,000.00',
      'Forecast to finish at ₱850,000.00, which is ₱850,000.00 under the budget.',
      'Forecast margin on the contract price: ₱2,150,000.00',
      'Each peso spent is earning ₱2.00 of work.',
      'Ahead of schedule: the work done is 118% of what was planned by today.',
    ]);
    expect(text(root, '#progress-heading + p')).toBe('Last updated 20 Oct 2026 · Amounts include VAT');
  });

  it('leaves out the margin without a contract price and the schedule without a target end date', async () => {
    const report = progressFixture();
    const { root } = await open({
      report: {
        ...report,
        earnedValue: {
          ...report.earnedValue,
          contractPriceMinor: null,
          forecastMarginMinor: null,
          spiBps: null,
          plannedMinor: null,
          plannedPercentBps: null,
        },
      },
    });

    const facts = texts(root, '.facts__item');
    expect(facts.some((fact) => fact.includes('Forecast margin'))).toBe(false);
    expect(facts.some((fact) => fact.includes('schedule'))).toBe(false);
  });

  it('says what to do instead of showing a summary before any progress exists', async () => {
    const { root } = await open({ report: untouchedFixture() });

    expect(root.querySelector('.headline')).toBeNull();
    expect(text(root, 'app-empty-state')).toContain('Record progress to see how far along the project is');
    // The update form is right there.
    expect(root.querySelectorAll('input[type="text"]').length).toBe(3);
  });

  it('tells people who cannot record that someone else has to', async () => {
    const { root } = await open({ report: untouchedFixture(), permissions: READER });

    expect(text(root, 'app-empty-state')).toContain('A project manager or cost engineer can record it.');
    expect(root.querySelectorAll('input').length).toBe(0);
  });

  it('points to the budget when the project has no budget lines', async () => {
    const { root } = await open({ report: progressFixture({ hasProgress: false, divisions: [] }) });

    expect(text(root, 'app-empty-state')).toContain('No budget lines yet');
    expect(root.querySelector('.savebar')).toBeNull();
  });
});

describe('ProjectProgressTab: the lines', () => {
  it('groups lines by division with each division rolled up, and a short list starts open', async () => {
    const { root } = await open();

    expect(texts(root, '.division__name')).toEqual(['Concrete', 'Unclassified']);
    expect(texts(root, '.division__figure')).toEqual(['40%', '100%']);
    expect(texts(root, '.division__sub')[0]).toBe('₱600,000.00 worth of ₱1,500,000.00 · 2 lines');
    expect(texts(root, '.line__name')).toEqual(['Footings', 'Columns', 'Site office']);
    expect(texts(root, '.line__meta')[0]).toBe('3100 · ₱1,000,000.00 budget · Updated 20 Oct 2026');
  });

  it('folds and opens a division', async () => {
    const { harness, root } = await open();
    const head = root.querySelector<HTMLButtonElement>('.division__head')!;
    expect(head.getAttribute('aria-expanded')).toBe('true');

    head.click();
    await settle(harness.fixture);

    expect(head.getAttribute('aria-expanded')).toBe('false');
    expect(texts(root, '.line__name')).toEqual(['Site office']);
  });

  it('starts folded and draws only part of a very long division until asked for more', async () => {
    const { harness, root } = await open({ report: manyLinesFixture(100) });
    expect(root.querySelectorAll('.line').length).toBe(0);

    root.querySelector<HTMLButtonElement>('.division__head')!.click();
    await settle(harness.fixture);
    expect(root.querySelectorAll('.line').length).toBe(40);

    buttonNamed(root, 'Show more lines (60 left)').click();
    await settle(harness.fixture);
    expect(root.querySelectorAll('.line').length).toBe(80);
  });

  it('shows a line that was never updated as not updated yet', async () => {
    const { root } = await open({ report: untouchedFixture() });

    expect(texts(root, '.line__meta')[0]).toContain('Not updated yet');
  });
});

describe('ProjectProgressTab: recording', () => {
  it('shows nothing to save until a line changes, then counts the changed lines', async () => {
    const { harness, root } = await open();
    expect(root.querySelector('.savebar')).toBeNull();

    await typePercent(harness, root, 'Footings', '60');
    expect(text(root, '.savebar__count')).toBe('1 line changed');

    await typePercent(harness, root, 'Columns', '25');
    expect(text(root, '.savebar__count')).toBe('2 lines changed');

    // Typing a line back to what is saved is no change.
    await typePercent(harness, root, 'Columns', '20');
    expect(text(root, '.savebar__count')).toBe('1 line changed');
  });

  it('steps a line by 5 points and stops at 100', async () => {
    const { harness, root } = await open();

    root.querySelector<HTMLButtonElement>('[aria-label="Raise Footings by 5%"]')!.click();
    await settle(harness.fixture);
    expect(percentField(root, 'Footings')!.value).toBe('55');

    root.querySelector<HTMLButtonElement>('[aria-label="Lower Footings by 5%"]')!.click();
    root.querySelector<HTMLButtonElement>('[aria-label="Lower Footings by 5%"]')!.click();
    await settle(harness.fixture);
    expect(percentField(root, 'Footings')!.value).toBe('45');
  });

  it('asks why only when a line goes down', async () => {
    const { harness, root } = await open();

    await typePercent(harness, root, 'Footings', '60');
    expect(root.textContent).not.toContain('Why did it go down?');

    await typePercent(harness, root, 'Footings', '40');
    expect(root.textContent).toContain('Why did it go down?');
  });

  it('shows a typed percent that is not a percent on its line and does not save', async () => {
    const { harness, root, api } = await open();

    await typePercent(harness, root, 'Footings', 'lots');
    buttonNamed(root, 'Save progress').click();
    await settle(harness.fixture);

    expect(root.textContent).toContain('Enter a percent between 0 and 100.');
    expect(api['record']).not.toHaveBeenCalled();
  });

  it('saves every changed line together with the date, the note and the percent the person saw, then reloads', async () => {
    const { harness, root, api } = await open();
    await typePercent(harness, root, 'Footings', '40');
    type(root, 'textarea', 'Two footings were redone');
    await typePercent(harness, root, 'Columns', '30');

    buttonNamed(root, 'Save progress').click();
    await settle(harness.fixture);

    expect(api['record']).toHaveBeenCalledTimes(1);
    const [projectId, request] = api['record']!.mock.calls[0] as [string, Record<string, unknown>];
    expect(projectId).toBe(PROJECT_ID);
    expect(request['asOfDate']).toMatch(/^\d{4}-\d{2}-\d{2}$/);
    expect(request['clientRequestId']).toEqual(expect.any(String));
    expect(request['lines']).toEqual([
      { budgetLineId: FOOTINGS_ID, percentBps: 4000, note: 'Two footings were redone', expectedPercentBps: 5000 },
      { budgetLineId: COLUMNS_ID, percentBps: 3000, note: null, expectedPercentBps: 2000 },
    ]);
    expect(TestBed.inject(ToastService).toasts().at(-1)?.message).toBe('Progress saved for 1 line.');
    expect(api['get']).toHaveBeenCalledTimes(2);
    expect(root.querySelector('.savebar')).toBeNull();
  });

  it('puts the server message on the line it is about and keeps what the person typed', async () => {
    const refusal = new ApiError({
      status: 422,
      title: 'Some details need your attention',
      detail: 'Check the highlighted fields.',
      code: 'validation_failed',
      errors: { 'lines[0].note': ['Say why the progress went down, for example rework.'] },
    });
    const { harness, root } = await open({ api: { record: stub(() => throwError(() => refusal)) } });
    await typePercent(harness, root, 'Footings', '40');

    buttonNamed(root, 'Save progress').click();
    await settle(harness.fixture);

    expect(text(root, '.line--edited .field__error')).toBe('Say why the progress went down, for example rework.');
    expect(percentField(root, 'Footings')!.value).toBe('40');
    expect(text(root, '.savebar__count')).toBe('1 line changed');
  });

  it('shows a stale figure as a conflict with a way to reload, and keeps the edit', async () => {
    const conflict = new ApiError({
      status: 409,
      title: 'This record changed',
      detail: 'Someone else changed this record while you were editing.',
      code: 'concurrency_conflict',
    });
    const { harness, root, api } = await open({ api: { record: stub(() => throwError(() => conflict)) } });
    await typePercent(harness, root, 'Footings', '60');

    buttonNamed(root, 'Save progress').click();
    await settle(harness.fixture);

    expect(text(root, '.savebar')).toContain('This record changed');
    buttonNamed(root, 'Reload progress').click();
    await settle(harness.fixture);
    expect(api['get']).toHaveBeenCalledTimes(2);
    expect(text(root, '.savebar__count')).toBe('1 line changed');
  });

  it('discards the edits when asked', async () => {
    const { harness, root } = await open();
    await typePercent(harness, root, 'Footings', '60');

    buttonNamed(root, 'Discard changes').click();
    await settle(harness.fixture);

    expect(root.querySelector('.savebar')).toBeNull();
    expect(percentField(root, 'Footings')!.value).toBe('50');
  });
});

describe('ProjectProgressTab: who may change it', () => {
  it('shows the figures only, with history, to someone who can read but not record', async () => {
    const { root } = await open({ permissions: READER });

    expect(root.querySelectorAll('input').length).toBe(0);
    expect(root.querySelector('.savebar')).toBeNull();
    expect(texts(root, '.line__read')[0]).toBe('50% complete');
    expect(buttonNamed(root, 'History')).toBeTruthy();
  });

  it('is read-only on an archived project and says why', async () => {
    const { root } = await open({ project: projectFixture({ status: 'Archived' }) });

    expect(root.querySelectorAll('input').length).toBe(0);
    expect(text(root, '#lines-heading + p')).toBe("Archived: progress can't be changed.");
  });
});

describe('ProjectProgressTab: history', () => {
  it('opens a line’s history in a dialog', async () => {
    const history = stub(() =>
      of({
        items: [
          {
            id: 'h1',
            asOfDate: '2026-10-20',
            percentBps: 5000,
            note: 'Poured the last footing',
            recordedBy: 'Paolo Project-Manager',
            recordedAtUtc: '2026-10-20T03:00:00Z',
          },
        ],
        total: 1,
        page: 1,
        pageSize: 10,
      }),
    );
    const { harness, root, api } = await open({ api: { history } });

    root.querySelector<HTMLButtonElement>('[aria-label="History of Footings"]')!.click();
    await settle(harness.fixture);

    expect(api['history']).toHaveBeenCalledWith(FOOTINGS_ID, 1, 10);
    const dialog = root.querySelector('app-progress-history-dialog')!;
    expect(text(dialog, '.dialog__title')).toBe('History: Footings');
    expect(text(dialog, '.history__main')).toBe('50% on 20 Oct 2026');
    expect(text(dialog, '.history__meta')).toContain('Paolo Project-Manager');
    expect(text(dialog, '.history__note')).toBe('Poured the last footing');
  });
});
