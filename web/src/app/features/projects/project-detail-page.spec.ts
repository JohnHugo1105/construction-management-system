import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ChangeDetectionStrategy, Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { Observable, of, throwError } from 'rxjs';
import type { Mock } from 'vitest';
import { ApiError } from '../../core/api/api-error';
import { Permission } from '../../core/session/permissions';
import { SessionState } from '../../core/session/session-state';
import { buttonNamed, settle, text, texts } from '../../testing/dom';
import { signInForTest } from '../../testing/session-fixture';
import { ProjectDetailPage } from './project-detail-page';
import { ProjectsApi } from './projects-api';
import { Project } from './projects.models';

@Component({ template: 'overview', changeDetection: ChangeDetectionStrategy.OnPush })
class Overview {}

const planning: Project = {
  id: 'p1',
  name: 'Harbour Tower',
  client: 'Acme Realty',
  location: 'Cebu',
  contractPriceMinor: 125000000,
  startDate: '2026-12-03',
  targetEndDate: null,
  status: 'Planning',
  archiveReason: null,
  archiveNote: null,
  archivedAtUtc: null,
  subProjects: [],
  version: 7,
};

type Call = (...args: unknown[]) => Observable<unknown>;
const stub = (implementation: Call): Mock<Call> => vi.fn<Call>(implementation);

const conflict = () =>
  new ApiError({
    status: 409,
    title: 'This record changed',
    detail:
      'Someone else changed this record while you were editing. Reload it to see the latest version, then try again.',
    code: 'concurrency_conflict',
  });

interface Options {
  readonly project?: Project;
  readonly permissions?: readonly string[];
  readonly get?: Mock<Call>;
  readonly transition?: Mock<Call>;
  readonly archive?: Mock<Call>;
}

async function open(options: Options = {}) {
  const get = options.get ?? stub(() => of(options.project ?? planning));
  const transition = options.transition ?? stub(() => of(planning));
  const archive = options.archive ?? stub(() => of(planning));
  TestBed.configureTestingModule({
    providers: [
      provideRouter(
        [
          {
            path: 'projects/:id',
            component: ProjectDetailPage,
            children: [{ path: '', component: Overview }],
          },
          { path: 'projects', component: Overview },
        ],
        withComponentInputBinding(),
      ),
      provideHttpClient(),
      provideHttpClientTesting(),
      { provide: ProjectsApi, useValue: { get, transition, archive } },
    ],
  });
  await signInForTest(
    TestBed.inject(SessionState),
    TestBed.inject(HttpTestingController),
    options.permissions ? { roles: ['Custom'], permissions: options.permissions } : {},
  );
  const harness = await RouterTestingHarness.create();
  await harness.navigateByUrl('/projects/p1', ProjectDetailPage);
  await settle(harness.fixture);
  return { harness, root: harness.fixture.nativeElement as HTMLElement, get, transition, archive };
}

const actionsOf = (root: HTMLElement) => root.querySelector<HTMLElement>('.summary__actions');
const openMore = async (harness: RouterTestingHarness, root: HTMLElement) => {
  buttonNamed(root, 'More').click();
  await settle(harness.fixture);
};

