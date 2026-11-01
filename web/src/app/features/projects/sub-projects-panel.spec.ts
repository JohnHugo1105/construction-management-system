import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { Observable, of, throwError } from 'rxjs';
import type { Mock } from 'vitest';
import { ApiError } from '../../core/api/api-error';
import { Permission } from '../../core/session/permissions';
import { SessionState } from '../../core/session/session-state';
import { buttonNamed, settle, text, texts, type } from '../../testing/dom';
import { signInForTest } from '../../testing/session-fixture';
import { ProjectContext } from './project-context';
import { ProjectsApi } from './projects-api';
import { Project } from './projects.models';
import { SubProjectsPanel } from './sub-projects-panel';

const project: Project = {
  id: 'p1',
  name: 'Harbour Tower',
  client: null,
  location: null,
  contractPriceMinor: null,
  startDate: '2026-12-03',
  targetEndDate: null,
  status: 'Active',
  archiveReason: null,
  archiveNote: null,
  archivedAtUtc: null,
  subProjects: [
    { id: 's1', name: 'Block A', startDate: '2026-12-10', status: 'Active' },
    { id: 's2', name: 'Block B', startDate: '2027-01-05', status: 'Archived' },
  ],
  version: 4,
};

type Call = (...args: unknown[]) => Observable<unknown>;
const stub = (implementation: Call): Mock<Call> => vi.fn<Call>(implementation);

async function open(
  options: {
    project?: Project;
    permissions?: readonly string[];
    api?: Record<string, Mock<Call>>;
  } = {},
) {
  const api = options.api ?? {};
  TestBed.configureTestingModule({
    providers: [
      provideRouter([]),
      provideHttpClient(),
      provideHttpClientTesting(),
      ProjectContext,
      { provide: ProjectsApi, useValue: api },
    ],
  });
  await signInForTest(
    TestBed.inject(SessionState),
    TestBed.inject(HttpTestingController),
    options.permissions ? { roles: ['Custom'], permissions: options.permissions } : {},
  );
  const context = TestBed.inject(ProjectContext);
  context.replace(options.project ?? project);
  const fixture = TestBed.createComponent(SubProjectsPanel);
  await settle(fixture);
  return { fixture, root: fixture.nativeElement as HTMLElement, context };
}

