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
import { InventoryApi } from './inventory-api';
import {
  ANNEX_ID,
  PROJECT_LOOKUP,
  RIVERSIDE_ID,
  excavator,
  hammers,
  listItemOf,
} from './inventory.fixtures';
import { InventoryListItem, InventoryListParams } from './inventory.models';
import { StockPage } from './stock-page';

@Component({ template: '', changeDetection: ChangeDetectionStrategy.OnPush })
class Blank {}

const retired = listItemOf(
  hammers({
    id: 'item-saw',
    description: 'Hand saw',
    status: 'Retired',
    totalQuantity: 0,
    balances: [],
    brand: null,
  }),
);

interface Options {
  readonly url?: string;
  readonly result?: PagedResult<InventoryListItem>;
  readonly list?: (params: InventoryListParams) => Observable<PagedResult<InventoryListItem>>;
  readonly tablet?: boolean;
  readonly viewer?: boolean;
}

async function open(options: Options = {}) {
  const list = vi.fn(
    options.list ??
      (() => of(options.result ?? pageOf([listItemOf(hammers()), listItemOf(excavator())]))),
  );
  const projectLookup = vi.fn(() => of(PROJECT_LOOKUP));
  TestBed.configureTestingModule({
    providers: [
      provideRouter([
        { path: 'stock', component: StockPage },
        { path: 'stock/new', component: Blank },
        { path: 'stock/:id', component: Blank },
      ]),
      provideHttpClient(),
      provideHttpClientTesting(),
      { provide: InventoryApi, useValue: { list, projectLookup } },
      {
        provide: Viewport,
        useValue: { isTablet: signal(options.tablet ?? true), isDesktop: signal(true) },
      },
    ],
  });
  await signInForTest(
    TestBed.inject(SessionState),
    TestBed.inject(HttpTestingController),
    options.viewer ? { roles: ['Viewer'], permissions: VIEWER_PERMISSIONS } : {},
  );
  const harness = await RouterTestingHarness.create();
  await harness.navigateByUrl(options.url ?? '/stock', StockPage);
  await settle(harness.fixture);
  return {
    harness,
    root: harness.fixture.nativeElement as HTMLElement,
    list,
    projectLookup,
    router: TestBed.inject(Router),
  };
}

const requests = (list: ReturnType<typeof vi.fn>) =>
  list.mock.calls.map(([params]) => params as InventoryListParams);
const paramsOf = (url: string) =>
  Object.fromEntries(new URL(url, 'http://app.test').searchParams.entries());

async function choose(
  harness: RouterTestingHarness,
  root: HTMLElement,
  selector: string,
  value: string,
) {
  const select = root.querySelector<HTMLSelectElement>(selector)!;
  select.value = value;
  select.dispatchEvent(new Event('change', { bubbles: true }));
  await settle(harness.fixture);
}

describe('StockPage on a wide screen', () => {
  it('shows a row per item: description, brand and serial, category, quantity with unit, and where it is', async () => {
    const { root } = await open();

    const rows = root.querySelectorAll('tbody tr');
    expect(rows).toHaveLength(2);
    expect(text(rows[0]!, 'th a')).toBe('Claw hammer 16 oz');
    expect(text(rows[0]!, 'th .cell-sub')).toBe('Stanley');
    expect(texts(rows[0]!, 'td')[0]).toBe('Tool');
    expect(texts(rows[0]!, 'td')[1]).toBe('24 pc');
    expect(texts(rows[0]!, '.places .place')).toEqual(['Warehouse 14', 'Riverside Residences 10']);

    // A single piece shows its serial number.
    expect(text(rows[1]!, 'th .cell-sub')).toBe('Komatsu · Serial PC200-8-A41127');
    expect(texts(rows[1]!, 'td')[0]).toBe('Machinery');
    expect(texts(rows[1]!, 'td')[1]).toBe('1 unit');
    expect(texts(rows[1]!, '.places .place')).toEqual(['Riverside Residences 1']);
  });

  it('links each item to its page', async () => {
    const { root } = await open();

    expect(root.querySelector('tbody a')!.getAttribute('href')).toBe('/stock/item-hammer');
  });

  it('labels the sideways-scrolling table as a region the keyboard can reach', async () => {
    const { root } = await open();

    const region = root.querySelector('.table-wrap')!;
    expect(region.getAttribute('role')).toBe('region');
    expect(region.getAttribute('aria-label')).toBe('Stock items');
    expect(region.getAttribute('tabindex')).toBe('0');
  });

  it('asks for the first page of active items anywhere', async () => {
    const { list } = await open();

    expect(requests(list)).toEqual([
      {
        search: '',
        category: '',
        status: 'Active',
        projectId: '',
        inWarehouse: false,
        page: 1,
        pageSize: 25,
      },
    ]);
  });

  it('says "No stock anywhere" for an item with nothing left, instead of a zero or an empty cell', async () => {
    const { root } = await open({
      url: '/stock?retired=1',
      result: pageOf([retired]),
    });

    const row = root.querySelector('tbody tr')!;
    expect(texts(row, 'td')[0]).toBe('Tool Retired');
    expect(texts(row, 'td')[1]).toBe('');
    expect(text(row, '.places__none')).toBe('No stock anywhere');
    expect(row.querySelector('.place')).toBeNull();
  });
});

