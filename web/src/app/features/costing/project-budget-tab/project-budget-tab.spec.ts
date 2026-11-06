import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { Observable, of, throwError } from 'rxjs';
import type { Mock } from 'vitest';
import { ApiError } from '../../../core/api/api-error';
import { Permission } from '../../../core/session/permissions';
import { SessionState } from '../../../core/session/session-state';
import { Viewport } from '../../../shared/ui/viewport/viewport';
import { buttonNamed, settle, text, texts, type } from '../../../testing/dom';
import { VIEWER_PERMISSIONS, signInForTest } from '../../../testing/session-fixture';
import { ProjectContext } from '../../projects/project-context';
import { ProjectsApi } from '../../projects/projects-api';
import { Project } from '../../projects/projects.models';
import { CostingApi } from '../costing-api';
import { BudgetLine, CostSummary, TotaledPage } from '../costing.models';
import {
  PROJECT_ID,
  SUB_PROJECT_ID,
  budgetLineFixture,
  pageWithTotals,
  projectFixture,
  referenceFixture,
  summaryFixture,
} from '../testing/costing-fixtures';
import { ProjectBudgetTab } from './project-budget-tab';

type Call = (...args: unknown[]) => Observable<unknown>;
const stub = (implementation: Call): Mock<Call> => vi.fn<Call>(implementation);

interface Options {
  readonly url?: string;
  readonly project?: Project;
  readonly permissions?: readonly string[];
  readonly summary?: CostSummary;
  readonly lines?: TotaledPage<BudgetLine>;
  readonly tablet?: boolean;
  readonly api?: Record<string, Mock<Call>>;
}

