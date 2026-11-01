import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ChangeDetectionStrategy, Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { Observable, of, throwError } from 'rxjs';
import type { Mock } from 'vitest';
import { PagedResult } from '../../core/api/paged-result';
import { SessionState } from '../../core/session/session-state';
import { Viewport } from '../../shared/ui/viewport/viewport';
import { buttonNamed, settle, text, texts } from '../../testing/dom';
import { VIEWER_PERMISSIONS, pageOf, signInForTest } from '../../testing/session-fixture';
import { HR_PERMISSIONS, annex, listItem, riverside, validationError } from './workforce.fixtures';
import { WorkforceApi } from './workforce-api';
import { WorkerListItem, WorkerListParams } from './workforce.models';
import { WorkersPage } from './workers-page';

@Component({ template: '', changeDetection: ChangeDetectionStrategy.OnPush })
class Blank {}

type Call = (...args: unknown[]) => Observable<unknown>;
const stub = (implementation: Call): Mock<Call> => vi.fn<Call>(implementation);

const ana = listItem({
  id: 'w5',
  employeeNumber: 'W-005',
  fullName: 'Ana Bautista',
  designation: 'Helper',
});
const juan = listItem({ id: 'w1' });
const lito = listItem({
  id: 'w9',
  employeeNumber: 'W-100',
  fullName: 'Lito Navarro',
  designation: 'Mason',
  status: 'OnLeave',
  currentProjectId: null,
  currentProjectName: null,
});

interface Options {
  readonly url?: string;
  readonly items?: readonly WorkerListItem[];
  readonly permissions?: readonly string[];
  readonly tablet?: boolean;
  readonly assignMany?: Mock<Call>;
}

async function open(options: Options = {}) {
  const list = vi.fn((params: WorkerListParams): Observable<PagedResult<WorkerListItem>> => {
    void params;
    return of(pageOf(options.items ?? [ana, juan, lito]));
  });
  const assignMany = options.assignMany ?? stub(() => of([]));
  TestBed.configureTestingModule({
    providers: [
      provideRouter([
        { path: 'workers', component: WorkersPage },
        { path: 'workers/new', component: Blank },
        { path: 'workers/:id', component: Blank },
      ]),
      provideHttpClient(),
      provideHttpClientTesting(),
      {
        provide: WorkforceApi,
        useValue: { list, assignMany, projectChoices: () => of([riverside, annex]) },
      },
      {
        provide: Viewport,
        useValue: { isTablet: signal(options.tablet ?? true), isDesktop: signal(true) },
      },
    ],
  });
  await signInForTest(
    TestBed.inject(SessionState),
    TestBed.inject(HttpTestingController),
    options.permissions ? { roles: ['Custom'], permissions: options.permissions } : {},
  );
  const harness = await RouterTestingHarness.create();
  await harness.navigateByUrl(options.url ?? '/workers', WorkersPage);
  await settle(harness.fixture);
  return {
    harness,
    root: harness.fixture.nativeElement as HTMLElement,
    list,
    assignMany,
    router: TestBed.inject(Router),
  };
}

const tick = (root: HTMLElement, name: string) => {
  const label = Array.from(root.querySelectorAll<HTMLLabelElement>('label.pick__label')).find(
    (item) => item.textContent?.includes(name),
  )!;
  label.querySelector<HTMLInputElement>('input')!.click();
};

