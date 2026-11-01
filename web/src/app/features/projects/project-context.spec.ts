import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Observable, of, throwError } from 'rxjs';
import type { Mock } from 'vitest';
import { ApiError } from '../../core/api/api-error';
import { Permission } from '../../core/session/permissions';
import { SessionState } from '../../core/session/session-state';
import { signInForTest } from '../../testing/session-fixture';
import { ProjectContext } from './project-context';
import { ProjectsApi } from './projects-api';
import { Project } from './projects.models';

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

async function setup(get: Mock<Call>, permissions?: readonly string[]) {
  TestBed.configureTestingModule({
    providers: [
      provideHttpClient(),
      provideHttpClientTesting(),
      ProjectContext,
      { provide: ProjectsApi, useValue: { get } },
    ],
  });
  await signInForTest(
    TestBed.inject(SessionState),
    TestBed.inject(HttpTestingController),
    permissions ? { roles: ['Custom'], permissions } : {},
  );
  return TestBed.inject(ProjectContext);
}

describe('ProjectContext', () => {
  it('starts loading and refuses to hand out a project that has not arrived', async () => {
    const context = await setup(stub(() => of(project)));

    expect(context.status()).toBe('loading');
    expect(() => context.project()).toThrow(/has not loaded yet/);
    expect(context.name()).toBe('');
    expect(context.subProjects()).toEqual([]);
  });

  it('loads a project and tells the tabs its id, name, state and sub-projects', async () => {
    const get = stub(() => of(project));
    const context = await setup(get);

    await context.load('p1');

    expect(get).toHaveBeenCalledWith('p1');
    expect(context.status()).toBe('ready');
    expect(context.project().id).toBe('p1');
    expect(context.projectId()).toBe('p1');
    expect(context.name()).toBe('Harbour Tower');
    expect(context.isArchived()).toBe(false);
    expect(context.subProjects()).toHaveLength(2);
  });

  it('lists only the sub-projects that can still receive records', async () => {
    const context = await setup(stub(() => of(project)));

    await context.load('p1');

    expect(context.activeSubProjects().map((sub) => sub.name)).toEqual(['Block A']);
  });

  it('switches writing off for an archived project, even for someone who may manage projects', async () => {
    const context = await setup(stub(() => of({ ...project, status: 'Archived' as const })));

    await context.load('p1');

    expect(context.isArchived()).toBe(true);
    expect(context.canManage()).toBe(false);
  });

  it('lets only people with the permission manage an active project', async () => {
    const manager = await setup(stub(() => of(project)));
    await manager.load('p1');
    expect(manager.canManage()).toBe(true);
    TestBed.resetTestingModule();

    const reader = await setup(
      stub(() => of(project)),
      [Permission.projectsRead],
    );
    await reader.load('p1');
    expect(reader.canManage()).toBe(false);
  });

  it('takes the project a change returned', async () => {
    const context = await setup(stub(() => of(project)));
    await context.load('p1');

    context.replace({ ...project, status: 'OnHold', version: 5 });

    expect(context.project().version).toBe(5);
    expect(context.project().status).toBe('OnHold');
  });

  it('keeps the project on screen while it reloads, then shows the new one', async () => {
    const get = stub(() =>
      of({ ...project, name: 'Harbour Tower (renamed)', version: 9 }),
    ).mockReturnValueOnce(of(project));
    const context = await setup(get);
    await context.load('p1');

    await context.reload();

    expect(get).toHaveBeenCalledTimes(2);
    expect(context.name()).toBe('Harbour Tower (renamed)');
    expect(context.refreshing()).toBe(false);
    expect(context.status()).toBe('ready');
  });

  it('keeps what it has when a reload fails, rather than blanking the page', async () => {
    const get = stub(() =>
      throwError(() => new ApiError({ status: 500, title: 't', detail: 'd' })),
    ).mockReturnValueOnce(of(project));
    const context = await setup(get);
    await context.load('p1');

    await context.reload();

    expect(context.status()).toBe('ready');
    expect(context.name()).toBe('Harbour Tower');
  });

  it('reports a project that cannot be loaded, with the safe error', async () => {
    const context = await setup(
      stub(() =>
        throwError(
          () =>
            new ApiError({
              status: 404,
              title: 'Not found',
              detail: "We couldn't find that project. It may have been removed.",
              code: 'not_found',
            }),
        ),
      ),
    );

    await context.load('missing');

    expect(context.status()).toBe('error');
    expect(context.error()?.isNotFound).toBe(true);
    expect(context.projectId()).toBe('missing');
  });

  it('starts again when the page moves to another project', async () => {
    const get = stub((id) =>
      of({ ...project, id: String(id), name: id === 'p2' ? 'Riverside Clinic' : 'Harbour Tower' }),
    );
    const context = await setup(get);
    await context.load('p1');

    const next = context.load('p2');
    expect(context.status()).toBe('loading');
    expect(context.name()).toBe('');
    await next;

    expect(context.name()).toBe('Riverside Clinic');
  });
});
