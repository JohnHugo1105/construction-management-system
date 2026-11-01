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
import { CostEntry, TotaledPage } from '../costing.models';
import {
  PROJECT_ID,
  costEntryFixture,
  pageWithTotals,
  projectFixture,
  referenceFixture,
} from '../testing/costing-fixtures';
import { ProjectCostsTab } from './project-costs-tab';

type Call = (...args: unknown[]) => Observable<unknown>;
const stub = (implementation: Call): Mock<Call> => vi.fn<Call>(implementation);

const manual = costEntryFixture();
const payroll = costEntryFixture({
  id: 'ce-2',
  description: 'Labor, week 42',
  origin: 'Payroll',
  sourceDealer: 'Payroll',
  invoiceNumber: null,
  canEdit: false,
  grossMinor: 4500000,
  netMinor: 4017857,
  vatMinor: 482143,
});
const stock = costEntryFixture({
  id: 'ce-3',
  description: 'Steel transfer',
  origin: 'Inventory',
  sourceDealer: 'Warehouse',
  invoiceNumber: null,
  canEdit: false,
});
const voided = costEntryFixture({
  id: 'ce-4',
  description: 'Wrong invoice',
  status: 'Voided',
  voidReason: 'Entered twice',
  canEdit: false,
});

interface Options {
  readonly url?: string;
  readonly project?: Project;
  readonly permissions?: readonly string[];
  readonly entries?: TotaledPage<CostEntry>;
  readonly tablet?: boolean;
  readonly api?: Record<string, Mock<Call>>;
}