describe('SubProjectsPanel', () => {
  it('lists the sub-projects, with archived ones marked in words', async () => {
    const { root } = await open();

    expect(texts(root, '.sub-row__name')).toEqual(['Block A', 'Block B']);
    expect(texts(root, '.sub-row__info .text-muted')).toEqual([
      'Started 10 Dec 2026',
      'Started 5 Jan 2027',
    ]);
    expect(root.querySelectorAll('.sub-row app-status-chip')).toHaveLength(1);
    expect(text(root, '.sub-row--archived')).toContain('Archived');
  });

  it('says what a sub-project is for when there are none, with no counters', async () => {
    const { root } = await open({ project: { ...project, subProjects: [] } });

    expect(text(root, 'section')).toContain(
      'No sub-projects yet. Add one when you split the work into buildings or phases.',
    );
    expect(root.querySelector('.sub-list')).toBeNull();
  });

  it('gives managers the buttons, and everyone else a read-only list', async () => {
    const manager = await open();
    expect(texts(manager.root, '.btn')).toEqual([
      'Add sub-project',
      'Edit',
      'Archive',
      'Edit',
      'Restore',
    ]);
    TestBed.resetTestingModule();

    const reader = await open({ permissions: [Permission.projectsRead] });
    expect(reader.root.querySelectorAll('.btn')).toHaveLength(0);
    expect(texts(reader.root, '.sub-row__name')).toEqual(['Block A', 'Block B']);
  });

  it('hides every button while the project is archived', async () => {
    const { root } = await open({ project: { ...project, status: 'Archived' } });

    expect(root.querySelectorAll('.btn')).toHaveLength(0);
  });

  it('names each row button after its sub-project, for screen readers', async () => {
    const { root } = await open();

    expect(
      Array.from(root.querySelectorAll('.sub-row .btn')).map((button) =>
        button.getAttribute('aria-label'),
      ),
    ).toEqual(['Edit Block A', 'Archive Block A', 'Edit Block B', 'Restore Block B']);
  });

  it('adds a sub-project with the project version, and shows it', async () => {
    const addSubProject = stub(() =>
      of({
        ...project,
        version: 5,
        subProjects: [
          ...project.subProjects,
          { id: 's3', name: 'Block C', startDate: '2026-12-20', status: 'Active' as const },
        ],
      }),
    );
    const { fixture, root, context } = await open({ api: { addSubProject } });

    buttonNamed(root, 'Add sub-project').click();
    await settle(fixture);
    const dialog = root.querySelector<HTMLDialogElement>('dialog')!;
    expect(text(dialog, 'h2')).toBe('Add sub-project');
    type(dialog, '[formControlName="name"]', '  Block C  ');
    type(dialog, '[formControlName="startDate"]', '2026-12-20');
    buttonNamed(dialog, 'Add sub-project').click();
    await settle(fixture);

    expect(addSubProject).toHaveBeenCalledWith(
      'p1',
      { name: 'Block C', startDate: '2026-12-20' },
      4,
    );
    expect(root.querySelector('dialog')).toBeNull();
    expect(context.project().version).toBe(5);
    expect(texts(root, '.sub-row__name')).toContain('Block C');
  });

  it('shows a name that is already used under the name field, in the API words', async () => {
    const addSubProject = stub(() =>
      throwError(
        () =>
          new ApiError({
            status: 409,
            title: "That can't be done right now",
            detail: 'This project already has a sub-project named "Block A".',
            code: 'rule_violation',
          }),
      ),
    );
    const { fixture, root } = await open({ api: { addSubProject } });

    buttonNamed(root, 'Add sub-project').click();
    await settle(fixture);
    const dialog = root.querySelector<HTMLDialogElement>('dialog')!;
    type(dialog, '[formControlName="name"]', 'Block A');
    buttonNamed(dialog, 'Add sub-project').click();
    await settle(fixture);

    expect(text(dialog, '.field__error')).toBe(
      'This project already has a sub-project named "Block A".',
    );
    expect(dialog.querySelector('[formControlName="name"]')!.getAttribute('aria-invalid')).toBe(
      'true',
    );
    expect(dialog.hasAttribute('open')).toBe(true);
    expect((dialog.querySelector('[formControlName="name"]') as HTMLInputElement).value).toBe(
      'Block A',
    );
  });

  it('edits a sub-project with its name and date filled in', async () => {
    const updateSubProject = stub(() => of({ ...project, version: 5 }));
    const { fixture, root } = await open({ api: { updateSubProject } });

    root.querySelector<HTMLElement>('[aria-label="Edit Block A"]')!.click();
    await settle(fixture);
    const dialog = root.querySelector<HTMLDialogElement>('dialog')!;
    expect(text(dialog, 'h2')).toBe('Edit sub-project');
    expect((dialog.querySelector('[formControlName="name"]') as HTMLInputElement).value).toBe(
      'Block A',
    );
    expect((dialog.querySelector('[formControlName="startDate"]') as HTMLInputElement).value).toBe(
      '2026-12-10',
    );

    type(dialog, '[formControlName="name"]', 'Block A (east)');
    buttonNamed(dialog, 'Save sub-project').click();
    await settle(fixture);

    expect(updateSubProject).toHaveBeenCalledWith(
      'p1',
      's1',
      { name: 'Block A (east)', startDate: '2026-12-10' },
      4,
    );
  });

  it('asks before archiving, then archives with the version', async () => {
    const archiveSubProject = stub(() => of({ ...project, version: 5 }));
    const { fixture, root } = await open({ api: { archiveSubProject } });

    root.querySelector<HTMLElement>('[aria-label="Archive Block A"]')!.click();
    await settle(fixture);
    const dialog = root.querySelector<HTMLDialogElement>('dialog')!;
    expect(text(dialog, 'h2')).toBe('Archive this sub-project?');
    expect(text(dialog, '.confirm-message')).toBe(
      'Block A stays on record, but cannot receive new records until you restore it.',
    );
    expect(archiveSubProject).not.toHaveBeenCalled();

    buttonNamed(dialog, 'Archive sub-project').click();
    await settle(fixture);

    expect(archiveSubProject).toHaveBeenCalledWith('p1', 's1', 4);
    expect(root.querySelector('dialog')).toBeNull();
  });

  it('restores an archived sub-project straight away', async () => {
    const restoreSubProject = stub(() => of({ ...project, version: 5 }));
    const { fixture, root } = await open({ api: { restoreSubProject } });

    root.querySelector<HTMLElement>('[aria-label="Restore Block B"]')!.click();
    await settle(fixture);

    expect(restoreSubProject).toHaveBeenCalledWith('p1', 's2', 4);
  });
});
