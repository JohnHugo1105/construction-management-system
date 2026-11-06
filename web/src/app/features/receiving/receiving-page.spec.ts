import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ChangeDetectionStrategy, Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { of, throwError } from 'rxjs';
import { ApiError } from '../../core/api/api-error';
import { Permission } from '../../core/session/permissions';
import { SessionState } from '../../core/session/session-state';
import { buttonNamed, settle, text, texts, type } from '../../testing/dom';
import { signInForTest } from '../../testing/session-fixture';
import { ReceivingApi } from './receiving-api';
import { ReceivingPage } from './receiving-page';
import { listItem } from './receiving.fixtures';
import { ReceivingListParams, ReceivingOrderListItem } from './receiving.models';

@Component({ template: '', changeDetection: ChangeDetectionStrategy.OnPush })
class Blank {}

async function open(
  options: { items?: ReceivingOrderListItem[]; failure?: boolean; url?: string } = {},
) {
  const listOrders = vi.fn((params: ReceivingListParams) => {
    void params;
    return options.failure
      ? throwError(() => new ApiError({ status: 500, title: 'Something went wrong', detail: 'Try again in a moment.' }))
      : of({
          items: options.items ?? [
            listItem(),
            listItem({
              id: 'order-2',
              number: 'PO-0002',
              supplierName: 'Cebu Hardware',
              projectName: 'Harbor Warehouse',
              subProjectName: 'Block B',
              expectedDate: null,
              deliveryState: 'PartlyReceived',
            }),
          ],
          orderCount: options.items?.length ?? 2,
          page: 1,
          pageSize: 25,
        });
  });
  TestBed.configureTestingModule({
    providers: [
      provideRouter([
        { path: 'receiving', component: ReceivingPage },
        { path: 'receiving/:id', component: Blank },
      ]),
      provideHttpClient(),
      provideHttpClientTesting(),
      { provide: ReceivingApi, useValue: { listOrders } },
    ],
  });
  await signInForTest(TestBed.inject(SessionState), TestBed.inject(HttpTestingController), {
    permissions: [Permission.projectsRead, Permission.receivingRecord],
    roles: ['WarehouseKeeper'],
  });
  const harness = await RouterTestingHarness.create();
  await harness.navigateByUrl(options.url ?? '/receiving', ReceivingPage);
  await settle(harness.fixture);
  return { harness, root: harness.fixture.nativeElement as HTMLElement, listOrders };
}

describe('ReceivingPage', () => {
  it('shows one card per order awaiting delivery: who, where, when, and one way in', async () => {
    const { root } = await open();

    const cards = root.querySelectorAll('.row-card');
    expect(cards).toHaveLength(2);
    expect(text(cards[0]!, '.row-card__title')).toBe('Visayas Steel Trading');
    expect(text(cards[0]!, '.chip')).toBe('Nothing delivered yet');
    expect(cards[0]!.textContent).toContain('PO-0001 · Riverside Residences');
    expect(cards[0]!.textContent).toContain('Expected 5 Nov 2026');
    expect(texts(cards[0]!, 'a, button')).toEqual(['Open']);
    expect(cards[0]!.querySelector('a')!.getAttribute('href')).toBe('/receiving/order-1');

    expect(text(cards[1]!, '.chip')).toBe('Partly delivered');
    expect(cards[1]!.textContent).toContain('PO-0002 · Harbor Warehouse, Block B');
    expect(cards[1]!.textContent).toContain('No date given');
  });

  it('gives each Open link a name that says which order it opens', async () => {
    const { root } = await open();

    expect(root.querySelector('.row-card a')!.getAttribute('aria-label')).toBe(
      'Open PO-0001 from Visayas Steel Trading',
    );
  });

  it('shows no amount of money anywhere on the page', async () => {
    const { root } = await open();

    expect(root.textContent).not.toMatch(/₱|PHP|VAT|total|price|cost/i);
  });

  it('asks the server for the first page, and for a search when one is typed', async () => {
    const { harness, root, listOrders } = await open();

    expect(listOrders.mock.calls[0]![0]).toEqual({ search: '', page: 1, pageSize: 25 });
    vi.useFakeTimers();
    try {
      type(root, 'app-search-box input', 'steel');
      vi.advanceTimersByTime(400);
    } finally {
      vi.useRealTimers();
    }
    await settle(harness.fixture);

    expect(listOrders.mock.calls.at(-1)![0]).toMatchObject({ search: 'steel' });
  });

  it('says nothing is waiting, and why, when no order is awaiting delivery', async () => {
    const { root } = await open({ items: [] });

    expect(text(root, 'app-empty-state')).toContain('Nothing is waiting to be delivered');
    expect(text(root, 'app-empty-state')).toContain(
      'once the office approves it, and stays until everything on it has arrived',
    );
    expect(root.querySelector('app-search-box')).toBeNull();
    expect(root.querySelector('.row-card')).toBeNull();
  });

  it('says no delivery matches a search and offers a way back to every delivery', async () => {
    const { root, harness, listOrders } = await open({
      items: [],
      url: '/receiving?search=nothing',
    });

    expect(text(root, 'app-empty-state')).toContain('No deliveries match');
    expect(root.querySelector('app-search-box')).not.toBeNull();
    buttonNamed(root, 'Show every delivery').click();
    await settle(harness.fixture);
    expect(listOrders.mock.calls.at(-1)![0]).toEqual({ search: '', page: 1, pageSize: 25 });
  });

  it('says what happened when the list can’t be loaded, and offers to try again', async () => {
    const { root } = await open({ failure: true });

    expect(text(root, 'app-api-error-alert')).toContain('Something went wrong');
    expect(texts(root, 'app-api-error-alert button')).toContain('Try again');
    expect(root.querySelector('app-empty-state')).toBeNull();
  });
});
