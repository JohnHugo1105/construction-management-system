import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ChangeDetectionStrategy, Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { Observable, of, throwError } from 'rxjs';
import type { Mock } from 'vitest';
import { ApiError } from '../../core/api/api-error';
import { Permission } from '../../core/session/permissions';
import { SessionState } from '../../core/session/session-state';
import { Viewport } from '../../shared/ui/viewport/viewport';
import { buttonNamed, settle, text, texts } from '../../testing/dom';
import { signInForTest } from '../../testing/session-fixture';
import { DashboardApi } from './dashboard-api';
import { DashboardPage } from './dashboard-page';
import { Dashboard, DashboardRow } from './dashboard.models';

@Component({ template: '', changeDetection: ChangeDetectionStrategy.OnPush })
class Blank {}

function row(
  id: string,
  name: string,
  health: DashboardRow['health'],
  overrides: Partial<DashboardRow> = {},
): DashboardRow {
  return {
    id,
    name,
    status: 'Active',
    contractPriceMinor: 4850000000,
    budgetMinor: 1000000,
    actualMinor: 400000,
    remainingMinor: 600000,
    usedBps: 4000,
    health,
    marginAfterActualMinor: 4849600000,
    budgetMarginMinor: 4849000000,
    ...overrides,
  };
}

const over = row('p-over', 'Bridge Repair', 'OverBudget', {
  budgetMinor: 1000000,
  actualMinor: 1180075,
  remainingMinor: -180075,
  usedBps: 11801,
});
const near = row('p-near', 'Clinic', 'NearLimit', {
  usedBps: 9400,
  actualMinor: 940000,
  remainingMinor: 60000,
});
const fine = row('p-fine', 'Riverside Residences', 'OnTrack');
const idle = row('p-idle', 'Harbor Warehouse', 'NoActivity', {
  status: 'Planning',
  budgetMinor: 0,
  actualMinor: 0,
  remainingMinor: 0,
  usedBps: null,
  marginAfterActualMinor: 1200000000,
  contractPriceMinor: 1200000000,
});

function dashboard(projects: DashboardRow[], overrides: Partial<Dashboard> = {}): Dashboard {
  return {
    basis: 'IncludingVat',
    projects,
    totals: {
      projectCount: projects.length,
      contractPriceMinor: 6675000000,
      budgetMinor: 1791000000,
      actualMinor: 721000000,
      remainingMinor: 1070000000,
      usedBps: 4026,
      health: 'OnTrack',
      marginAfterActualMinor: 5954000000,
      budgetMarginMinor: 4884000000,
    },
    ...overrides,
  };
}

type Call = (...args: unknown[]) => Observable<unknown>;
const stub = (implementation: Call): Mock<Call> => vi.fn<Call>(implementation);

interface Options {
  readonly url?: string;
  readonly data?: Dashboard;
  readonly load?: Mock<Call>;
  readonly permissions?: readonly string[];
  readonly tablet?: boolean;
}

