import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ReceivingApi } from './receiving-api';
import { partlyDelivered, receivingOrder } from './receiving.fixtures';

function setup() {
  TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
  return { api: TestBed.inject(ReceivingApi), http: TestBed.inject(HttpTestingController) };
}

describe('ReceivingApi', () => {
  afterEach(() => TestBed.inject(HttpTestingController).verify());

  it('lists the orders awaiting delivery, leaving out an empty search', () => {
    const { api, http } = setup();

    api.listOrders({ search: '', page: 2, pageSize: 25 }).subscribe();
    const plain = http.expectOne((request) => request.url === '/api/receiving/orders');
    expect(plain.request.method).toBe('GET');
    expect(plain.request.params.get('page')).toBe('2');
    expect(plain.request.params.get('pageSize')).toBe('25');
    expect(plain.request.params.has('search')).toBe(false);
    plain.flush({ items: [], orderCount: 0, page: 2, pageSize: 25 });

    api.listOrders({ search: 'steel', page: 1, pageSize: 25 }).subscribe();
    http
      .expectOne((request) => request.url === '/api/receiving/orders' && request.params.get('search') === 'steel')
      .flush({ items: [], orderCount: 0, page: 1, pageSize: 25 });
  });

  it('opens one order, records a delivery under a request id, and voids one with a reason and version', () => {
    const { api, http } = setup();

    api.getOrder('order-1').subscribe();
    http.expectOne('/api/receiving/orders/order-1').flush(receivingOrder());

    api
      .recordDelivery(
        'order-1',
        {
          receivedDate: '2026-11-01',
          deliveryReceiptNumber: 'DR-1',
          note: null,
          lines: [{ purchaseOrderLineId: 'line-1', quantity: 4 }],
        },
        'request-1',
      )
      .subscribe();
    const record = http.expectOne('/api/receiving/orders/order-1/deliveries');
    expect(record.request.method).toBe('POST');
    expect(record.request.body).toEqual({
      receivedDate: '2026-11-01',
      deliveryReceiptNumber: 'DR-1',
      note: null,
      lines: [{ purchaseOrderLineId: 'line-1', quantity: 4 }],
      clientRequestId: 'request-1',
    });
    record.flush(partlyDelivered());

    api.voidDelivery('order-1', 'delivery-1', 'Counted the wrong truck', 2).subscribe();
    const voided = http.expectOne('/api/receiving/orders/order-1/deliveries/delivery-1/void');
    expect(voided.request.body).toEqual({ reason: 'Counted the wrong truck', version: 2 });
    voided.flush(receivingOrder());
  });

  it('calls only /api/receiving/... and never a purchase order endpoint, where the prices are', () => {
    const { api, http } = setup();

    api.listOrders({ search: 'x', page: 1, pageSize: 25 }).subscribe();
    api.getOrder('o').subscribe();
    api
      .recordDelivery('o', { receivedDate: '2026-11-01', deliveryReceiptNumber: null, note: null, lines: [] }, 'r')
      .subscribe();
    api.voidDelivery('o', 'd', 'why', 1).subscribe();

    const requests = http.match(() => true);
    expect(requests).toHaveLength(4);
    for (const request of requests) {
      expect(request.request.url.startsWith('/api/receiving/')).toBe(true);
      expect(request.request.url).not.toContain('purchase-orders');
      request.flush(null);
    }
  });
});