describe('ProjectDetailPage lifecycle actions', () => {
  it('leads with the one next step and folds the rest under More', async () => {
    const { harness, root } = await open();

    expect(text(root, '.summary__info')).toContain('Planning');
    expect(text(root, '.summary__info')).toContain('Acme Realty · Cebu');
    expect(texts(actionsOf(root)!, '.btn')).toEqual(['Start project', 'More']);

    await openMore(harness, root);
    expect(texts(root, '.menu__panel .menu__item')).toEqual(['Archive project']);
  });

  it('starts the project with the version it loaded, then offers the next step', async () => {
    const transition = stub(() => of({ ...planning, status: 'Active' as const, version: 8 }));
    const { harness, root } = await open({ transition });

    buttonNamed(root, 'Start project').click();
    await settle(harness.fixture);

    expect(transition).toHaveBeenCalledWith('p1', 'start', 7);
    expect(text(root, '.summary__info')).toContain('Active');
    expect(texts(actionsOf(root)!, '.btn')).toEqual(['Mark completed', 'More']);
    await openMore(harness, root);
    expect(texts(root, '.menu__panel .menu__item')).toEqual(['Put on hold', 'Archive project']);
  });

  it('says someone else changed it, and Reload fetches the latest version', async () => {
    const get = stub(() =>
      of({ ...planning, name: 'Harbour Tower (renamed)', version: 9 }),
    ).mockReturnValueOnce(of(planning));
    const transition = stub(() => throwError(() => conflict()));
    const { harness, root } = await open({ get, transition });

    buttonNamed(root, 'Start project').click();
    await settle(harness.fixture);
    expect(text(root, 'app-api-error-alert')).toContain(
      'Someone else changed this record while you were editing.',
    );

    buttonNamed(root, 'Reload project').click();
    await settle(harness.fixture);

    expect(get).toHaveBeenCalledTimes(2);
    expect(root.querySelector('app-api-error-alert .alert')).toBeNull();
    transition.mockReturnValue(of({ ...planning, status: 'Active' as const, version: 10 }));
    buttonNamed(root, 'Start project').click();
    await settle(harness.fixture);
    expect(transition.mock.calls.at(-1)).toEqual(['p1', 'start', 9]);
  });

  it('shows a refusal in the API words, without a status code', async () => {
    const transition = stub(() =>
      throwError(
        () =>
          new ApiError({
            status: 409,
            title: "That can't be done right now",
            detail: 'Only a project that is still being planned can be started.',
            code: 'rule_violation',
          }),
      ),
    );
    const { harness, root } = await open({ transition });

    buttonNamed(root, 'Start project').click();
    await settle(harness.fixture);

    expect(text(root, 'app-api-error-alert')).toContain(
      'Only a project that is still being planned can be started.',
    );
    expect(text(root, 'app-api-error-alert')).not.toMatch(/409|rule_violation/);
  });

  it('offers no actions to someone who cannot manage projects', async () => {
    const { root } = await open({ permissions: [Permission.projectsRead] });

    expect(actionsOf(root)).toBeNull();
    expect(root.textContent).not.toContain('Start project');
  });
});

describe('ProjectDetailPage tabs', () => {
  it('links to a tab for each area the person may read, and hides the rest', async () => {
    const full = await open();
    expect(texts(full.root, '.tab')).toEqual(['Overview', 'Budget', 'Costs', 'Team', 'Stock']);
    TestBed.resetTestingModule();

    const warehouse = await open({
      permissions: [Permission.projectsRead, Permission.inventoryRead],
    });
    expect(texts(warehouse.root, '.tab')).toEqual(['Overview', 'Stock']);
    expect(
      Array.from(warehouse.root.querySelectorAll('.tab')).map((tab) => tab.getAttribute('href')),
    ).toEqual(['/projects/p1', '/projects/p1/stock']);
  });

  it('shows no tab row when there is only the overview', async () => {
    const { root } = await open({ permissions: [Permission.projectsRead] });

    expect(root.querySelector('app-tab-nav')).toBeNull();
  });

  it('marks the open tab as the current page', async () => {
    const { root } = await open();

    expect(root.querySelector('.tab--active')?.textContent?.trim()).toBe('Overview');
    expect(root.querySelector('.tab--active')?.getAttribute('aria-current')).toBe('page');
  });
});

describe('ProjectDetailPage archived project', () => {
  const archived: Project = {
    ...planning,
    status: 'Archived',
    archiveReason: 'Finished',
    archivedAtUtc: '2026-12-31T00:00:00Z',
    version: 12,
  };

  it('says it is read-only and offers to restore it', async () => {
    const { harness, root } = await open({ project: archived });

    expect(text(root, 'app-alert')).toContain('This project is archived');
    expect(text(root, 'app-alert')).toContain(
      'It is read-only (finished project). Restore it to make changes.',
    );
    expect(texts(actionsOf(root)!, '.btn')).toEqual(['Restore project']);
    expect(root.querySelector('app-menu')).toBeNull();
    expect(harness.fixture.componentInstance).toBeTruthy();
  });

  it('gives the other reason in the banner', async () => {
    const { root } = await open({
      project: { ...archived, archiveReason: 'Other', archiveNote: 'Client cancelled' },
    });

    expect(text(root, 'app-alert')).toContain('It is read-only (Client cancelled).');
  });
});