describe('StockPage on a phone', () => {
  it('shows cards with the balances as compact chips instead of a table', async () => {
    const { root } = await open({ tablet: false });

    expect(root.querySelector('table')).toBeNull();
    const cards = root.querySelectorAll('.row-card');
    expect(cards).toHaveLength(2);
    expect(cards[0]!.textContent).toContain('Claw hammer 16 oz');
    expect(text(cards[0]!, '.row-card__quantity')).toBe('24 pc');
    expect(texts(cards[0]!, '.place')).toEqual(['Warehouse 14', 'Riverside Residences 10']);
    expect(cards[1]!.textContent).toContain('Serial PC200-8-A41127');
  });
});

describe('StockPage permissions and empty states', () => {
  it('gives people who manage stock the one next step: Add an item', async () => {
    const { root } = await open();

    expect(buttonNamed(root.querySelector('.toolbar')!, 'Add an item').getAttribute('href')).toBe(
      '/stock/new',
    );
  });

  it('shows no "Add an item" to someone who can only read stock', async () => {
    const { root } = await open({ viewer: true });

    expect(root.textContent).not.toContain('Add an item');
    expect(root.querySelectorAll('tbody tr')).toHaveLength(2);
  });

  it('invites a manager to add the first item', async () => {
    const { root } = await open({ result: pageOf([]) });

    expect(text(root, 'app-empty-state')).toContain('No stock items yet');
    expect(
      buttonNamed(root.querySelector('app-empty-state')!, 'Add an item').getAttribute('href'),
    ).toBe('/stock/new');
  });

  it('tells a reader who can add the first item, without a button', async () => {
    const { root } = await open({ result: pageOf([]), viewer: true });

    expect(text(root, 'app-empty-state')).toContain('A warehouse keeper can add the first item.');
    expect(root.querySelector('app-empty-state a')).toBeNull();
  });

  it('says nothing matches a search, and clearing it goes back to the full list', async () => {
    const { harness, root, router } = await open({ url: '/stock?search=zzz', result: pageOf([]) });

    expect(text(root, 'app-empty-state')).toContain('No items match');
    buttonNamed(root, 'Show all items').click();
    await settle(harness.fixture);

    expect(router.url).toBe('/stock');
  });

  it('says so when no retired items match, and points back to active items', async () => {
    const { root } = await open({ url: '/stock?retired=1', result: pageOf([]) });

    expect(text(root, 'app-empty-state')).toContain('No retired items match');
  });
});

describe('StockPage category chips', () => {
  it('filters by category from a chip, starting again at page 1', async () => {
    const { harness, root, list, router } = await open({ url: '/stock?page=3' });

    buttonNamed(root, 'Tools').click();
    await settle(harness.fixture);

    expect(paramsOf(router.url)).toEqual({ category: 'Tool' });
    expect(requests(list).at(-1)).toMatchObject({ category: 'Tool', page: 1 });
    expect(buttonNamed(root, 'Tools').getAttribute('aria-pressed')).toBe('true');
    expect(buttonNamed(root, 'All').getAttribute('aria-pressed')).toBe('false');
  });

  it('sends Machinery as its own category and leaves the category out for All', async () => {
    const { harness, root, list } = await open();

    buttonNamed(root, 'Machinery').click();
    await settle(harness.fixture);
    expect(requests(list).at(-1)).toMatchObject({ category: 'Machinery' });

    buttonNamed(root, 'All').click();
    await settle(harness.fixture);
    expect(requests(list).at(-1)).toMatchObject({ category: '' });
  });
});

