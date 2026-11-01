import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ChangeDetectionStrategy, Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter, withComponentInputBinding } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { Observable, Subject, of, throwError } from 'rxjs';
import type { Mock } from 'vitest';
import { ApiError } from '../../core/api/api-error';
import { ToastService } from '../../core/notifications/toast-service';
import { SessionState } from '../../core/session/session-state';
import { Viewport } from '../../shared/ui/viewport/viewport';
import { buttonNamed, flushMicrotasks, settle, text, texts, type } from '../../testing/dom';
import { VIEWER_PERMISSIONS, pageOf, signInForTest } from '../../testing/session-fixture';
import { InventoryApi } from './inventory-api';
import {
  HAMMER_HISTORY,
  PROJECT_LOOKUP,
  RIVERSIDE_ID,
  conflict,
  excavator,
  hammers,
  movement,
  onProject,
  ruleViolation,
  warehouse,
} from './inventory.fixtures';
import { InventoryItem, StockMovement } from './inventory.models';
import { ItemDetailPage } from './item-detail-page';
import { NOT_REVERSIBLE } from './movement-presentation';

@Component({ template: '', changeDetection: ChangeDetectionStrategy.OnPush })
class Blank {}

type Call = (...args: unknown[]) => Observable<unknown>;
const stub = (implementation: Call): Mock<Call> => vi.fn<Call>(implementation);

interface Options {
  readonly item?: InventoryItem;
  readonly history?: readonly StockMovement[];
  readonly historyTotal?: number;
  readonly permissions?: readonly string[];
  readonly tablet?: boolean;
  readonly url?: string;
  readonly get?: Mock<Call>;
  readonly api?: Record<string, Mock<Call>>;
}

