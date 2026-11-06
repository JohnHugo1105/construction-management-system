import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { Observable, of, throwError } from 'rxjs';
import type { Mock } from 'vitest';
import { ToastService } from '../../../core/notifications/toast-service';
import { SessionState } from '../../../core/session/session-state';
import { buttonNamed, settle, text, texts, type } from '../../../testing/dom';
import { pageOf, signInForTest } from '../../../testing/session-fixture';
import { PROJECT_ID, projectFixture } from '../../costing/testing/costing-fixtures';
import { ProjectContext } from '../../projects/project-context';
import { ProjectsApi } from '../../projects/projects-api';
import { Project } from '../../projects/projects.models';
import { ruleViolation } from '../../workforce/workforce.fixtures';
import { SiteDiaryApi } from '../site-diary-api';
import { READER, TODAY, WRITER, daysFromToday, entry, listItem } from '../site-diary.fixtures';
import { DiaryEntry, DiaryListItem } from '../site-diary.models';
import { ProjectDiaryTab } from './project-diary-tab';

type Call = (...args: unknown[]) => Observable<unknown>;
const stub = (implementation: Call): Mock<Call> => vi.fn<Call>(implementation);

interface Options {
  readonly project?: Project;
  readonly permissions?: readonly string[];
  readonly items?: readonly DiaryListItem[];
  readonly total?: number;
  readonly report?: DiaryEntry;
  readonly api?: Record<string, Mock<Call>>;
  readonly url?: string;
}

const yesterday = listItem({ id: 'd1', reportDate: daysFromToday(-1), weather: 'Rain', workersOnSite: 12, subcontractorWorkers: 6 });
const dayBefore = listItem({
  id: 'd0',
  reportDate: daysFromToday(-2),
  weather: 'HeavyRain',
  workersOnSite: 1,
  subcontractorWorkers: 0,
  workDoneFirstLine: 'No work: heavy rain all day.',
});

async function open(options: Options = {}) {
  const items = options.items ?? [yesterday, dayBefore];
  const api = {
    list: stub(() => of(pageOf(items, options.total ?? items.length))),
    get: stub(() => of(options.report ?? entry())),
    filledIn: stub(() => of({ onSite: { count: 0, names: [] }, deliveries: [] })),
    create: stub(() => of(entry())),
    correct: stub(() => of(entry())),
    voidReport: stub(() => of(entry({ status: 'Void', canChange: false }))),
    ...options.api,
  };
  TestBed.configureTestingModule({
    providers: [
      provideRouter([{ path: 'projects/:id/diary', component: ProjectDiaryTab }]),
      provideHttpClient(),
      provideHttpClientTesting(),
      ProjectContext,
      { provide: ProjectsApi, useValue: {} },
      { provide: SiteDiaryApi, useValue: api },
    ],
  });
  await signInForTest(TestBed.inject(SessionState), TestBed.inject(HttpTestingController), {
    roles: ['Custom'],
    permissions: options.permissions ?? WRITER,
  });
  TestBed.inject(ProjectContext).replace(options.project ?? projectFixture());
  const harness = await RouterTestingHarness.create();
  await harness.navigateByUrl(options.url ?? `/projects/${PROJECT_ID}/diary`, ProjectDiaryTab);
  await settle(harness.fixture);
  return { harness, root: harness.fixture.nativeElement as HTMLElement, api };
}

const submit = () => document.body.querySelector<HTMLButtonElement>('button[type="submit"]')!.click();

