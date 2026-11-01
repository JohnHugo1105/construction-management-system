import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ChangeDetectionStrategy, Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { Observable, of, throwError } from 'rxjs';
import { ApiError } from '../../core/api/api-error';
import { PagedResult } from '../../core/api/paged-result';
import { SessionState } from '../../core/session/session-state';
import { Viewport } from '../../shared/ui/viewport/viewport';
import { buttonNamed, settle, text, texts, type } from '../../testing/dom';
import { VIEWER_PERMISSIONS, pageOf, signInForTest } from '../../testing/session-fixture';
import { ProjectListPage } from './project-list-page';
import { ProjectsApi } from './projects-api';
import { ProjectListItem, ProjectListParams } from './projects.models';

@Component({ template: '', changeDetection: ChangeDetectionStrategy.OnPush })
class Blank {}

const harbour: ProjectListItem = {
  id: 'p1',
  name: 'Harbour Tower',
  client: 'Acme Realty',
  location: 'Cebu',
  status: 'Active',
  startDate: '2026-12-03',
  targetEndDate: '2027-03-01',
  contractPriceMinor: 125000000,
  subProjectCount: 2,
  version: 1,
};

const clinic: ProjectListItem = {
  id: 'p2',
  name: 'Riverside Clinic',
  client: null,
  location: null,
  status: 'Planning',
  startDate: '2026-11-02',
  targetEndDate: null,
  contractPriceMinor: null,
  subProjectCount: 0,
  version: 3,
};

interface Options {
  readonly url?: string;
  readonly result?: PagedResult<ProjectListItem>;
  readonly list?: (params: ProjectListParams) => Observable<PagedResult<ProjectListItem>>;
  readonly tablet?: boolean;
  readonly viewer?: boolean;
  /** Someone who cannot see costs (a warehouse keeper): the contract price is not theirs to see. */
  readonly withoutMoney?: boolean;
}

async function open(options: Options = {}) {
  const list = vi.fn(options.list ?? (() => of(options.result ?? pageOf([harbour, clinic]))));
  TestBed.configureTestingModule({
    providers: [
      provideRouter([
        { path: 'projects', component: ProjectListPage },
        { path: 'projects/new', component: Blank },
        { path: 'projects/:id', component: Blank },
      ]),
      provideHttpClient(),
      provideHttpClientTesting(),
      { provide: ProjectsApi, useValue: { list } },
      {
        provide: Viewport,
        useValue: { isTablet: signal(options.tablet ?? true), isDesktop: signal(true) },
      },
    ],
  });
  await signInForTest(
    TestBed.inject(SessionState),
    TestBed.inject(HttpTestingController),
    options.withoutMoney
      ? { roles: ['WarehouseKeeper'], permissions: ['projects.read', 'inventory.read'] }
      : options.viewer
        ? { roles: ['Viewer'], permissions: VIEWER_PERMISSIONS }
        : {},
  );
  const harness = await RouterTestingHarness.create();
  await harness.navigateByUrl(options.url ?? '/projects', ProjectListPage);
  await settle(harness.fixture);
  return {
    harness,
    root: harness.fixture.nativeElement as HTMLElement,
    list,
    router: TestBed.inject(Router),
  };
}

const requests = (list: ReturnType<typeof vi.fn>) =>
  list.mock.calls.map(([params]) => params as ProjectListParams);
const paramsOf = (url: string) =>
  Object.fromEntries(new URL(url, 'http://app.test').searchParams.entries());

describe('ProjectListPage for someone who cannot see costs', () => {
  it('has no contract price column on a wide screen', async () => {
    const { root } = await open({ withoutMoney: true });

    expect(texts(root, 'thead th')).toEqual(['Project', 'Status', 'Dates', 'Sub-projects']);
    expect(texts(root.querySelectorAll('tbody tr')[0]!, 'th, td')).not.toContain('Not set');
    expect(root.textContent).not.toContain('Contract price');
  });

  it('has no price on the phone cards either', async () => {
    const { root } = await open({ withoutMoney: true, tablet: false });

    expect(root.querySelector('.money')).toBeNull();
    expect(root.textContent).not.toContain('Not set');
  });
});