async function open(options: Options = {}) {
  const item = options.item ?? hammers();
  const get = options.get ?? stub(() => of(item));
  const history = options.history ?? HAMMER_HISTORY;
  const movements = stub((_id, page) =>
    of(pageOf(history, options.historyTotal ?? history.length, page as number)),
  );
  const api = {
    get,
    movements,
    projectLookup: stub(() => of(PROJECT_LOOKUP)),
    ...options.api,
  };
  TestBed.configureTestingModule({
    providers: [
      provideRouter(
        [
          { path: 'stock/:id', component: ItemDetailPage },
          { path: 'stock/:id/edit', component: Blank },
          { path: 'stock', component: Blank },
        ],
        withComponentInputBinding(),
      ),
      provideHttpClient(),
      provideHttpClientTesting(),
      { provide: InventoryApi, useValue: api },
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
  await harness.navigateByUrl(options.url ?? `/stock/${item.id}`, ItemDetailPage);
  await settle(harness.fixture);
  return {
    harness,
    root: harness.fixture.nativeElement as HTMLElement,
    api,
    get,
    movements,
    router: TestBed.inject(Router),
    toast: TestBed.inject(ToastService),
  };
}

type View = Awaited<ReturnType<typeof open>>;

const readOnly = { permissions: VIEWER_PERMISSIONS };
const buttonsIn = (root: HTMLElement) => texts(root, '.summary__actions .btn');
const openMore = async (view: View) => {
  buttonNamed(view.root, 'More').click();
  await settle(view.harness.fixture);
};
const menuItems = (root: HTMLElement) => texts(root, '.menu__panel .menu__item');
const rowsOf = (root: HTMLElement) => root.querySelectorAll('tbody tr');
const submit = async (view: View) => {
  view.root.querySelector<HTMLButtonElement>('dialog button[type="submit"]')!.click();
  await settle(view.harness.fixture);
  await flushMicrotasks(6);
  await settle(view.harness.fixture);
};
async function choosePlace(view: View, controlName: string, value: string) {
  const select = view.root.querySelector<HTMLSelectElement>(
    `dialog select[formControlName="${controlName}"]`,
  )!;
  select.value = value;
  select.dispatchEvent(new Event('change', { bubbles: true }));
  await settle(view.harness.fixture);
}

describe('ItemDetailPage: what it shows', () => {
  it('asks for the item and the first page of its history', async () => {
    const { get, movements } = await open();

    expect(get).toHaveBeenCalledWith('item-hammer');
    expect(movements).toHaveBeenCalledWith('item-hammer', 1, 25);
  });

  it('shows what it is, the total, and where it is as chips with the unit', async () => {
    const { root } = await open();

    expect(texts(root, '.summary__info app-status-chip')).toEqual(['Tool', 'Active']);
    expect(text(root, '.summary__info')).toContain('Stanley');
    expect(text(root, '.total')).toBe('24 pc in total');
    expect(texts(root, '#where-heading ~ div .place')).toEqual([
      'Warehouse 14 pc',
      'Riverside Residences 10 pc',
    ]);
    expect(text(root, '.card')).toContain('₱450.00 each, including VAT');
    expect(root.querySelector('app-alert')).toBeNull();
  });

  it('shows the serial number of a single piece, and no zero or empty chips for an item with nothing left', async () => {
    const { root } = await open({ item: excavator() });
    expect(text(root, '.summary__info')).toContain('Komatsu · Serial PC200-8-A41127');
    expect(text(root, '.total')).toBe('1 unit in total');

    TestBed.resetTestingModule();
    const empty = await open({
      item: hammers({ totalQuantity: 0, balances: [] }),
    });
    expect(empty.root.querySelector('.total')).toBeNull();
    expect(text(empty.root, '.places__none')).toBe('No stock anywhere');
  });

  it('keeps purchase details one step away, in a disclosure', async () => {
    const { root } = await open();

    const details = root.querySelector('details')!;
    expect(text(details, 'summary')).toBe('Purchase details');
    expect(texts(details, 'dt')).toEqual(['Purchased', 'Supplier', 'Invoice number']);
    expect(texts(details, 'dd')).toEqual(['1 Jun 2026', 'Hardware Hub', 'HH-1001']);
  });

  it('says plainly when the item cannot be found, and offers a way back instead of a retry', async () => {
    const get = stub(() =>
      throwError(
        () =>
          new ApiError({
            status: 404,
            title: 'Not found',
            detail: "We couldn't find that item.",
            code: 'not_found',
          }),
      ),
    );
    const { root } = await open({ get });

    expect(text(root, 'app-api-error-alert')).toContain("We couldn't find that item.");
    expect(root.textContent).not.toContain('Try again');
    expect(buttonNamed(root, 'Back to stock').getAttribute('href')).toBe('/stock');
  });
});

describe('ItemDetailPage: the history', () => {
  const full: readonly StockMovement[] = [
    movement({
      id: 'm-charged',
      quantity: 4,
      chargedAmountMinor: 180000,
      note: 'For the slab pour',
      date: '2026-09-02',
    }),
    movement({
      id: 'm-reversal',
      type: 'Reversal',
      fromName: 'Riverside Residences',
      toName: 'Warehouse',
      quantity: 6,
      date: '2026-08-20',
      note: 'Wrong site',
      reversalOfMovementId: 'm-wrong',
    }),
    movement({
      id: 'm-wrong',
      quantity: 6,
      date: '2026-08-19',
      isReversed: true,
      toName: 'Harbor Warehouse',
    }),
    movement({
      id: 'm-count',
      type: 'Adjustment',
      fromName: 'Warehouse',
      toName: null,
      quantity: 2,
      date: '2026-08-10',
      note: 'Found two broken',
    }),
    ...HAMMER_HISTORY.slice(1),
  ];

  it('lists each line newest first: type, from and to by name, quantity with unit, who, note and the charge', async () => {
    const { root } = await open({ history: full });

    const rows = rowsOf(root);
    expect(rows).toHaveLength(5);
    expect(text(rows[0]!, 'th')).toBe('2 Sep 2026');
    expect(text(rows[0]!, 'app-status-chip')).toBe('Transfer');
    expect(text(rows[0]!, '.route')).toBe('From Warehouse → to Riverside Residences');
    expect(text(rows[0]!, 'td.num')).toBe('4 pc');
    expect(texts(rows[0]!, 'td')[2]).toBe('Wilson Warehouse-Keeper');
    expect(texts(rows[0]!, '.cell-sub')).toEqual([
      'Charged ₱1,800.00 to Riverside Residences',
      'Note: For the slab pour',
    ]);
    // A count and a purchase name the side outside the system by what it is.
    expect(text(rows[3]!, 'app-status-chip')).toBe('Count');
    expect(text(rows[3]!, '.route')).toBe('From Warehouse → to Stock count');
    expect(text(rows[4]!, 'app-status-chip')).toBe('Purchase');
    expect(text(rows[4]!, '.route')).toBe('From Supplier → to Warehouse');
  });

  it('marks a line that was reversed, so nobody has to guess', async () => {
    const { root } = await open({ history: full });

    expect(texts(rowsOf(root)[2]!, 'app-status-chip')).toEqual(['Transfer', 'Reversed']);
    expect(texts(rowsOf(root)[0]!, 'app-status-chip')).toEqual(['Transfer']);
  });

  it('shows read-only people the history, with the Reversed marker, and nothing to click', async () => {
    const { root } = await open({ history: full, ...readOnly });

    expect(rowsOf(root)).toHaveLength(5);
    expect(texts(rowsOf(root)[2]!, 'app-status-chip')).toEqual(['Transfer', 'Reversed']);
    expect(root.querySelector('tbody .btn')).toBeNull();
    expect(root.querySelector('.why-not')).toBeNull();
    expect(root.querySelector('thead th .visually-hidden')).toBeNull();
  });

  it('shows cards on a phone, with the same words', async () => {
    const { root } = await open({ history: full, tablet: false });

    expect(root.querySelector('table')).toBeNull();
    const cards = root.querySelectorAll('.row-card');
    expect(cards).toHaveLength(5);
    expect(text(cards[0]!, 'app-status-chip')).toBe('Transfer');
    expect(text(cards[0]!, '.route')).toBe('From Warehouse → to Riverside Residences');
    expect(text(cards[0]!, '.row-card__quantity')).toBe('4 pc');
    expect(cards[0]!.textContent).toContain('Charged ₱1,800.00 to Riverside Residences');
    expect(cards[0]!.textContent).toContain('Recorded by Wilson Warehouse-Keeper');
    expect(texts(cards[2]!, 'app-status-chip')).toEqual(['Transfer', 'Reversed']);
  });

  it('offers Reverse only on lines that can be reversed, and says why on the ones that cannot', async () => {
    const { root } = await open({ history: full });

    const rows = rowsOf(root);
    // The charged transfer, the count and the purchase can be reversed.
    for (const index of [0, 3, 4]) expect(rows[index]!.querySelector('.btn')).not.toBeNull();
    // A reversal cannot be reversed, and says so in plain words.
    expect(rows[1]!.querySelector('.btn')).toBeNull();
    expect(text(rows[1]!, '.why-not')).toBe(NOT_REVERSIBLE.reversal);
    // A line that was already reversed carries its marker and no button.
    expect(rows[2]!.querySelector('.btn')).toBeNull();
  });

  it('names each Reverse button after its line, for screen readers', async () => {
    const { root } = await open({ history: full });

    expect(rowsOf(root)[0]!.querySelector('.btn')!.getAttribute('aria-label')).toBe(
      'Reverse the transfer of 4 pc on 2 Sep 2026',
    );
  });

  it('pages on the server and keeps the page in the address', async () => {
    const view = await open({ historyTotal: 60 });
    expect(text(view.root, '.pager__summary')).toBe('Showing 1 to 25 of 60 movements');

    buttonNamed(view.root, 'Next').click();
    await settle(view.harness.fixture);

    expect(view.router.url).toBe('/stock/item-hammer?page=2');
    expect(view.movements).toHaveBeenLastCalledWith('item-hammer', 2, 25);
  });
});

describe('ItemDetailPage: who can act', () => {
  it('leads people who manage stock with Move stock, and folds the rest under More', async () => {
    const view = await open();

    expect(buttonsIn(view.root)).toEqual(['Move stock', 'More']);
    await openMore(view);
    expect(menuItems(view.root)).toEqual([
      'Receive more stock',
      'Count stock',
      'Write off',
      'Retire item',
      'Edit details',
    ]);
    const edit = view.root.querySelector<HTMLAnchorElement>('.menu__item[href]')!;
    expect(edit.getAttribute('href')).toBe('/stock/item-hammer/edit');
  });

  it('never offers to restock a single piece', async () => {
    const view = await open({ item: excavator() });

    await openMore(view);
    expect(menuItems(view.root)).toEqual([
      'Count stock',
      'Write off',
      'Retire item',
      'Edit details',
    ]);
  });

  it('shows people who can only read stock no action at all', async () => {
    const { root } = await open({ ...readOnly });

    expect(root.querySelector('.summary__actions')).toBeNull();
    expect(root.querySelector('app-menu')).toBeNull();
    expect(texts(root, 'button')).not.toContain('Move stock');
    expect(text(root, '.total')).toBe('24 pc in total');
    expect(rowsOf(root)).toHaveLength(2);
  });
});

describe('ItemDetailPage: a retired item', () => {
  const retired = excavator({
    status: 'Retired',
    retiredReason: 'Engine failed beyond repair',
    totalQuantity: 0,
    balances: [],
    version: 7,
  });
  const writeOff = movement({
    id: 'm-writeoff',
    type: 'WriteOff',
    fromName: 'Riverside Residences',
    toName: null,
    quantity: 1,
    date: '2026-09-01',
    note: 'Engine failed beyond repair',
  });
  const history = [
    writeOff,
    movement({
      id: 'm-purchase',
      type: 'Purchase',
      fromName: null,
      toName: 'Warehouse',
      quantity: 1,
    }),
  ];

  it('says it is retired and why, and offers no movement or edit action', async () => {
    const { root } = await open({ item: retired, history });

    expect(texts(root, '.summary__info app-status-chip')).toEqual(['Machinery', 'Retired']);
    expect(text(root, 'app-alert .alert__title')).toBe("It can't be moved, counted or edited");
    expect(text(root, 'app-alert')).toContain('Reason: Engine failed beyond repair.');
    expect(root.querySelector('.summary__actions')).toBeNull();
    expect(root.querySelector('app-menu')).toBeNull();
    expect(root.textContent).not.toContain('Move stock');
    expect(root.textContent).not.toContain('Edit details');
  });

  it('still lets its write-off be reversed, and explains why the other lines cannot be', async () => {
    const { root } = await open({ item: retired, history });

    const rows = rowsOf(root);
    expect(text(rows[0]!, '.btn')).toBe('Reverse');
    expect(rows[1]!.querySelector('.btn')).toBeNull();
    expect(text(rows[1]!, '.why-not')).toBe(NOT_REVERSIBLE.retired);
    expect(text(root, 'app-alert')).toContain(
      'Only its write-off can be reversed, from the history below.',
    );
  });

  it('shows a reader the retirement without the hint about reversing', async () => {
    const { root } = await open({ item: retired, history, ...readOnly });

    expect(texts(root, '.summary__info app-status-chip')).toEqual(['Machinery', 'Retired']);
    expect(text(root, 'app-alert')).toContain("It can't be moved, counted or edited");
    expect(text(root, 'app-alert')).not.toContain('Only its write-off can be reversed');
    expect(root.querySelector('tbody .btn')).toBeNull();
  });
});

describe('ItemDetailPage: after an action', () => {
  const moved = hammers({
    version: 4,
    balances: [warehouse(10), onProject(14)],
  });

  it("takes the server's answer as the item and fetches the history again, so the page agrees with itself", async () => {
    const transfer = stub(() => of(moved));
    const view = await open({ api: { transfer }, history: HAMMER_HISTORY });
    expect(view.movements).toHaveBeenCalledTimes(1);

    buttonNamed(view.root, 'Move stock').click();
    await settle(view.harness.fixture);
    expect(text(view.root, 'dialog h2')).toBe('Move stock');
    await choosePlace(view, 'from', 'warehouse');
    await choosePlace(view, 'to', RIVERSIDE_ID);
    type(view.root, 'dialog app-number-input input', '4');
    await submit(view);

    expect(transfer).toHaveBeenCalledWith(
      'item-hammer',
      expect.objectContaining({ quantity: 4, version: 3 }),
    );
    // The balances come from the answer, not from a second request for the item ...
    expect(texts(view.root, '#where-heading ~ div .place')).toEqual([
      'Warehouse 10 pc',
      'Riverside Residences 14 pc',
    ]);
    expect(view.get).toHaveBeenCalledTimes(1);
    // ... and the history is fetched again from its newest line.
    expect(view.movements).toHaveBeenCalledTimes(2);
    expect(view.movements).toHaveBeenLastCalledWith('item-hammer', 1, 25);
    expect(view.root.querySelector('dialog')).toBeNull();
    expect(view.toast.toasts().at(-1)?.message).toBe('Stock moved.');
  });

  it('never shows the new balances beside the old history: both change together, and the dialog stays busy until then', async () => {
    const newest = movement({ id: 'move-new', quantity: 4, note: 'Fourth move' });
    const gate = new Subject<ReturnType<typeof pageOf<StockMovement>>>();
    const transfer = stub(() => of(moved));
    const movements = stub(() => of(pageOf(HAMMER_HISTORY))).mockReturnValueOnce(
      of(pageOf(HAMMER_HISTORY)),
    );
    movements.mockReturnValueOnce(gate);
    const view = await open({ api: { transfer, movements } });

    buttonNamed(view.root, 'Move stock').click();
    await settle(view.harness.fixture);
    await choosePlace(view, 'from', 'warehouse');
    await choosePlace(view, 'to', RIVERSIDE_ID);
    type(view.root, 'dialog app-number-input input', '4');
    await submit(view);

    // The server has answered, but the newest history has not arrived: the page still shows the old state, and the dialog
    // is still there, busy, so the move cannot be sent twice.
    expect(transfer).toHaveBeenCalledTimes(1);
    expect(texts(view.root, '#where-heading ~ div .place')).toEqual([
      'Warehouse 14 pc',
      'Riverside Residences 10 pc',
    ]);
    expect(rowsOf(view.root)).toHaveLength(2);
    expect(view.root.querySelector('dialog')).not.toBeNull();
    expect(view.root.querySelector('dialog button[type="submit"]')!.getAttribute('aria-busy')).toBe(
      'true',
    );
    view.root.querySelector<HTMLButtonElement>('dialog button[type="submit"]')!.click();
    await settle(view.harness.fixture);
    expect(transfer).toHaveBeenCalledTimes(1);

    gate.next(pageOf([newest, ...HAMMER_HISTORY], 3));
    gate.complete();
    await settle(view.harness.fixture);
    await flushMicrotasks(4);
    await settle(view.harness.fixture);

    expect(texts(view.root, '#where-heading ~ div .place')).toEqual([
      'Warehouse 10 pc',
      'Riverside Residences 14 pc',
    ]);
    expect(rowsOf(view.root)).toHaveLength(3);
    expect(view.root.querySelector('dialog')).toBeNull();
  });

  it('shows the new item anyway when the newest history cannot be fetched, and the history says so itself', async () => {
    const transfer = stub(() => of(moved));
    const movements = stub(() => of(pageOf(HAMMER_HISTORY)));
    movements.mockReturnValueOnce(of(pageOf(HAMMER_HISTORY)));
    movements.mockReturnValue(
      throwError(
        () =>
          new ApiError({
            status: 500,
            title: 'Something went wrong',
            detail: "We couldn't finish that. Try again in a moment.",
            traceId: 'ref-3',
          }),
      ),
    );
    const view = await open({ api: { transfer, movements } });

    buttonNamed(view.root, 'Move stock').click();
    await settle(view.harness.fixture);
    await choosePlace(view, 'from', 'warehouse');
    await choosePlace(view, 'to', RIVERSIDE_ID);
    type(view.root, 'dialog app-number-input input', '4');
    await submit(view);
    await flushMicrotasks(4);
    await settle(view.harness.fixture);

    expect(texts(view.root, '#where-heading ~ div .place')).toEqual([
      'Warehouse 10 pc',
      'Riverside Residences 14 pc',
    ]);
    expect(view.root.querySelector('dialog')).toBeNull();
    expect(text(view.root, '#history-heading ~ app-api-error-alert')).toContain('Reference: ref-3');
  });

  it('sends the next action with the version of the answer, not the one the page opened with', async () => {
    const transfer = stub(() => of(moved));
    const adjust = stub(() => of(moved));
    const view = await open({ api: { transfer, adjust } });

    buttonNamed(view.root, 'Move stock').click();
    await settle(view.harness.fixture);
    await choosePlace(view, 'from', 'warehouse');
    await choosePlace(view, 'to', RIVERSIDE_ID);
    type(view.root, 'dialog app-number-input input', '4');
    await submit(view);

    await openMore(view);
    buttonNamed(view.root, 'Count stock').click();
    await settle(view.harness.fixture);
    await choosePlace(view, 'place', 'warehouse');
    type(view.root, 'dialog app-number-input input', '9');
    type(view.root, 'dialog textarea', 'Counted again');
    await submit(view);

    expect(adjust).toHaveBeenCalledWith('item-hammer', expect.objectContaining({ version: 4 }));
  });

  it('goes back to the newest history page after an action taken from a later page', async () => {
    const transfer = stub(() => of(moved));
    const view = await open({
      api: { transfer },
      historyTotal: 60,
      url: '/stock/item-hammer?page=2',
    });
    expect(view.movements).toHaveBeenLastCalledWith('item-hammer', 2, 25);

    buttonNamed(view.root, 'Move stock').click();
    await settle(view.harness.fixture);
    await choosePlace(view, 'from', 'warehouse');
    await choosePlace(view, 'to', RIVERSIDE_ID);
    type(view.root, 'dialog app-number-input input', '4');
    await submit(view);

    expect(view.router.url).toBe('/stock/item-hammer');
    expect(view.movements).toHaveBeenLastCalledWith('item-hammer', 1, 25);
  });

  it('reverses a line from its row: asks why, sends it, and shows the new state', async () => {
    const restored = hammers({ version: 4, balances: [warehouse(24)] });
    const reverse = stub(() => of(restored));
    const view = await open({ api: { reverse } });

    buttonNamed(view.root, 'Reverse').click();
    await settle(view.harness.fixture);
    expect(text(view.root, 'dialog h2')).toBe('Reverse this movement?');
    expect(text(view.root, 'dialog')).toContain(
      'Transfer: 10 pc, Warehouse to Riverside Residences',
    );
    type(view.root, 'dialog textarea', 'Moved to the wrong site');
    await submit(view);

    expect(reverse).toHaveBeenCalledWith(
      'item-hammer',
      'move-transfer',
      expect.objectContaining({ reason: 'Moved to the wrong site', version: 3 }),
    );
    expect(texts(view.root, '#where-heading ~ div .place')).toEqual(['Warehouse 24 pc']);
    expect(view.movements).toHaveBeenCalledTimes(2);
    expect(view.toast.toasts().at(-1)?.message).toBe('Movement reversed.');
  });

  it('keeps the dialog open and the page as it was when the server refuses', async () => {
    const message = 'Only 14 pc are there, but you asked for 15.';
    const transfer = stub(() => throwError(() => ruleViolation(message)));
    const view = await open({ api: { transfer } });

    buttonNamed(view.root, 'Move stock').click();
    await settle(view.harness.fixture);
    await choosePlace(view, 'from', 'warehouse');
    await choosePlace(view, 'to', RIVERSIDE_ID);
    type(view.root, 'dialog app-number-input input', '15');
    await submit(view);

    expect(text(view.root, 'dialog app-api-error-alert')).toContain(message);
    expect(texts(view.root, '#where-heading ~ div .place')).toEqual([
      'Warehouse 14 pc',
      'Riverside Residences 10 pc',
    ]);
    expect(view.movements).toHaveBeenCalledTimes(1);
  });

  it('on a conflict, Reload fetches the item and the history again and the dialog sends the fresh version', async () => {
    const fresh = hammers({
      version: 6,
      balances: [warehouse(12), onProject(10)],
      totalQuantity: 22,
    });
    const get = stub(() => of(fresh)).mockReturnValueOnce(of(hammers()));
    const transfer = stub(() => of(moved)).mockReturnValueOnce(throwError(() => conflict()));
    const view = await open({ get, api: { transfer } });

    buttonNamed(view.root, 'Move stock').click();
    await settle(view.harness.fixture);
    await choosePlace(view, 'from', 'warehouse');
    await choosePlace(view, 'to', RIVERSIDE_ID);
    type(view.root, 'dialog app-number-input input', '4');
    await submit(view);
    expect(text(view.root, 'dialog app-api-error-alert')).toContain(
      'Someone else changed this record while you were editing.',
    );

    buttonNamed(view.root, 'Reload item').click();
    await settle(view.harness.fixture);
    await flushMicrotasks(4);
    await settle(view.harness.fixture);

    expect(view.get).toHaveBeenCalledTimes(2);
    expect(view.movements).toHaveBeenCalledTimes(2);
    expect(texts(view.root, '#where-heading ~ div .place')).toEqual([
      'Warehouse 12 pc',
      'Riverside Residences 10 pc',
    ]);
    // The dialog is still open, its alert is gone and it now shows what is really in the warehouse.
    expect(view.root.querySelector('dialog app-api-error-alert .alert')).toBeNull();
    expect(texts(view.root, 'dialog select[formControlName="from"] option')).toContain(
      'Warehouse (12 pc)',
    );

    await submit(view);
    expect(transfer).toHaveBeenLastCalledWith(
      'item-hammer',
      expect.objectContaining({ version: 6 }),
    );
  });

  it('explains what to do first when someone tries to retire an item that still has stock', async () => {
    const view = await open();

    await openMore(view);
    buttonNamed(view.root, 'Retire item').click();
    await settle(view.harness.fixture);

    expect(text(view.root, 'dialog h2')).toBe('Retire this item?');
    expect(text(view.root, 'dialog app-alert')).toContain('Stock is still on record (24 pc).');
    expect(view.root.querySelector('dialog form')).toBeNull();
  });
});