describe('WorkersPage', () => {
  it('shows who each worker is, where they are posted and their state, and no pay', async () => {
    const { root } = await open({ permissions: HR_PERMISSIONS });

    const rows = root.querySelectorAll('tbody tr');
    expect(rows).toHaveLength(3);
    expect(text(rows[0]!, 'th a')).toBe('Ana Bautista');
    expect(text(rows[0]!, 'th .cell-sub')).toBe('W-005');
    expect(texts(rows[0]!, 'td')).toContain('Helper');
    expect(texts(rows[0]!, 'td')).toContain('Riverside Residences');
    // A worker with no posting says so in words.
    expect(texts(rows[2]!, 'td')).toContain('Not posted to a project');
    expect(text(rows[2]!, 'app-status-chip')).toBe('On leave');
    // Pay never appears in the list, whoever is looking.
    expect(root.textContent).not.toMatch(/₱|per day|per hour|rate/i);
  });

  it('shows cards with the same facts on a phone', async () => {
    const { root } = await open({ permissions: HR_PERMISSIONS, tablet: false });

    expect(root.querySelector('table')).toBeNull();
    const cards = root.querySelectorAll('.row-card');
    expect(cards).toHaveLength(3);
    expect(text(cards[1]!, '.row-card__title')).toBe('Juan Dela Cruz');
    expect(text(cards[1]!, 'p')).toBe('W-001 · Foreman');
  });

  it('gives a person who can only look no tick boxes and no way to hire', async () => {
    const { root } = await open({ permissions: VIEWER_PERMISSIONS });

    expect(root.querySelectorAll('tbody tr')).toHaveLength(3);
    expect(root.querySelectorAll('input[type="checkbox"]')).toHaveLength(0);
    expect(
      Array.from(root.querySelectorAll('a')).some((link) =>
        link.textContent?.includes('Hire a worker'),
      ),
    ).toBe(false);
  });

  it('offers hiring to a person who manages workers, and crew posting only once someone is ticked', async () => {
    const { root, harness } = await open({ permissions: HR_PERMISSIONS });

    expect(buttonNamed(root, 'Hire a worker').getAttribute('href')).toBe('/workers/new');
    expect(root.querySelector('.selection-bar')).toBeNull();

    tick(root, 'Ana Bautista');
    await settle(harness.fixture);
    expect(text(root, '.selection-bar__count')).toBe('1 worker selected');
    expect(buttonNamed(root, 'Assign selected to a project')).toBeTruthy();
  });

  it('asks the server for the chosen status, project and page, and keeps them in the address', async () => {
    const { list, harness, router } = await open({
      url: '/workers?status=OnLeave&project=p1&search=ana&page=2',
    });

    expect(list).toHaveBeenCalledWith({
      search: 'ana',
      status: 'OnLeave',
      projectId: 'p1',
      page: 2,
      pageSize: 25,
    });

    // Choosing another status chip starts again at page 1 and shows in the address.
    buttonNamed(harness.fixture.nativeElement as HTMLElement, 'Suspended').click();
    await settle(harness.fixture);
    expect(router.url).toBe('/workers?status=Suspended&project=p1&search=ana');
  });

  it('shows nothing to search yet, and the one next step, when there are no workers at all', async () => {
    const { root } = await open({ items: [], permissions: HR_PERMISSIONS });

    expect(root.querySelector('app-search-box')).toBeNull();
    expect(root.querySelector('.filter-chips')).toBeNull();
    expect(text(root, 'app-empty-state')).toContain('No workers yet');
    expect(buttonNamed(root, 'Hire a worker').getAttribute('href')).toBe('/workers/new');
    // The next step is offered once, not twice.
    expect(texts(root, 'a').filter((label) => label === 'Hire a worker')).toHaveLength(1);
  });

  it('offers to clear the filters when none match', async () => {
    const { root } = await open({ items: [], url: '/workers?status=Retired' });

    expect(text(root, 'app-empty-state')).toContain('No workers match');
    expect(buttonNamed(root, 'Show all workers')).toBeTruthy();
  });
});