async function open(options: Options = {}) {
  const load = options.load ?? stub(() => of(options.data ?? dashboard([fine, idle, over, near])));
  TestBed.configureTestingModule({
    providers: [
      provideRouter([
        { path: 'dashboard', component: DashboardPage },
        { path: 'projects/new', component: Blank },
        { path: 'projects/:id/budget', component: Blank },
      ]),
      provideHttpClient(),
      provideHttpClientTesting(),
      { provide: DashboardApi, useValue: { load } },
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
  await harness.navigateByUrl(options.url ?? '/dashboard', DashboardPage);
  await settle(harness.fixture);
  return {
    harness,
    root: harness.fixture.nativeElement as HTMLElement,
    load,
    router: TestBed.inject(Router),
  };
}

describe('DashboardPage', () => {
  it('leads with how many projects need attention, worked out from the rows', async () => {
    const { root } = await open();

    expect(text(root, '.lead__headline')).toBe('2 projects need attention');
    expect(text(root, '.lead__detail')).toBe('1 over budget, 1 near the limit');
  });

  it('shows the company band with the VAT basis in one plain line', async () => {
    const { root } = await open();

    const band = root.querySelector('section[aria-labelledby="band-heading"]')!;
    expect(texts(band, 'dd.money')).toEqual(['₱17,910,000.00', '₱7,210,000.00', '₱10,700,000.00']);
    expect(text(band, 'app-status-chip')).toBe('On track');
    expect(text(band, '.panel-head p')).toBe('Amounts include VAT');
  });

  it('states the other basis when the company compares without VAT', async () => {
    const { root } = await open({ data: dashboard([fine], { basis: 'ExcludingVat' }) });

    expect(text(root, 'section[aria-labelledby="band-heading"] .panel-head p')).toBe(
      'Amounts exclude VAT',
    );
  });

  it('lists the worst first, and says it does', async () => {
    const { root } = await open();

    expect(texts(root, 'tbody th a')).toEqual([
      'Bridge Repair',
      'Clinic',
      'Riverside Residences',
      'Harbor Warehouse',
    ]);
    expect(text(root, '.list-head')).toContain('Sorted with what needs attention first.');
  });

  it('shows each project with its health as an icon and words, and how far over a budget is in words', async () => {
    const { root } = await open();

    const rows = root.querySelectorAll('tbody tr');
    expect(texts(rows[0]!, 'td')).toEqual([
      'Over budget',
      'Over by ₱1,800.75',
      '118.01%',
      '₱10,000.00',
      '₱11,800.75',
      '₱48,496,000.00',
    ]);
    expect(rows[0]!.querySelector('app-status-chip app-icon')).not.toBeNull();
    expect(text(rows[0]!, 'th .cell-sub')).toBe('Active');
    expect(text(rows[2]!, 'app-status-chip')).toBe('On track');
    expect(text(rows[3]!, 'app-status-chip')).toBe('No activity');
  });

  it('shows a project with nothing budgeted or spent as one quiet line, not a row of zeros', async () => {
    const { root } = await open();

    const idleRow = root.querySelectorAll('tbody tr')[3]!;
    expect(text(idleRow, 'td.idle')).toBe('Nothing budgeted or spent yet');
    expect(idleRow.textContent).not.toContain('₱0.00');
  });

  it('shows a margin column only when a project has a contract price', async () => {
    const withPrice = await open();
    expect(texts(withPrice.root, 'thead th')).toContain('Margin so far');
    TestBed.resetTestingModule();

    const noPrice = await open({
      data: dashboard([
        row('a', 'A', 'OnTrack', {
          contractPriceMinor: null,
          marginAfterActualMinor: null,
          budgetMarginMinor: null,
        }),
      ]),
    });
    expect(texts(noPrice.root, 'thead th')).not.toContain('Margin so far');
  });

  it('opens a project on its Budget tab', async () => {
    const { harness, root, router } = await open();

    const link = root.querySelector<HTMLAnchorElement>('tbody th a')!;
    expect(link.getAttribute('href')).toBe('/projects/p-over/budget');
    link.click();
    await settle(harness.fixture);
    expect(router.url).toBe('/projects/p-over/budget');
  });

  it('shows cards on a phone, each opening its project', async () => {
    const { root } = await open({ tablet: false });

    expect(root.querySelector('table')).toBeNull();
    const cards = root.querySelectorAll('.row-card');
    expect(cards).toHaveLength(4);
    expect(text(cards[0]!, '.row-card__title')).toBe('Bridge Repair');
    expect(cards[0]!.querySelector('a')!.getAttribute('href')).toBe('/projects/p-over/budget');
    expect(text(cards[0]!, 'app-status-chip')).toBe('Over budget');
    expect(text(cards[0]!, '.card__remaining')).toContain('Over by ₱1,800.75');
    expect(text(cards[3]!, 'p:nth-of-type(2)')).toBe('Nothing budgeted or spent yet.');
  });

  it('asks for archived projects only when the toggle is on, and keeps that in the address', async () => {
    const { harness, root, load, router } = await open();
    expect(load).toHaveBeenLastCalledWith(false);

    const toggle = root.querySelector<HTMLInputElement>('.toggle input')!;
    expect(text(root, '.toggle')).toBe('Show archived projects');
    toggle.click();
    await settle(harness.fixture);
    await settle(harness.fixture);

    expect(load).toHaveBeenLastCalledWith(true);
    expect(new URL(router.url, 'http://app.test').searchParams.get('archived')).toBe('1');
  });

  it('shows archived projects straight away from a shared link', async () => {
    const { root, load } = await open({ url: '/dashboard?archived=1' });

    expect(load).toHaveBeenLastCalledWith(true);
    expect(root.querySelector<HTMLInputElement>('.toggle input')!.checked).toBe(true);
  });

  it('says nothing to track yet when no project has a budget or a cost', async () => {
    const { root } = await open({
      data: dashboard([idle], { totals: { ...dashboard([idle]).totals, health: 'NoActivity' } }),
    });

    expect(text(root, '.lead__headline')).toBe('Nothing to track yet');
    // No band of zeros.
    expect(root.querySelector('section[aria-labelledby="band-heading"]')).toBeNull();
  });

  it('says no project is over its total budget when none needs attention, and points to the breakdown', async () => {
    const { root } = await open({ data: dashboard([fine, idle]) });

    expect(text(root, '.lead__headline')).toBe('No project is over its total budget');
    expect(text(root, '.lead__detail')).toContain(
      'Open one to see which parts are close to their limit.',
    );
  });

  it('shows the loading skeleton before the figures arrive', async () => {
    // The page is created directly (not through the router harness, which waits for loading to finish).
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: DashboardApi, useValue: { load: () => new Observable(() => undefined) } },
      ],
    });
    await signInForTest(TestBed.inject(SessionState), TestBed.inject(HttpTestingController));
    const fixture = TestBed.createComponent(DashboardPage);
    fixture.detectChanges();
    await Promise.resolve();
    fixture.detectChanges();

    const root = fixture.nativeElement as HTMLElement;
    expect(text(root, 'app-skeleton')).toBe('Loading the dashboard');
    expect(root.querySelector('.lead')).toBeNull();
  });

  it('says there are no projects yet, with a New project link for people who manage projects', async () => {
    const empty = dashboard([], { totals: { ...dashboard([]).totals, health: 'NoActivity' } });
    const manager = await open({ data: empty });
    expect(text(manager.root, 'app-empty-state')).toContain('No projects yet');
    expect(buttonNamed(manager.root, 'New project').getAttribute('href')).toBe('/projects/new');
    // No lead fact or band for nothing.
    expect(manager.root.querySelector('.lead')).toBeNull();
    TestBed.resetTestingModule();

    const reader = await open({
      data: empty,
      permissions: [Permission.projectsRead, Permission.costsRead],
    });
    expect(text(reader.root, 'app-empty-state')).toContain(
      'A project manager can add the first project.',
    );
    expect(
      Array.from(reader.root.querySelectorAll('a')).some((link) =>
        link.textContent?.includes('New project'),
      ),
    ).toBe(false);
  });

  it('says it could not load, and Retry asks again', async () => {
    const failure = new ApiError({
      status: 500,
      title: 'Something went wrong',
      detail: "We couldn't finish that. Try again in a moment.",
      traceId: 'abc-123',
    });
    let attempts = 0;
    const load = stub(() => (++attempts === 1 ? throwError(() => failure) : of(dashboard([fine]))));
    const { harness, root } = await open({ load });

    expect(text(root, 'app-api-error-alert')).toContain('Something went wrong');
    expect(text(root, 'app-api-error-alert')).toContain('Reference: abc-123');

    buttonNamed(root, 'Retry').click();
    await settle(harness.fixture);
    await settle(harness.fixture);

    expect(load).toHaveBeenCalledTimes(2);
    expect(root.querySelector('app-api-error-alert')?.textContent?.trim() ?? '').toBe('');
    expect(text(root, 'tbody th a')).toBe('Riverside Residences');
  });
});