describe('ProjectListPage on a wide screen', () => {
  it('shows a row per project with the price in the company currency and dates in the house style', async () => {
    const { root } = await open();

    const rows = root.querySelectorAll('tbody tr');
    expect(rows).toHaveLength(2);
    const first = texts(rows[0]!, 'th, td');
    expect(text(rows[0]!, 'th a')).toBe('Harbour Tower');
    expect(text(rows[0]!, 'th .cell-sub')).toBe('Acme Realty · Cebu');
    expect(first[1]).toBe('Active');
    expect(first[2]).toBe('₱1,250,000.00');
    expect(first[3]).toBe('3 Dec 2026 to 1 Mar 2027');
    expect(first[4]).toBe('2');

    const second = texts(rows[1]!, 'th, td');
    expect(second[0]).toBe('Riverside Clinic');
    expect(rows[1]!.querySelector('.cell-sub')).toBeNull();
    expect(second[1]).toBe('Planning');
    expect(second[2]).toBe('Not set');
    expect(second[3]).toBe('From 2 Nov 2026');
    // A project without sub-projects shows no zero.
    expect(second[4]).toBe('');
  });

  it('links each project name to its page', async () => {
    const { root } = await open();

    expect(root.querySelector('tbody a')!.getAttribute('href')).toBe('/projects/p1');
  });

  it('labels the sideways-scrolling table as a region the keyboard can reach', async () => {
    const { root } = await open();

    const region = root.querySelector('.table-wrap')!;
    expect(region.getAttribute('role')).toBe('region');
    expect(region.getAttribute('aria-label')).toBe('Projects');
    expect(region.getAttribute('tabindex')).toBe('0');
  });

  it('asks for the first page of everything except archived projects', async () => {
    const { list } = await open();

    expect(requests(list)).toEqual([{ search: '', status: '', page: 1, pageSize: 25 }]);
  });
});

describe('ProjectListPage on a phone', () => {
  it('shows cards instead of a table, and mentions sub-projects only when there are some', async () => {
    const { root } = await open({ tablet: false });

    expect(root.querySelector('table')).toBeNull();
    const cards = root.querySelectorAll('.row-card');
    expect(cards).toHaveLength(2);
    expect(cards[0]!.textContent).toContain('Harbour Tower');
    expect(cards[0]!.textContent).toContain('₱1,250,000.00');
    expect(cards[0]!.textContent).toContain('2 sub-projects');
    expect(cards[1]!.textContent).not.toContain('sub-project');
  });
});

describe('ProjectListPage empty states', () => {
  it('invites a manager to add the first project', async () => {
    const { root } = await open({ result: pageOf([]) });

    expect(text(root, 'app-empty-state')).toContain('No projects yet');
    expect(
      buttonNamed(root.querySelector('app-empty-state')!, 'New project').getAttribute('href'),
    ).toBe('/projects/new');
  });

  it('does not offer "New project" to someone who cannot add one', async () => {
    const { root } = await open({ result: pageOf([]), viewer: true });

    expect(text(root, 'app-empty-state')).toContain('A project manager can add the first project.');
    expect(root.textContent).not.toContain('New project');
  });

  it('says nothing matches a search, and clearing it goes back to the full list', async () => {
    const { harness, root, router } = await open({
      url: '/projects?search=zzz',
      result: pageOf([]),
    });

    expect(text(root, 'app-empty-state')).toContain('No projects match');
    buttonNamed(root, 'Show all projects').click();
    await settle(harness.fixture);

    expect(router.url).toBe('/projects');
  });
});