describe('ProjectDetailPage archive dialog', () => {
  async function openArchive() {
    const archive = stub(() =>
      of({
        ...planning,
        status: 'Archived' as const,
        archiveReason: 'Finished' as const,
        version: 8,
      }),
    );
    const page = await open({ archive });
    await openMore(page.harness, page.root);
    buttonNamed(page.root, 'Archive project').click();
    await settle(page.harness.fixture);
    return { ...page, archive, dialog: page.root.querySelector<HTMLDialogElement>('dialog')! };
  }

  const submit = async (page: Awaited<ReturnType<typeof openArchive>>) => {
    buttonNamed(page.dialog, 'Archive project').click();
    await settle(page.harness.fixture);
  };

  it('asks for a reason and starts with none chosen', async () => {
    const page = await openArchive();

    expect(page.dialog.hasAttribute('open')).toBe(true);
    expect(text(page.dialog, 'h2')).toBe('Archive this project?');
    expect(texts(page.dialog, '.choice__label')).toEqual(['Finished project', 'Other']);
    expect(
      page.dialog.querySelectorAll<HTMLInputElement>('input[type="radio"]:checked'),
    ).toHaveLength(0);
    expect(page.dialog.querySelector('textarea')).toBeNull();
  });

  it('will not archive until a reason is chosen', async () => {
    const page = await openArchive();

    await submit(page);

    expect(text(page.dialog, '.field__error')).toBe('Choose why the project is being archived.');
    expect(page.archive).not.toHaveBeenCalled();
  });

  it('needs a note when the reason is "Other"', async () => {
    const page = await openArchive();

    page.dialog.querySelector<HTMLInputElement>('input[value="Other"]')!.click();
    await settle(page.harness.fixture);
    expect(page.dialog.querySelector('textarea')).not.toBeNull();

    await submit(page);
    expect(text(page.dialog, '.field__error')).toBe('Say why the project is being archived.');
    expect(page.archive).not.toHaveBeenCalled();
  });

  it('archives with the version it loaded, closes, and shows the read-only banner', async () => {
    const page = await openArchive();

    page.dialog.querySelector<HTMLInputElement>('input[value="Finished"]')!.click();
    await settle(page.harness.fixture);
    await submit(page);

    expect(page.archive).toHaveBeenCalledWith('p1', { reason: 'Finished', note: null, version: 7 });
    expect(page.root.querySelector('dialog')).toBeNull();
    expect(text(page.root, 'app-alert')).toContain('This project is archived');
  });

  it('sends the note with "Other"', async () => {
    const page = await openArchive();

    page.dialog.querySelector<HTMLInputElement>('input[value="Other"]')!.click();
    await settle(page.harness.fixture);
    const note = page.dialog.querySelector<HTMLTextAreaElement>('textarea')!;
    note.value = '  Client cancelled  ';
    note.dispatchEvent(new Event('input', { bubbles: true }));
    await submit(page);

    expect(page.archive).toHaveBeenCalledWith('p1', {
      reason: 'Other',
      note: 'Client cancelled',
      version: 7,
    });
  });

  it('returns focus to where it came from when dismissed', async () => {
    const page = await openArchive();

    buttonNamed(page.dialog, 'Cancel').click();
    await settle(page.harness.fixture);

    expect(page.root.querySelector('dialog')).toBeNull();
    expect(document.activeElement?.textContent).toContain('More');
  });
});

describe('ProjectDetailPage when the project cannot be shown', () => {
  it('explains a missing project and offers the way back, without a pointless retry', async () => {
    const get = stub(() =>
      throwError(
        () =>
          new ApiError({
            status: 404,
            title: 'Not found',
            detail: "We couldn't find that project. It may have been removed.",
            code: 'not_found',
          }),
      ),
    );
    const { root } = await open({ get });

    expect(text(root, 'app-api-error-alert')).toContain(
      "We couldn't find that project. It may have been removed.",
    );
    expect(buttonNamed(root, 'Back to projects').getAttribute('href')).toBe('/projects');
    expect(root.textContent).not.toContain('Try again');
  });

  it('offers to try again after a server failure', async () => {
    const get = stub(() =>
      throwError(
        () =>
          new ApiError({
            status: 500,
            title: 'Something went wrong',
            detail: "We couldn't finish that. Try again in a moment.",
          }),
      ),
    );
    const { root } = await open({ get });

    expect(buttonNamed(root, 'Try again')).toBeTruthy();
  });
});