describe('DashboardPage committed cost', () => {
  const committed = {
    committedMinor: 300000,
    forecastMinor: 1180000,
    forecastRemainingMinor: -180000,
    forecastHealth: 'OverBudget' as const,
  };

  it('shows what is committed and the forecast on the project row, with the warning chip, and on the total', async () => {
    const withOrders = row('p-orders', 'Tower B', 'OnTrack', { commitments: committed });
    const data = dashboard([withOrders, fine], {
      totals: { ...dashboard([]).totals, projectCount: 2, commitments: committed },
    });
    const { root } = await open({ data });

    const tower = Array.from(root.querySelectorAll('tbody tr')).find((item) =>
      item.textContent?.includes('Tower B'),
    )!;
    expect(text(tower, 'app-commitment-line')).toContain('Committed ₱3,000.00');
    expect(text(tower, 'app-commitment-line')).toContain('Forecast ₱11,800.00');
    expect(text(tower, 'app-commitment-line app-status-chip')).toBe('Forecast over budget');
    expect(
      text(root, 'section[aria-labelledby="band-heading"] app-commitment-line'),
    ).toContain('Committed ₱3,000.00');
  });

  it('shows no commitment figures for a project with nothing committed', async () => {
    const { root } = await open();

    expect(root.querySelector('app-commitment-line .commitments')).toBeNull();
  });
});