async function open(options: Options = {}) {
  const api = {
    reference: stub(() => of(referenceFixture())),
    listCostEntries: stub(() => of(options.entries ?? pageWithTotals([manual, payroll, stock]))),
    ...options.api,
  };
  TestBed.configureTestingModule({
    providers: [
      provideRouter([{ path: 'projects/:id/costs', component: ProjectCostsTab }]),
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
  await harness.navigateByUrl(options.url ?? `/projects/${PROJECT_ID}/costs`, ProjectCostsTab);
  await settle(harness.fixture);
  return {
    harness,
    root: harness.fixture.nativeElement as HTMLElement,
    api,
    router: TestBed.inject(Router),
  };
}

const WRITER = [Permission.projectsRead, Permission.costsRead, Permission.costsWrite];
const rowFor = (root: HTMLElement, description: string) =>
  Array.from(root.querySelectorAll('app-cost-entries-list tbody tr')).find((row) =>
    row.textContent?.includes(description),
  )!;

describe('ProjectCostsTab: the list', () => {
  it('shows each entry with its supplier, invoice, source chip and the money the server worked out', async () => {
    const { root } = await open();

    const first = rowFor(root, 'Pipe rental and fittings');
    expect(text(first, 'th')).toContain('Cebu Plumbing Supply · Invoice CPS-7781');
    expect(text(first, 'th')).toContain('Equipment · 15200 Process Piping');
    expect(text(first, 'td .entry__total')).toBe('₱215,000.00');
    expect(texts(first, 'td:first-of-type .cell-sub')).toEqual([
      'Before VAT ₱191,964.29',
      'VAT ₱23,035.71',
    ]);
    expect(texts(first, 'td')[1]).toBe('23 Oct 2026');
    expect(text(first, 'app-status-chip')).toBe('Manual');
  });

  it('shows where each entry came from as a chip with words and an icon', async () => {
    const { root } = await open();

    expect(
      texts(root, 'app-cost-entries-list tbody tr app-status-chip').filter((chip) => chip !== ''),
    ).toEqual(['Manual', 'Payroll', 'Inventory']);
    expect(
      root.querySelectorAll('app-cost-entries-list tbody app-status-chip app-icon'),
    ).toHaveLength(3);
  });

  it('shows the totals of the whole filtered set in a sticky summary', async () => {
    const { root } = await open({
      entries: pageWithTotals(
        [manual],
        { netMinor: 643750002, vatMinor: 77249998, grossMinor: 721000000 },
        7,
      ),
    });

    const totals = root.querySelector('.totals')!;
    expect(text(totals, '.totals__main')).toBe('Total spent with VAT₱7,210,000.00');
    expect(text(totals, '.totals__detail')).toBe('Before VAT ₱6,437,500.02 · VAT ₱772,499.98');
  });

  it('shows a voided entry with its reason, struck out and with no buttons, when asked for', async () => {
    const { root } = await open({
      url: `/projects/${PROJECT_ID}/costs?voided=1`,
      permissions: WRITER,
      entries: pageWithTotals([voided]),
    });

    const row = rowFor(root, 'Wrong invoice');
    expect(text(row, 'app-status-chip')).toContain('Voided');
    expect(row.textContent).toContain('Entered twice');
    expect(row.querySelector('button')).toBeNull();
    expect(text(root, 'section[aria-labelledby="costs-heading"]')).toContain(
      'Voided costs are shown but are not counted in the totals.',
    );
  });

  it('asks the API for the filters in the address, including dates and source', async () => {
    const { root, api } = await open({
      url: `/projects/${PROJECT_ID}/costs?origin=Payroll&from=2026-10-01&to=2026-10-31&search=CPS&voided=1&division=15`,
    });

    expect(api['listCostEntries']).toHaveBeenCalledWith(
      PROJECT_ID,
      {
        subProjectId: '',
        division: 15,
        itemType: '',
        origin: 'Payroll',
        from: '2026-10-01',
        to: '2026-10-31',
        search: 'CPS',
        includeVoided: true,
      },
      { page: 1, pageSize: 25 },
    );
    expect(text(root, 'app-filter-toggle')).toBe('Filters (5)');
  });

  it('lays the entries out as cards on a phone', async () => {
    const { root } = await open({ tablet: false, permissions: WRITER });

    expect(root.querySelector('app-cost-entries-list table')).toBeNull();
    expect(root.querySelectorAll('app-cost-entries-list .row-card')).toHaveLength(3);
  });

  it('shows the one next step when nothing has been spent yet', async () => {
    const empty = pageWithTotals([], { netMinor: 0, vatMinor: 0, grossMinor: 0 });
    const writer = await open({ permissions: WRITER, entries: empty });
    expect(text(writer.root, 'app-empty-state')).toContain(
      'Record the first invoice or receipt to see what this project has spent.',
    );
    expect(texts(writer.root, 'button')).toContain('Record a cost');
    TestBed.resetTestingModule();

    const reader = await open({ permissions: VIEWER_PERMISSIONS, entries: empty });
    expect(text(reader.root, 'app-empty-state')).toContain(
      'A cost engineer can record the first cost.',
    );
    expect(texts(reader.root, 'button')).not.toContain('Record a cost');
  });
});

describe('ProjectCostsTab: entries made by the system', () => {
  it('has no Edit or Void on an entry made by payroll or stock, and says where to correct it', async () => {
    const { root } = await open({ permissions: WRITER });

    const payrollRow = rowFor(root, 'Labor, week 42');
    expect(payrollRow.querySelector('button')).toBeNull();
    expect(text(payrollRow, '.entry__system')).toBe('Made by payroll: correct it in Payroll');
    const stockRow = rowFor(root, 'Steel transfer');
    expect(stockRow.querySelector('button')).toBeNull();
    expect(text(stockRow, '.entry__system')).toBe('Made by inventory: correct it in Stock');
    // A manual entry has both.
    expect(texts(rowFor(root, 'Pipe rental and fittings'), 'button')).toEqual(['Edit', 'Void']);
  });

  it('says the same on a phone', async () => {
    const { root } = await open({ tablet: false, permissions: WRITER });

    const cards = Array.from(root.querySelectorAll('app-cost-entries-list .row-card'));
    const payrollCard = cards.find((card) => card.textContent?.includes('Labor, week 42'))!;
    expect(payrollCard.querySelector('button')).toBeNull();
    expect(payrollCard.textContent).toContain('Made by payroll: correct it in Payroll');
  });
});

describe('ProjectCostsTab: who can change what', () => {
  it('gives people who may write the record, edit and void buttons', async () => {
    const { root } = await open({ permissions: WRITER });

    expect(texts(root, 'button')).toContain('Record a cost');
    expect(texts(rowFor(root, 'Pipe rental and fittings'), 'button')).toEqual(['Edit', 'Void']);
  });

  it('shows people who may only read every entry and the totals but no buttons', async () => {
    const { root } = await open({ permissions: VIEWER_PERMISSIONS });

    expect(root.querySelectorAll('app-cost-entries-list tbody tr')).toHaveLength(3);
    expect(root.querySelector('.totals')).not.toBeNull();
    expect(texts(root, 'button')).not.toContain('Record a cost');
    expect(root.querySelector('app-cost-entries-list button')).toBeNull();
    expect(root.querySelector('button[disabled], button[aria-disabled="true"]')).toBeNull();
    expect(root.textContent).not.toContain('Archived');
  });

  it('turns every write off for an archived project and says why to the people who could have written', async () => {
    const { root } = await open({
      permissions: WRITER,
      project: projectFixture({ status: 'Archived' }),
    });

    expect(texts(root, 'button')).not.toContain('Record a cost');
    expect(root.querySelector('app-cost-entries-list button')).toBeNull();
    expect(text(root, '.panel-head')).toContain(
      "Archived: costs can't be recorded, changed or voided.",
    );
    expect(root.querySelectorAll('app-cost-entries-list tbody tr')).toHaveLength(3);
  });
});

describe('ProjectCostsTab: download', () => {
  it('links the costs as a plain download carrying the current filters', async () => {
    const { root } = await open({
      url: `/projects/${PROJECT_ID}/costs?origin=Manual&from=2026-10-01&search=CPS`,
    });

    const link = buttonNamed(root, 'Download costs (CSV)') as HTMLAnchorElement;
    expect(link.tagName).toBe('A');
    expect(link.hasAttribute('download')).toBe(true);
    const url = new URL(link.getAttribute('href')!, 'http://app.test');
    expect(url.pathname).toBe(`/api/projects/${PROJECT_ID}/cost-entries/export`);
    expect(Object.fromEntries(url.searchParams)).toEqual({
      origin: 'Manual',
      from: '2026-10-01',
      search: 'CPS',
    });
  });
});

describe('ProjectCostsTab: recording, editing and voiding', () => {
  it('opens the form to record a cost with supplier and invoice fields', async () => {
    const { harness, root } = await open({ permissions: WRITER });

    buttonNamed(root, 'Record a cost').click();
    await settle(harness.fixture);

    expect(text(root, 'app-line-dialog .dialog__title')).toBe('Record a cost');
    expect(root.querySelector('[formControlName="sourceDealer"]')).not.toBeNull();
    expect(root.querySelector('[formControlName="invoiceNumber"]')).not.toBeNull();
  });

  it('opens the form filled in to edit a manual cost', async () => {
    const { harness, root } = await open({ permissions: WRITER });

    root
      .querySelector<HTMLButtonElement>('button[aria-label="Edit Pipe rental and fittings"]')!
      .click();
    await settle(harness.fixture);

    expect(text(root, 'app-line-dialog .dialog__title')).toBe('Edit cost');
    expect(root.querySelector<HTMLInputElement>('[formControlName="invoiceNumber"]')!.value).toBe(
      'CPS-7781',
    );
  });

  it("shows that day's entries from the supplier when the person asks to open the existing invoice, and closes the form", async () => {
    const duplicate = new ApiError({
      status: 422,
      title: 'Some details need your attention',
      detail: 'Check the highlighted fields and try again.',
      code: 'validation_failed',
      errors: {
        invoiceNumber: [
          'This supplier invoice is already recorded for this project, dated 23 Oct 2026. Check the invoice number.',
        ],
      },
    });
    const createCostEntry = stub(() => throwError(() => duplicate));
    const { harness, root, api, router } = await open({
      permissions: WRITER,
      url: `/projects/${PROJECT_ID}/costs?origin=Payroll`,
      api: { createCostEntry },
    });

    buttonNamed(root, 'Record a cost').click();
    await settle(harness.fixture);
    type(root, '[formControlName="sourceDealer"]', 'Cebu Plumbing Supply');
    type(root, '[formControlName="invoiceNumber"]', 'CPS-7781');
    type(root, '[formControlName="description"]', 'Pipe rental');
    Array.from(root.querySelectorAll<HTMLElement>('label.choice'))
      .find((item) => item.querySelector('.choice__label')?.textContent?.trim() === 'Equipment')!
      .querySelector('input')!
      .click();
    type(root, 'app-number-input input', '1');
    type(root, '[formControlName="unit"]', 'lot');
    type(root, 'app-money-input input', '215,000.00');
    Array.from(root.querySelectorAll<HTMLElement>('label.choice'))
      .find((item) => item.textContent?.includes('Unit cost includes VAT'))!
      .querySelector('input')!
      .click();
    root.querySelector<HTMLButtonElement>('app-line-dialog button[type="submit"]')!.click();
    await settle(harness.fixture);
    expect(text(root, 'app-line-dialog .field__error')).toContain('already recorded');

    buttonNamed(root, 'Open the existing entry').click();
    await settle(harness.fixture);
    await settle(harness.fixture);

    expect(root.querySelector('app-line-dialog')).toBeNull();
    const params = new URL(router.url, 'http://app.test').searchParams;
    // The server ignores case and punctuation in an invoice number, so a text search for it could miss the entry:
    // the list shows the day the server named, from that supplier.
    expect(params.get('search')).toBe('Cebu Plumbing Supply');
    expect(params.get('from')).toBe('2026-10-23');
    expect(params.get('to')).toBe('2026-10-23');
    // Every other filter is cleared so the entry is not hidden by one.
    expect(params.get('origin')).toBeNull();
    expect(api['listCostEntries'].mock.calls.at(-1)![1]).toMatchObject({
      search: 'Cebu Plumbing Supply',
      from: '2026-10-23',
      to: '2026-10-23',
      origin: '',
    });
  });

  it('falls back to searching the invoice number when the server names no date', async () => {
    const createCostEntry = stub(() =>
      throwError(
        () =>
          new ApiError({
            status: 422,
            title: 'Some details need your attention',
            detail: 'Check the highlighted fields and try again.',
            code: 'validation_failed',
            errors: {
              invoiceNumber: ['This supplier invoice is already recorded for this project.'],
            },
          }),
      ),
    );
    const { harness, root, router } = await open({ permissions: WRITER, api: { createCostEntry } });

    buttonNamed(root, 'Record a cost').click();
    await settle(harness.fixture);
    type(root, '[formControlName="sourceDealer"]', 'Cebu Plumbing Supply');
    type(root, '[formControlName="invoiceNumber"]', 'CPS-7781');
    type(root, '[formControlName="description"]', 'Pipe rental');
    Array.from(root.querySelectorAll<HTMLElement>('label.choice'))
      .find((item) => item.querySelector('.choice__label')?.textContent?.trim() === 'Equipment')!
      .querySelector('input')!
      .click();
    type(root, 'app-number-input input', '1');
    type(root, '[formControlName="unit"]', 'lot');
    type(root, 'app-money-input input', '215,000.00');
    Array.from(root.querySelectorAll<HTMLElement>('label.choice'))
      .find((item) => item.textContent?.includes('No VAT'))!
      .querySelector('input')!
      .click();
    root.querySelector<HTMLButtonElement>('app-line-dialog button[type="submit"]')!.click();
    await settle(harness.fixture);
    buttonNamed(root, 'Open the existing entry').click();
    await settle(harness.fixture);
    await settle(harness.fixture);

    const params = new URL(router.url, 'http://app.test').searchParams;
    expect(params.get('search')).toBe('CPS-7781');
    expect(params.get('from')).toBeNull();
  });

  it('asks for a reason before voiding, then voids with the version it loaded and reloads', async () => {
    const voidCostEntry = stub(() => of(costEntryFixture({ status: 'Voided' })));
    const { harness, root, api } = await open({ permissions: WRITER, api: { voidCostEntry } });
    const before = api['listCostEntries']!.mock.calls.length;

    root
      .querySelector<HTMLButtonElement>('button[aria-label="Void Pipe rental and fittings"]')!
      .click();
    await settle(harness.fixture);
    expect(text(root, 'app-reason-dialog .dialog__title')).toBe('Void this cost?');
    expect(text(root, 'app-reason-dialog p')).toBe(
      "It stays on record, no longer counts in totals, and can't be restored.",
    );

    root.querySelector<HTMLButtonElement>('app-reason-dialog button[type="submit"]')!.click();
    await settle(harness.fixture);
    expect(voidCostEntry).not.toHaveBeenCalled();
    expect(text(root, 'app-reason-dialog .field__error')).toBe(
      'Say why this cost is being voided.',
    );

    type(root, 'app-reason-dialog textarea', 'Entered twice');
    buttonNamed(root, 'Void cost').click();
    await settle(harness.fixture);
    await settle(harness.fixture);

    expect(voidCostEntry).toHaveBeenCalledWith('ce-1', 'Entered twice', 2);
    expect(root.querySelector('app-reason-dialog')).toBeNull();
    expect(api['listCostEntries']!.mock.calls.length).toBe(before + 1);
  });
});
