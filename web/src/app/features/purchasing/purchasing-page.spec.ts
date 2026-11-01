import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ChangeDetectionStrategy, Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { of } from 'rxjs';
import { Permission } from '../../core/session/permissions';
import { SessionState } from '../../core/session/session-state';
import { Viewport } from '../../shared/ui/viewport/viewport';
import { buttonNamed, settle, text, texts } from '../../testing/dom';
import { pageOf, signInForTest } from '../../testing/session-fixture';
import { PurchasingApi } from './purchasing-api';
import { PurchasingPage } from './purchasing-page';
import { RIVERSIDE_ID, listItem } from './purchasing.fixtures';
import { PurchaseOrderListParams } from './purchasing.models';

@Component({ template: '', changeDetection: ChangeDetectionStrategy.OnPush })
class Blank {}

async function open(options: { items?: ReturnType<typeof listItem>[]; permissions?: readonly string[]; tablet?: boolean; url?: string } = {}) {
  const listOrders = vi.fn((params: PurchaseOrderListParams) => {
    void params;
    return of(pageOf(options.items ?? [listItem(), listItem({ id: 'order-2', number: 'PO-0002', status: 'Draft', supplierName: 'Tilemart Cebu', grossMinor: 158_000_000 })]));
  });
  const projectLookup = vi.fn(() =>
    of([
      { id: RIVERSIDE_ID, name: 'Riverside Residences', status: 'Active', subProjects: [] },
      { id: 'p2', name: 'Harbor Warehouse', status: 'Active', subProjects: [] },
    ]),
  );
  TestBed.configureTestingModule({
    providers: [
      provideRouter([
        { path: 'purchasing', component: PurchasingPage },
        { path: 'purchasing/orders/new', component: Blank },
        { path: 'purchasing/orders/:id', component: Blank },
        { path: 'purchasing/suppliers', component: Blank },
      ]),
      provideHttpClient(),
      provideHttpClientTesting(),
      { provide: PurchasingApi, useValue: { listOrders, projectLookup } },
      { provide: Viewport, useValue: { isTablet: signal(options.tablet ?? true), isDesktop: signal(true) } },
    ],
  });
  await signInForTest(TestBed.inject(SessionState), TestBed.inject(HttpTestingController), options.permissions ? { permissions: options.permissions } : {});
  const harness = await RouterTestingHarness.create();
  await harness.navigateByUrl(options.url ?? '/purchasing', PurchasingPage);
  await settle(harness.fixture);
  return { harness, root: harness.fixture.nativeElement as HTMLElement, listOrders };
}

const READER = [Permission.purchasingRead, Permission.projectsRead];

describe('PurchasingPage', () => {
  it('lists each order with its number, supplier, project, status and total with VAT', async () => {
    const { root } = await open();

    const rows = root.querySelectorAll('tbody tr');
    expect(rows).toHaveLength(2);
    expect(text(rows[0]!, 'th a')).toBe('PO-0001');
    expect(texts(rows[0]!, 'td')).toEqual(['Visayas Steel Trading', 'Riverside Residences', '20 Oct 2026', 'Approved', '₱5,000.00']);
    expect(texts(rows[1]!, 'td')[3]).toBe('Draft');
    expect(texts(rows[1]!, 'td')[4]).toBe('₱1,580,000.00');
    expect(rows[0]!.querySelector('a')!.getAttribute('href')).toBe('/purchasing/orders/order-1');
  });

  it('shows cards on a phone', async () => {
    const { root } = await open({ tablet: false });

    expect(root.querySelector('table')).toBeNull();
    expect(root.querySelectorAll('.row-card')).toHaveLength(2);
    expect(text(root.querySelector('.row-card')!, '.row-card__title')).toBe('PO-0001 · Visayas Steel Trading');
  });

  it('asks for the first page of every order, and for one status when a chip is pressed', async () => {
    const { harness, root, listOrders } = await open();

    expect(listOrders.mock.calls[0]![0]).toMatchObject({ status: '', projectId: '', search: '', page: 1, pageSize: 25 });
    buttonNamed(root, 'Draft').click();
    await settle(harness.fixture);

    expect(listOrders.mock.calls.at(-1)![0]).toMatchObject({ status: 'Draft' });
  });

  it('gives people who draft orders the one next step, and nobody else', async () => {
    const manager = await open();
    expect(buttonNamed(manager.root.querySelector('.toolbar')!, 'New purchase order').getAttribute('href')).toBe('/purchasing/orders/new');
    TestBed.resetTestingModule();

    const reader = await open({ permissions: READER });
    expect(reader.root.textContent).not.toContain('New purchase order');
  });

  it('invites a drafter to raise the first order, and tells everyone else who can', async () => {
    const drafter = await open({ items: [] });
    expect(drafter.root.textContent).toContain('No purchase orders yet');
    expect(drafter.root.textContent).toContain('Raise a purchase order to a supplier');
    TestBed.resetTestingModule();

    const reader = await open({ items: [], permissions: READER });
    expect(reader.root.textContent).toContain('A cost engineer or accountant can raise the first purchase order.');
  });

  it('offers the firms tab only to people who may read firms', async () => {
    const withFirms = await open({ permissions: [...READER, Permission.suppliersRead] });
    expect(texts(withFirms.root, '.tab')).toEqual(['Purchase orders', 'Suppliers']);
    TestBed.resetTestingModule();

    const without = await open({ permissions: READER });
    expect(without.root.querySelector('.tab')).toBeNull();
  });
});