describe('StockPage place filter', () => {
  it('offers anywhere, the warehouse, then every project with archived ones marked', async () => {
    const { root, projectLookup } = await open();

    expect(projectLookup).toHaveBeenCalledWith(true);
    expect(texts(root, '#place-filter option')).toEqual([
      'Anywhere',
      'In the warehouse',
      'Harbor Warehouse',
      'Riverside Residences',
      'Old School Annex (archived)',
    ]);
  });

  it('keeps stock in the warehouse as ?place=warehouse and asks the API for it', async () => {
    const { harness, root, list, router } = await open();

    await choose(harness, root, '#place-filter', 'warehouse');

    expect(paramsOf(router.url)).toEqual({ place: 'warehouse' });
    expect(requests(list).at(-1)).toMatchObject({ inWarehouse: true, projectId: '' });
    expect(root.querySelector<HTMLSelectElement>('#place-filter')!.value).toBe('warehouse');
  });

  it('keeps a project as ?place=<id> and asks the API for items on it', async () => {
    const { harness, root, list, router } = await open();

    await choose(harness, root, '#place-filter', RIVERSIDE_ID);

    expect(paramsOf(router.url)).toEqual({ place: RIVERSIDE_ID });
    expect(requests(list).at(-1)).toMatchObject({ projectId: RIVERSIDE_ID, inWarehouse: false });
  });

  it('goes back to anywhere by dropping the place from the address', async () => {
    const { harness, root, list, router } = await open({ url: `/stock?place=${RIVERSIDE_ID}` });

    await choose(harness, root, '#place-filter', '');

    expect(router.url).toBe('/stock');
    expect(requests(list).at(-1)).toMatchObject({ projectId: '', inWarehouse: false });
  });

  it('reads the place from a shared link and shows it chosen', async () => {
    const { root, list } = await open({ url: `/stock?place=${ANNEX_ID}&category=Tool` });

    expect(requests(list)[0]).toMatchObject({ projectId: ANNEX_ID, category: 'Tool' });
    expect(root.querySelector<HTMLSelectElement>('#place-filter')!.value).toBe(ANNEX_ID);
  });

  it('ignores a place that is neither the warehouse nor a project id', async () => {
    const { list } = await open({ url: '/stock?place=somewhere-else' });

    expect(requests(list)[0]).toMatchObject({ projectId: '', inWarehouse: false });
  });

  it('keeps a project that is no longer listed visible instead of showing "Anywhere"', async () => {
    const unknown = '99999999-9999-4999-8999-999999999999';
    const { root } = await open({ url: `/stock?place=${unknown}` });

    expect(texts(root, '#place-filter option')).toContain('A project that is no longer listed');
    expect(root.querySelector<HTMLSelectElement>('#place-filter')!.value).toBe(unknown);
  });
});

describe('StockPage retired items', () => {
  it('lists active items and keeps retired ones behind "Show retired"', async () => {
    const { root } = await open();

    const toggle = root.querySelector<HTMLInputElement>('.toggle input')!;
    expect(toggle.checked).toBe(false);
    expect(text(root, '.toggle')).toBe('Show retired');
    expect(root.textContent).not.toContain('Only retired items are listed.');
  });

  it('shows retired items when asked, says only they are listed, and writes it to the address', async () => {
    const { harness, root, list, router } = await open();

    root.querySelector<HTMLInputElement>('.toggle input')!.click();
    await settle(harness.fixture);

    expect(paramsOf(router.url)).toEqual({ retired: '1' });
    expect(requests(list).at(-1)).toMatchObject({ status: 'Retired', page: 1 });
    expect(root.textContent).toContain('Only retired items are listed.');
    expect(root.querySelector<HTMLInputElement>('.toggle input')!.checked).toBe(true);
  });

  it('reads the retired switch from a shared link', async () => {
    const { root, list } = await open({ url: '/stock?retired=1', result: pageOf([retired]) });

    expect(requests(list)[0]).toMatchObject({ status: 'Retired' });
    expect(root.querySelector<HTMLInputElement>('.toggle input')!.checked).toBe(true);
    expect(text(root, 'tbody th a')).toBe('Hand saw');
  });
});

describe('StockPage paging, search and failures', () => {
  it('pages on the server: Next asks the API for page 2 and writes it to the address', async () => {
    const { harness, root, list, router } = await open({
      result: pageOf([listItemOf(hammers())], 60),
    });
    expect(text(root, '.pager__summary')).toBe('Showing 1 to 25 of 60 items');

    buttonNamed(root, 'Next').click();
    await settle(harness.fixture);

    expect(paramsOf(router.url)).toEqual({ page: '2' });
    expect(requests(list).at(-1)).toMatchObject({ page: 2, pageSize: 25 });
  });

  it('searches after a pause in typing', async () => {
    const { harness, root, list, router } = await open();
    const before = requests(list).length;

    vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout'] });
    try {
      type(root, 'input[type="search"]', 'komatsu');
      vi.advanceTimersByTime(299);
      expect(requests(list)).toHaveLength(before);
      vi.advanceTimersByTime(1);
    } finally {
      vi.useRealTimers();
    }
    await settle(harness.fixture);

    expect(requests(list).at(-1)).toMatchObject({ search: 'komatsu', page: 1 });
    expect(paramsOf(router.url)).toEqual({ search: 'komatsu' });
  });

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
                  traceId: 'ref-31',
                }),
            )
          : of(pageOf([listItemOf(hammers())])),
    });

    expect(text(root, 'app-api-error-alert')).toContain('Reference: ref-31');
    expect(root.querySelector('table')).toBeNull();

    fail = false;
    buttonNamed(root, 'Try again').click();
    await settle(harness.fixture);

    expect(list.mock.calls.length).toBeGreaterThan(1);
    expect(root.querySelectorAll('tbody tr')).toHaveLength(1);
  });
});