describe('WorkersPage crew posting', () => {
  it('names how many are selected and opens the dialog for the crew', async () => {
    const { root, harness } = await open({ permissions: HR_PERMISSIONS });

    tick(root, 'Ana Bautista');
    tick(root, 'Juan Dela Cruz');
    await settle(harness.fixture);

    expect(text(root, '.selection-bar__count')).toBe('2 workers selected');
    buttonNamed(root, 'Assign selected to a project').click();
    await settle(harness.fixture);

    expect(text(root, 'app-assign-dialog .dialog__title')).toBe('Assign 2 workers to a project');
  });

  it('keeps the dialog open and lists every worker the server refused, with the reason', async () => {
    const assignMany = stub(() =>
      throwError(() =>
        validationError({
          workers: [
            'Lito Navarro: Only an active worker can be assigned to a project.',
            'Juan Dela Cruz is already assigned there.',
          ],
        }),
      ),
    );
    const { root, harness } = await open({ permissions: HR_PERMISSIONS, assignMany });

    tick(root, 'Ana Bautista');
    tick(root, 'Juan Dela Cruz');
    tick(root, 'Lito Navarro');
    await settle(harness.fixture);
    buttonNamed(root, 'Assign selected to a project').click();
    await settle(harness.fixture);

    const select = root.querySelector<HTMLSelectElement>('app-assign-dialog select')!;
    select.value = 'p1';
    select.dispatchEvent(new Event('change', { bubbles: true }));
    buttonNamed(root, 'Assign workers').click();
    await settle(harness.fixture);

    // Everything that was sent is the whole crew, to the chosen project, from the date shown.
    expect(assignMany).toHaveBeenCalledTimes(1);
    const request = assignMany.mock.calls[0]![0] as {
      workerIds: string[];
      projectId: string;
      subProjectId: unknown;
    };
    expect(request.workerIds.sort()).toEqual(['w1', 'w5', 'w9']);
    expect(request.projectId).toBe('p1');
    expect(request.subProjectId).toBeNull();

    // The dialog stays open and says plainly what happened and why, one line for each refused worker.
    expect(root.querySelector('app-assign-dialog dialog[open]')).not.toBeNull();
    expect(text(root, 'app-assign-dialog .alert__title')).toBe("2 workers can't be assigned");
    expect(texts(root, 'app-assign-dialog .refusals li')).toEqual([
      'Lito Navarro: Only an active worker can be assigned to a project.',
      'Juan Dela Cruz is already assigned there.',
    ]);
    expect(text(root, 'app-assign-dialog .alert__text')).toContain('Nothing was changed.');
    // The selection is kept so the person can take the refused workers out and try again.
    expect(text(root, '.selection-bar__count')).toBe('3 workers selected');
    // The refusal list is not repeated in a second, general error.
    expect(root.querySelectorAll('app-assign-dialog app-api-error-alert .alert')).toHaveLength(0);
  });

  it('posts the crew, clears the selection and reloads the list when the server accepts', async () => {
    const assignMany = stub(() => of([]));
    const { root, harness, list } = await open({ permissions: HR_PERMISSIONS, assignMany });
    tick(root, 'Ana Bautista');
    await settle(harness.fixture);
    buttonNamed(root, 'Assign selected to a project').click();
    await settle(harness.fixture);
    const calls = list.mock.calls.length;

    const select = root.querySelector<HTMLSelectElement>('app-assign-dialog select')!;
    select.value = 'p2';
    select.dispatchEvent(new Event('change', { bubbles: true }));
    buttonNamed(root, 'Assign worker').click();
    await settle(harness.fixture);

    expect(assignMany).toHaveBeenCalledTimes(1);
    expect(root.querySelector('app-assign-dialog')).toBeNull();
    expect(root.querySelector('.selection-bar')).toBeNull();
    expect(list.mock.calls.length).toBeGreaterThan(calls);
  });

  it('asks for a project before sending anything', async () => {
    const assignMany = stub(() => of([]));
    const { root, harness } = await open({ permissions: HR_PERMISSIONS, assignMany });
    tick(root, 'Ana Bautista');
    await settle(harness.fixture);
    buttonNamed(root, 'Assign selected to a project').click();
    await settle(harness.fixture);

    buttonNamed(root, 'Assign worker').click();
    await settle(harness.fixture);

    expect(assignMany).not.toHaveBeenCalled();
    expect(text(root, 'app-assign-dialog .field__error')).toBe('Choose the project.');
  });
});