describe('ProjectListPage paging, filters and search', () => {
  it('pages on the server: Next asks the API for page 2 and writes it to the address', async () => {
    const { harness, root, list, router } = await open({ result: pageOf([harbour, clinic], 60) });
    expect(text(root, '.pager__summary')).toBe('Showing 1 to 25 of 60 projects');

    buttonNamed(root, 'Next').click();
    await settle(harness.fixture);

    expect(paramsOf(router.url)).toEqual({ page: '2' });
    expect(requests(list).at(-1)).toMatchObject({ page: 2, pageSize: 25 });
  });

  it('shows no pager when everything fits on one page', async () => {
    const { root } = await open();

    expect(root.querySelector('app-pager nav')).toBeNull();
  });

  it('filters by status from a chip, starting again at page 1', async () => {
    const { harness, root, list, router } = await open({ url: '/projects?page=3' });

    buttonNamed(root, 'Active').click();
    await settle(harness.fixture);

    expect(paramsOf(router.url)).toEqual({ status: 'Active' });
    expect(requests(list).at(-1)).toMatchObject({ status: 'Active', page: 1 });
    expect(buttonNamed(root, 'Active').getAttribute('aria-pressed')).toBe('true');
    expect(buttonNamed(root, 'All').getAttribute('aria-pressed')).toBe('false');
  });

  it('sends Archived as a status of its own, since "All" leaves archived projects out', async () => {
    const { harness, root, list } = await open();

    buttonNamed(root, 'Archived').click();
    await settle(harness.fixture);

    expect(requests(list).at(-1)).toMatchObject({ status: 'Archived' });
  });

  it('explains where archived projects went while the list is on "All"', async () => {
    const { root } = await open();

    expect(root.textContent).toContain(
      'Archived projects are not listed here. Choose Archived to see them.',
    );
  });

  it('waits for a pause in typing before searching', async () => {
    const { harness, root, list, router } = await open();
    const before = requests(list).length;

    vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout'] });
    try {
      type(root, 'input[type="search"]', 'tower');
      vi.advanceTimersByTime(299);
      expect(requests(list)).toHaveLength(before);
      vi.advanceTimersByTime(1);
    } finally {
      vi.useRealTimers();
    }
    await settle(harness.fixture);

    expect(requests(list).at(-1)).toMatchObject({ search: 'tower', page: 1 });
    expect(paramsOf(router.url)).toEqual({ search: 'tower' });
  });

  it('reads the filters from a shared link', async () => {
    const { root, list } = await open({ url: '/projects?search=tower&status=OnHold&page=2' });

    expect(requests(list)[0]).toMatchObject({ search: 'tower', status: 'OnHold', page: 2 });
    expect((root.querySelector('input[type="search"]') as HTMLInputElement).value).toBe('tower');
    expect(buttonNamed(root, 'On hold').getAttribute('aria-pressed')).toBe('true');
  });
});

describe('ProjectListPage when the list cannot load', () => {
  it('shows a calm message with a reference, and Try again asks again', async () => {
    let fail = true;
    const { harness, root, list } = await open({
      list: () =>
        fail
          ? throwError(
              () =>
                new ApiError({
                  status: 500,
                  title: 'Something went wrong',
                  detail: "We couldn't finish that. Try again in a moment.",
                  traceId: 'ref-77',
                }),
            )
          : of(pageOf([harbour])),
    });

    expect(text(root, 'app-api-error-alert')).toContain(
      "We couldn't finish that. Try again in a moment.",
    );
    expect(text(root, 'app-api-error-alert')).toContain('Reference: ref-77');
    expect(root.querySelector('table')).toBeNull();

    fail = false;
    buttonNamed(root, 'Try again').click();
    await settle(harness.fixture);

    expect(list.mock.calls.length).toBeGreaterThan(1);
    expect(root.querySelectorAll('tbody tr')).toHaveLength(1);
    expect(root.querySelector('app-api-error-alert .alert')).toBeNull();
  });
});