async function open(options: Options = {}) {
  const lines =
    options.lines ??
    pageWithTotals([
      budgetLineFixture(),
      budgetLineFixture({
        id: 'bl-2',
        description: 'Deformed bars',
        wbsCode: '3200',
        subScope: 'Concrete Reinforcement',
        unit: 'kg',
        quantity: 42000,
        unitCostMinor: 5200,
      }),
    ]);
  const api = {
    reference: stub(() => of(referenceFixture())),
    costSummary: stub(() => of(options.summary ?? summaryFixture())),
    listBudgetLines: stub(() => of(lines)),
    ...options.api,
  };
  TestBed.configureTestingModule({
    providers: [
      provideRouter([{ path: 'projects/:id/budget', component: ProjectBudgetTab }]),
      provideHttpClient(),
      provideHttpClientTesting(),
      ProjectContext,
      { provide: ProjectsApi, useValue: {} },
      { provide: CostingApi, useValue: api },
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
  TestBed.inject(ProjectContext).replace(options.project ?? projectFixture());
  const harness = await RouterTestingHarness.create();
  await harness.navigateByUrl(options.url ?? `/projects/${PROJECT_ID}/budget`, ProjectBudgetTab);
  await settle(harness.fixture);
  return {
    harness,
    root: harness.fixture.nativeElement as HTMLElement,
    api,
    router: TestBed.inject(Router),
  };
}

const READER = [Permission.projectsRead, Permission.costsRead];
const WRITER = [Permission.projectsRead, Permission.costsRead, Permission.costsWrite];
const buttonLabels = (root: HTMLElement) => texts(root, 'button, a[appButton]');
const lastFilters = (api: Record<string, Mock<Call>>) =>
  api['listBudgetLines']!.mock.calls.at(-1)![1] as Record<string, unknown>;

describe('ProjectBudgetTab: where the project stands', () => {
  it('shows the project position with its health, the VAT basis in one line and the margins from the server', async () => {
    const { root } = await open();

    const position = root.querySelector('app-cost-position')!;
    expect(texts(position, 'dt')).toEqual([
      'Budget',
      'Spent',
      'Remaining',
      'Used',
      'Margin so far',
      'Margin if finished on budget',
    ]);
    expect(texts(position, 'dd.money')).toEqual([
      '₱17,910,000.00',
      '₱7,210,000.00',
      '₱10,700,000.00',
      '₱41,290,000.00',
      '₱30,590,000.00',
    ]);
    expect(text(position, 'app-status-chip')).toBe('On track');
    expect(text(root, '#position-heading + p')).toBe('Amounts include VAT');
  });

  it('states the other VAT basis, and says margins still use amounts with VAT', async () => {
    const { root } = await open({ summary: summaryFixture({ basis: 'ExcludingVat' }) });

    expect(text(root, '#position-heading + p')).toBe('Amounts exclude VAT');
    expect(text(root, 'app-cost-position')).toContain('Margins always use amounts with VAT');
  });

  it('shows no margins when the project has no contract price', async () => {
    const { root } = await open({
      summary: summaryFixture({
        contractPriceMinor: null,
        marginAfterActualMinor: null,
        budgetMarginMinor: null,
      }),
    });

    expect(root.querySelector('.margins')).toBeNull();
    expect(root.textContent).not.toContain('Margin so far');
  });

  it('shows how far over budget in words when the position is over', async () => {
    const { root } = await open({
      summary: summaryFixture({
        position: {
          budgetMinor: 1000,
          actualMinor: 1500,
          remainingMinor: -500,
          usedBps: 15000,
          health: 'OverBudget',
        },
      }),
    });

    expect(text(root, 'app-cost-position dd.position__over')).toBe('Over by ₱5.00');
    expect(text(root, 'app-cost-position app-status-chip')).toBe('Over budget');
  });
});

describe('ProjectBudgetTab: forecast over budget', () => {
  // The demo's Riverside Residences: the work done says it finishes ₱11,040,912.96 over a budget of ₱18,443,999.99.
  const forecast = {
    estimateAtCompletionMinor: 2948491296,
    overBudgetMinor: 1104091296,
    percentCompleteBps: 3318,
  };

  it('says where the work done says the project will finish, with a way to the Progress tab', async () => {
    const { root } = await open({ summary: summaryFixture({ progressForecast: forecast }) });

    const line = root.querySelector('app-cost-position .forecast-line')!;
    expect(line.textContent?.replace(/\s+/g, ' ').trim()).toBe(
      'Forecast to finish ₱29,484,912.96, which is ₱11,040,912.96 over the budget. See Progress.',
    );
    const link = line.querySelector<HTMLAnchorElement>('a')!;
    expect(link.textContent?.trim()).toBe('See Progress');
    expect(link.getAttribute('href')).toBe(`/projects/${PROJECT_ID}/progress`);
    expect(line.querySelector('app-icon')).not.toBeNull();
  });

  it('shows nothing when the forecast is within the budget, or when there is none', async () => {
    for (const progressForecast of [
      { ...forecast, overBudgetMinor: 0 },
      { ...forecast, overBudgetMinor: -250000 },
      null,
      undefined,
    ]) {
      TestBed.resetTestingModule();
      const { root } = await open({ summary: summaryFixture({ progressForecast }) });

      expect(root.querySelector('.forecast-line')).toBeNull();
      expect(root.querySelector('app-cost-position')!.textContent).not.toContain(
        'Forecast to finish',
      );
    }
  });
});

describe('ProjectBudgetTab: where the money goes', () => {
  it('opens the divisions that have something over budget, unbudgeted or near the limit and folds the rest', async () => {
    const { root } = await open();

    const groups = Array.from(
      root.querySelectorAll('tbody.breakdown__body, app-cost-breakdown tbody'),
    );
    expect(groups).toHaveLength(3);
    expect(groups.map((group) => group.querySelectorAll('tr').length)).toEqual([3, 3, 1]);

    const expanders = Array.from(
      root.querySelectorAll<HTMLButtonElement>('app-cost-breakdown .expander'),
    );
    expect(expanders.map((item) => item.getAttribute('aria-expanded'))).toEqual([
      'true',
      'true',
      'false',
    ]);
  });

  it('names every problem in words and with an icon, never colour alone', async () => {
    const { root } = await open();

    const rows = Array.from(root.querySelectorAll('app-cost-breakdown tr'));
    const status = (name: string) =>
      text(
        rows.find((row) => row.textContent?.includes(name))!,
        'app-status-chip',
      );
    expect(status('Cast-In-Place Concrete')).toBe('Over budget');
    expect(status('Concrete Reinforcement')).toBe('Near limit');
    expect(status('Process Piping')).toBe('Unbudgeted');
    expect(
      root.querySelectorAll('app-cost-breakdown app-status-chip app-icon').length,
    ).toBeGreaterThan(5);
    expect(text(root, 'app-cost-breakdown')).toContain(
      'Needs a look: 1 over budget, 1 near the limit',
    );
    expect(text(root, 'app-cost-breakdown')).toContain('No budget was set for this.');
    expect(text(root, 'app-cost-breakdown')).toContain('Over by ₱180,750.00');
  });

  it('opens and folds a division when asked, and tells assistive technology which it is', async () => {
    const { harness, root } = await open();
    const finishes = Array.from(
      root.querySelectorAll<HTMLButtonElement>('app-cost-breakdown .expander'),
    ).find((item) => item.textContent?.includes('Finishes'))!;

    expect(root.textContent).not.toContain('Code 9300');
    finishes.click();
    await settle(harness.fixture);
    expect(finishes.getAttribute('aria-expanded')).toBe('true');
    expect(root.textContent).toContain('Code 9300');

    finishes.click();
    await settle(harness.fixture);
    expect(finishes.getAttribute('aria-expanded')).toBe('false');
    expect(root.textContent).not.toContain('Code 9300');
  });

  it('keeps sub-project and type behind a segmented control, and offers only the views that mean something', async () => {
    const { harness, root } = await open();

    expect(texts(root, 'app-segmented-control .segment')).toEqual(['By division', 'By type']);
    buttonNamed(root, 'By type').click();
    await settle(harness.fixture);

    const names = texts(root, 'app-cost-breakdown tbody th[scope="row"]');
    expect(names).toHaveLength(2);
    expect(names[0]).toContain('Equipment');
    expect(names[0]).toContain('No budget was set for this.');
    expect(names[1]).toBe('Material');
    expect(
      root
        .querySelector('app-segmented-control .segment[aria-pressed="true"]')
        ?.textContent?.trim(),
    ).toBe('By type');
  });

  it('lays the breakdown out as cards on a phone', async () => {
    const { root } = await open({ tablet: false });

    expect(root.querySelector('app-cost-breakdown table')).toBeNull();
    expect(root.querySelectorAll('app-cost-breakdown .breakdown-card')).toHaveLength(3);
  });
});

describe('ProjectBudgetTab: the budget lines', () => {
  it('shows the totals of the whole filtered set from the response, in a sticky summary', async () => {
    const { root } = await open();

    const totals = root.querySelector('.totals')!;
    expect(text(totals, '.totals__main')).toBe('Total with VAT₱17,910,000.00');
    expect(text(totals, '.totals__detail')).toBe('Before VAT ₱16,248,214.28 · VAT ₱1,661,785.72');
    expect(totals.getAttribute('role')).toBe('status');
  });

  it('words the totals as the matching total once a filter is on', async () => {
    const { root } = await open({ url: `/projects/${PROJECT_ID}/budget?division=3` });

    expect(text(root, '.totals__main')).toContain('Matching total with VAT');
  });

  it('lists each line with its figures, quantity and where it is classified', async () => {
    const { root } = await open();

    const rows = root.querySelectorAll('app-budget-lines-list tbody tr');
    expect(rows).toHaveLength(2);
    expect(text(rows[0]!, 'th')).toContain('Ready-mix concrete 4000 psi');
    expect(text(rows[0]!, 'th')).toContain('Material · 3300 Cast-In-Place Concrete');
    const cells = texts(rows[0]!, 'td');
    // The total leads; what is before VAT and the VAT itself sit quietly under it.
    expect(text(rows[0]!, 'td .line__total')).toBe('₱3,480,000.00');
    expect(texts(rows[0]!, 'td:first-of-type .cell-sub')).toEqual([
      'Before VAT ₱3,107,142.86',
      'VAT ₱372,857.14',
    ]);
    expect(cells[1]).toBe('600 m³ × ₱5,800.00 includes VAT');
  });

  it('shows a removed line with its reason, out of the totals, when asked for', async () => {
    const removed = budgetLineFixture({
      id: 'bl-9',
      status: 'Removed',
      removedReason: 'Replaced by a new quote',
    });
    const { root } = await open({
      url: `/projects/${PROJECT_ID}/budget?removed=1`,
      permissions: WRITER,
      lines: pageWithTotals([removed]),
    });

    expect(text(root, 'app-budget-lines-list tbody')).toContain('Replaced by a new quote');
    expect(text(root, 'app-budget-lines-list app-status-chip')).toBe('Removed');
    expect(text(root, 'section[aria-labelledby="lines-heading"]')).toContain(
      'Removed lines are shown but are not counted in the totals.',
    );
    // A removed line has no Edit or Remove.
    expect(texts(root, 'app-budget-lines-list button')).toEqual([]);
  });

  it('asks the API for the filters in the address, and keeps them in the address when they change', async () => {
    const { harness, root, api, router } = await open({
      url: `/projects/${PROJECT_ID}/budget?division=3&itemType=Material&search=rebar&subProject=${SUB_PROJECT_ID}&page=2`,
    });

    expect(api['listBudgetLines']).toHaveBeenCalledWith(
      PROJECT_ID,
      {
        subProjectId: SUB_PROJECT_ID,
        division: 3,
        itemType: 'Material',
        search: 'rebar',
        includeRemoved: false,
      },
      { page: 2, pageSize: 25 },
    );
    // The filter panel opens by itself when filters are in use, and the button counts them.
    expect(text(root, 'app-filter-toggle')).toBe('Filters (3)');
    expect(root.querySelector('#budget-filters')).not.toBeNull();

    const division = Array.from(
      root.querySelectorAll<HTMLSelectElement>('#budget-filters select'),
    ).find((item) => item.closest('app-form-field')?.textContent?.includes('Division'))!;
    division.value = '15';
    division.dispatchEvent(new Event('change', { bubbles: true }));
    await settle(harness.fixture);
    await harness.fixture.whenStable();
    const params = new URL(router.url, 'http://app.test').searchParams;
    expect(params.get('division')).toBe('15');
    expect(params.get('page')).toBeNull();
    expect(params.get('search')).toBe('rebar');
    expect(lastFilters(api)['division']).toBe(15);
  });

  it('searches as the person types and goes back to page 1', async () => {
    const { harness, root, api } = await open({ url: `/projects/${PROJECT_ID}/budget?page=2` });
    vi.useFakeTimers();
    try {
      type(root, 'app-search-box input', 'tile');
      vi.advanceTimersByTime(400);
    } finally {
      vi.useRealTimers();
    }
    await settle(harness.fixture);

    expect(lastFilters(api)['search']).toBe('tile');
    expect(api['listBudgetLines'].mock.calls.at(-1)![2]).toEqual({ page: 1, pageSize: 25 });
  });

  it('says when nothing matches and offers every line again', async () => {
    const { harness, root, router } = await open({
      url: `/projects/${PROJECT_ID}/budget?search=zzz`,
      lines: pageWithTotals([], { netMinor: 0, vatMinor: 0, grossMinor: 0 }),
    });

    expect(text(root, 'app-empty-state')).toContain('No budget lines match');
    buttonNamed(root, 'Show all budget lines').click();
    await settle(harness.fixture);
    expect(new URL(router.url, 'http://app.test').searchParams.get('search')).toBeNull();
  });

  it('lays the lines out as cards on a phone, each with its own Edit and Remove', async () => {
    const { root } = await open({ tablet: false, permissions: WRITER });

    expect(root.querySelector('app-budget-lines-list table')).toBeNull();
    const cards = root.querySelectorAll('app-budget-lines-list .row-card');
    expect(cards).toHaveLength(2);
    expect(texts(cards[0]!, 'button')).toEqual(['Edit', 'Remove']);
  });

  it('shows the one next step when there is no budget yet', async () => {
    const empty = pageWithTotals([], { netMinor: 0, vatMinor: 0, grossMinor: 0 });
    const writer = await open({
      permissions: WRITER,
      lines: empty,
      summary: summaryFixture({
        position: {
          budgetMinor: 0,
          actualMinor: 0,
          remainingMinor: 0,
          usedBps: null,
          health: 'NoActivity',
        },
      }),
    });

    expect(text(writer.root, 'app-empty-state')).toContain(
      'This project has no budget yet. Add your first budget line to start tracking cost.',
    );
    expect(buttonLabels(writer.root)).toContain('Add budget line');
    // Nothing else is drawn for a project with no budget: no position, no breakdown, no filters.
    expect(writer.root.querySelector('app-cost-position')).toBeNull();
    expect(writer.root.querySelector('app-cost-breakdown')).toBeNull();
    expect(writer.root.querySelector('.toolbar')).toBeNull();
    TestBed.resetTestingModule();

    const reader = await open({
      permissions: READER,
      lines: empty,
      summary: summaryFixture({
        position: {
          budgetMinor: 0,
          actualMinor: 0,
          remainingMinor: 0,
          usedBps: null,
          health: 'NoActivity',
        },
      }),
    });
    expect(text(reader.root, 'app-empty-state')).toContain(
      'A cost engineer can add the first budget line.',
    );
    expect(buttonLabels(reader.root)).not.toContain('Add budget line');
  });

  it('says the lines could not load, with a way to try again', async () => {
    const failing = stub(() =>
      throwError(
        () =>
          new ApiError({
            status: 500,
            title: 'Something went wrong',
            detail: "We couldn't finish that. Try again in a moment.",
          }),
      ),
    );
    const { root } = await open({ api: { listBudgetLines: failing } });

    expect(text(root, 'app-api-error-alert')).toContain('Something went wrong');
    expect(buttonLabels(root)).toContain('Try again');
  });
});

describe('ProjectBudgetTab: who can change what', () => {
  it('gives people who may write the add, edit and remove buttons', async () => {
    const { root } = await open({ permissions: WRITER });

    expect(buttonLabels(root)).toContain('Add budget line');
    const row = root.querySelector('app-budget-lines-list tbody tr')!;
    expect(texts(row, 'button')).toEqual(['Edit', 'Remove']);
  });

  it('shows people who may only read everything except the buttons', async () => {
    const { root } = await open({ permissions: VIEWER_PERMISSIONS });

    expect(root.querySelector('app-cost-position')).not.toBeNull();
    expect(root.querySelector('app-cost-breakdown')).not.toBeNull();
    expect(root.querySelectorAll('app-budget-lines-list tbody tr')).toHaveLength(2);
    expect(buttonLabels(root)).not.toContain('Add budget line');
    expect(root.querySelector('app-budget-lines-list button')).toBeNull();
    // Hidden, not disabled: there is nothing greyed out either.
    expect(root.querySelector('button[disabled], button[aria-disabled="true"]')).toBeNull();
    // They can still download.
    expect(buttonLabels(root)).toContain('Download budget (CSV)');
    // And nothing about being archived is said to someone who could not have written anyway.
    expect(root.textContent).not.toContain('Archived');
  });

  it('turns every write off for an archived project and says why to the people who could have written', async () => {
    const { root } = await open({
      permissions: WRITER,
      project: projectFixture({ status: 'Archived' }),
    });

    expect(buttonLabels(root)).not.toContain('Add budget line');
    expect(root.querySelector('app-budget-lines-list button')).toBeNull();
    expect(text(root, 'section[aria-labelledby="lines-heading"] .panel-head')).toContain(
      "Archived: lines can't be added, changed or removed.",
    );
    // Reading still works.
    expect(root.querySelectorAll('app-budget-lines-list tbody tr')).toHaveLength(2);
    expect(root.querySelector('app-cost-position')).not.toBeNull();
  });
});

describe('ProjectBudgetTab: downloads', () => {
  it('links the budget and the cost summary as plain downloads, the budget with the current filters', async () => {
    const { root } = await open({
      url: `/projects/${PROJECT_ID}/budget?division=3&removed=1&search=rebar`,
    });

    const budget = buttonNamed(root, 'Download budget (CSV)') as HTMLAnchorElement;
    const summary = buttonNamed(root, 'Download cost summary (CSV)') as HTMLAnchorElement;
    expect(budget.tagName).toBe('A');
    expect(budget.hasAttribute('download')).toBe(true);
    const url = new URL(budget.getAttribute('href')!, 'http://app.test');
    expect(url.pathname).toBe(`/api/projects/${PROJECT_ID}/budget-lines/export`);
    expect(Object.fromEntries(url.searchParams)).toEqual({
      division: '3',
      search: 'rebar',
      includeRemoved: 'true',
    });
    expect(summary.getAttribute('href')).toBe(`/api/projects/${PROJECT_ID}/cost-summary/export`);
  });
});

describe('ProjectBudgetTab: adding, editing and removing', () => {
  it('opens the form to add a line, and reloads the lines and the position after it is saved', async () => {
    const createBudgetLine = stub(() => of(budgetLineFixture()));
    const { harness, root, api } = await open({ permissions: WRITER, api: { createBudgetLine } });
    const linesBefore = api['listBudgetLines']!.mock.calls.length;
    const summaryBefore = api['costSummary']!.mock.calls.length;

    buttonNamed(root, 'Add budget line').click();
    await settle(harness.fixture);
    expect(root.querySelector('app-line-dialog')).not.toBeNull();
    expect(text(root, 'app-line-dialog .dialog__title')).toBe('Add budget line');
    type(root, '[formControlName="description"]', 'Gravel');
    root.querySelector<HTMLInputElement>('input[type="radio"]')!.click();
    type(root, 'app-number-input input', '10');
    type(root, '[formControlName="unit"]', 'm³');
    type(root, 'app-money-input input', '1,000.00');
    Array.from(root.querySelectorAll<HTMLElement>('label.choice'))
      .find((item) => item.textContent?.includes('Unit cost includes VAT'))!
      .querySelector('input')!
      .click();
    root.querySelector<HTMLButtonElement>('app-line-dialog button[type="submit"]')!.click();
    await settle(harness.fixture);
    await settle(harness.fixture);

    expect(createBudgetLine).toHaveBeenCalledTimes(1);
    expect(root.querySelector('app-line-dialog')).toBeNull();
    expect(api['listBudgetLines']!.mock.calls.length).toBe(linesBefore + 1);
    expect(api['costSummary']!.mock.calls.length).toBe(summaryBefore + 1);
  });

  it('opens the form filled in to edit a line', async () => {
    const { harness, root } = await open({ permissions: WRITER });

    buttonNamed(root, 'Edit').click();
    await settle(harness.fixture);

    expect(text(root, 'app-line-dialog .dialog__title')).toBe('Edit budget line');
    expect(root.querySelector<HTMLInputElement>('[formControlName="description"]')!.value).toBe(
      'Ready-mix concrete 4000 psi',
    );
  });

  it('asks for a reason before removing, then removes with the version it loaded and reloads', async () => {
    const removeBudgetLine = stub(() => of(budgetLineFixture({ status: 'Removed' })));
    const { harness, root, api } = await open({ permissions: WRITER, api: { removeBudgetLine } });
    const linesBefore = api['listBudgetLines']!.mock.calls.length;

    root
      .querySelector<HTMLButtonElement>('button[aria-label="Remove Ready-mix concrete 4000 psi"]')!
      .click();
    await settle(harness.fixture);
    expect(text(root, 'app-reason-dialog .dialog__title')).toBe('Remove this budget line?');
    expect(text(root, 'app-reason-dialog p')).toBe(
      'It stays on record and no longer counts in totals.',
    );

    // Without a reason nothing happens.
    root.querySelector<HTMLButtonElement>('app-reason-dialog button[type="submit"]')!.click();
    await settle(harness.fixture);
    expect(removeBudgetLine).not.toHaveBeenCalled();
    expect(text(root, 'app-reason-dialog .field__error')).toBe(
      'Say why this budget line is being removed.',
    );

    type(root, 'app-reason-dialog textarea', '  Replaced by a new quote ');
    root.querySelector<HTMLButtonElement>('app-reason-dialog button[type="submit"]')!.click();
    await settle(harness.fixture);
    await settle(harness.fixture);

    expect(removeBudgetLine).toHaveBeenCalledWith('bl-1', 'Replaced by a new quote', 4);
    expect(root.querySelector('app-reason-dialog')).toBeNull();
    expect(api['listBudgetLines']!.mock.calls.length).toBe(linesBefore + 1);
  });

  it('keeps the dialog and the reason when the server refuses, and offers a reload after a conflict', async () => {
    const conflict = new ApiError({
      status: 409,
      title: 'Someone else changed this',
      detail: 'Reload to see the latest.',
      code: 'concurrency_conflict',
    });
    const removeBudgetLine = stub(() => throwError(() => conflict));
    const { harness, root, api } = await open({ permissions: WRITER, api: { removeBudgetLine } });

    root
      .querySelector<HTMLButtonElement>('button[aria-label="Remove Ready-mix concrete 4000 psi"]')!
      .click();
    await settle(harness.fixture);
    type(root, 'app-reason-dialog textarea', 'Duplicate');
    root.querySelector<HTMLButtonElement>('app-reason-dialog button[type="submit"]')!.click();
    await settle(harness.fixture);

    expect(text(root, 'app-reason-dialog app-api-error-alert')).toContain(
      'Someone else changed this',
    );
    expect(root.querySelector<HTMLTextAreaElement>('app-reason-dialog textarea')!.value).toBe(
      'Duplicate',
    );
    const linesBefore = api['listBudgetLines']!.mock.calls.length;

    Array.from(root.querySelectorAll<HTMLButtonElement>('app-reason-dialog button'))
      .find((item) => item.textContent?.trim() === 'Reload')!
      .click();
    await settle(harness.fixture);

    expect(root.querySelector('app-reason-dialog')).toBeNull();
    expect(api['listBudgetLines']!.mock.calls.length).toBe(linesBefore + 1);
  });
});