describe('ProjectDiaryTab', () => {
  it('lists the days newest first with the weather, the headcount and the first line of the work done', async () => {
    const { root, api } = await open();

    expect(api.list).toHaveBeenCalledWith(PROJECT_ID, 1, 25);
    const rows = Array.from(root.querySelectorAll('.diary-row'));
    expect(rows).toHaveLength(2);
    expect(rows[0]!.textContent).toContain('Rain');
    expect(rows[0]!.textContent).toContain('12 workers on site, 6 subcontractor workers');
    expect(rows[0]!.textContent).toContain('Poured the second-floor slab.');
    expect(rows[1]!.textContent).toContain('Heavy rain');
    expect(rows[1]!.textContent).toContain('1 worker on site');
    expect(rows[1]!.textContent).not.toContain('subcontractor');
  });

  it('leads with one primary action, "Write today\'s report", while today has none', async () => {
    const { root } = await open();

    expect(texts(root, 'button.btn--primary')).toEqual(["Write today's report"]);
    expect(texts(root, 'button').filter((label) => label.includes('earlier day'))).toEqual(['Write a report for an earlier day']);
  });

  it('drops the primary action once today has a report', async () => {
    const { root } = await open({ items: [listItem({ id: 'today', reportDate: TODAY }), yesterday] });

    expect(root.querySelector('button.btn--primary')).toBeNull();
    expect(text(root, 'main, section.diary')).not.toContain("Write today's report");
    // Writing a report for another day stays one step away.
    expect(texts(root, 'button')).toContain('Write a report for an earlier day');
  });

  it('shows a reader the reports and no way to write one', async () => {
    const { root } = await open({ permissions: READER });

    expect(root.querySelectorAll('.diary-row')).toHaveLength(2);
    expect(root.querySelector('button.btn--primary')).toBeNull();
    expect(texts(root, 'button').some((label) => label.includes('Write'))).toBe(false);
  });

  it('leads an empty list to the next action for a writer, and just says so for a reader', async () => {
    const writer = await open({ items: [] });
    expect(text(writer.root, 'app-empty-state')).toContain('No site reports yet');
    expect(texts(writer.root, 'app-empty-state button')).toEqual(["Write today's report"]);
    // One primary action only: the header's is not repeated beside the empty state's.
    expect(writer.root.querySelectorAll('button.btn--primary')).toHaveLength(1);
    TestBed.resetTestingModule();

    const reader = await open({ items: [], permissions: READER });
    expect(text(reader.root, 'app-empty-state')).toContain('Reports for this project will appear here once someone writes them.');
    expect(reader.root.querySelectorAll('button')).toHaveLength(0);
  });

  it('is read-only on an archived project, with one line that says why', async () => {
    const { root } = await open({ project: projectFixture({ status: 'Archived' }) });

    expect(root.querySelectorAll('.diary-row')).toHaveLength(2);
    expect(root.querySelector('button.btn--primary')).toBeNull();
    expect(text(root, '.diary')).toContain('This project is archived, so its reports can be read but not written or changed.');
  });

  it('shows a failed read in plain words with a way to try again', async () => {
    const list = stub(() => throwError(() => ruleViolation('The diary could not be read.')));
    const { root } = await open({ api: { list } });

    expect(root.querySelector('app-api-error-alert')).not.toBeNull();
    expect(root.querySelectorAll('.diary-row')).toHaveLength(0);
    expect(texts(root, 'app-api-error-alert button')).toEqual(['Try again']);
  });

  it('pages through a long diary through the address bar', async () => {
    const { harness, root, api } = await open({ total: 60 });

    expect(text(root, 'app-pager')).toContain('Showing 1 to 25 of 60 reports');
    buttonNamed(root, 'Next').click();
    await settle(harness.fixture);

    expect(api.list).toHaveBeenLastCalledWith(PROJECT_ID, 2, 25);
    // Only the first page can say whether today has a report, so the action waits on it.
    expect(root.querySelector('button.btn--primary')).toBeNull();
  });

  it('opens a day to read it in full, with the day’s attendance and deliveries beside it', async () => {
    const { harness, root, api } = await open();

    (root.querySelector('.diary-row') as HTMLElement).click();
    await settle(harness.fixture);

    expect(api.get).toHaveBeenCalledWith(PROJECT_ID, yesterday.reportDate);
    const dialog = document.body.querySelector('app-diary-entry-dialog')!;
    expect(text(dialog, '.heading')).toBe('Work done');
    expect(dialog.textContent).toContain('Poured the second-floor slab.');
    expect(dialog.textContent).toContain('The concrete truck came late.');
    expect(dialog.textContent).toContain('Subcontractor workers on site: 6');
    expect(dialog.textContent).toContain('Ana Abad, Juan Reyes');
    expect(dialog.textContent).toContain('Rebar 16 mm');
    expect(dialog.textContent).toContain('Written by Pia Santos.');
    // Sections nobody filled in are not shown as empty headings.
    expect(texts(dialog, '.heading')).not.toContain('Safety notes');
    expect(texts(dialog, 'button')).toEqual(expect.arrayContaining(['Close', 'Void report', 'Correct report']));
  });

  it('tells a writer who may not change the report who can, and a reader nothing about changing', async () => {
    const locked = entry({ canChange: false });
    const writer = await open({ report: locked });
    (writer.root.querySelector('.diary-row') as HTMLElement).click();
    await settle(writer.harness.fixture);
    const dialog = document.body.querySelector('app-diary-entry-dialog')!;
    expect(dialog.textContent).toContain('Only the person who wrote this report can change it');
    expect(texts(dialog, 'button')).not.toContain('Correct report');
    TestBed.resetTestingModule();
    document.body.querySelectorAll('app-diary-entry-dialog').forEach((node) => node.remove());

    const reader = await open({ report: locked, permissions: READER });
    (reader.root.querySelector('.diary-row') as HTMLElement).click();
    await settle(reader.harness.fixture);
    const readerDialog = document.body.querySelector('app-diary-entry-dialog')!;
    expect(readerDialog.textContent).not.toContain('Only the person who wrote this report');
    expect(texts(readerDialog, 'button')).not.toEqual(expect.arrayContaining(['Correct report']));
    expect(texts(readerDialog, 'button')).not.toEqual(expect.arrayContaining(['Void report']));
  });

  it('writes today’s report from the tab, then shows the list as it now is', async () => {
    const { harness, root, api } = await open();
    const toast = TestBed.inject(ToastService);
    const success = vi.spyOn(toast, 'success');

    buttonNamed(root, "Write today's report").click();
    await settle(harness.fixture);
    type(document.body, 'textarea[formcontrolname="workDone"]', 'Formwork for the roof beams.');
    submit();
    await settle(harness.fixture);

    expect(api.create).toHaveBeenCalledWith(
      PROJECT_ID,
      TODAY,
      expect.objectContaining({ weather: 'Fair', workDone: 'Formwork for the roof beams.', subcontractorWorkers: 0 }),
      expect.any(String),
    );
    expect(success).toHaveBeenCalledWith('Report saved.');
    expect(api.list).toHaveBeenCalledTimes(2);
    expect(document.body.querySelector('app-diary-form-dialog')).toBeNull();
  });

  it('writes a report for an earlier day, starting on yesterday', async () => {
    const { harness, root } = await open();

    buttonNamed(root, 'Write a report for an earlier day').click();
    await settle(harness.fixture);

    expect(document.body.querySelector<HTMLInputElement>('input[formcontrolname="reportDate"]')!.value).toBe(daysFromToday(-1));
  });

  it('corrects a report from its page and reloads the list', async () => {
    const { harness, root, api } = await open();
    (root.querySelector('.diary-row') as HTMLElement).click();
    await settle(harness.fixture);

    buttonNamed(document.body, 'Correct report').click();
    await settle(harness.fixture);
    expect(document.body.querySelector('app-diary-entry-dialog')).toBeNull();
    type(document.body, 'textarea[formcontrolname="workDone"]', 'Poured the slab and cured it.');
    submit();
    await settle(harness.fixture);

    expect(api.correct).toHaveBeenCalledWith(PROJECT_ID, yesterday.reportDate, expect.objectContaining({ workDone: 'Poured the slab and cured it.' }), 0);
    expect(api.list).toHaveBeenCalledTimes(2);
  });

  it('voids a report only with a reason, and says the day can be written again', async () => {
    const { harness, root, api } = await open();
    const success = vi.spyOn(TestBed.inject(ToastService), 'success');
    (root.querySelector('.diary-row') as HTMLElement).click();
    await settle(harness.fixture);

    buttonNamed(document.body, 'Void report').click();
    await settle(harness.fixture);
    expect(document.body.textContent).toContain('you can then write a new report for that day');
    submit();
    await settle(harness.fixture);
    expect(api.voidReport).not.toHaveBeenCalled();
    expect(document.body.textContent).toContain('Say why this report is being voided.');

    type(document.body, 'textarea[formcontrolname="reason"]', '  Wrote it on the wrong project  ');
    submit();
    await settle(harness.fixture);

    expect(api.voidReport).toHaveBeenCalledWith(PROJECT_ID, yesterday.reportDate, 'Wrote it on the wrong project', 0);
    expect(success).toHaveBeenCalledWith('Report voided. You can write a new one for that day.');
    expect(api.list).toHaveBeenCalledTimes(2);
  });

  it('shows the server’s words when a void is refused and keeps the reason typed', async () => {
    const voidReport = stub(() =>
      throwError(() => ruleViolation('You could change this report until the end of 3 Nov 2026, and that time has passed. Ask a Project Manager or an Administrator to have it voided.')),
    );
    const { harness, root } = await open({ api: { voidReport } });
    (root.querySelector('.diary-row') as HTMLElement).click();
    await settle(harness.fixture);
    buttonNamed(document.body, 'Void report').click();
    await settle(harness.fixture);

    type(document.body, 'textarea[formcontrolname="reason"]', 'Wrong day');
    submit();
    await settle(harness.fixture);

    expect(document.body.textContent).toContain('that time has passed');
    expect(document.body.querySelector<HTMLTextAreaElement>('textarea[formcontrolname="reason"]')!.value).toBe('Wrong day');
  });
});
