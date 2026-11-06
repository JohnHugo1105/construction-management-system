import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ChangeDetectionStrategy, Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { Observable, of, throwError } from 'rxjs';
import type { Mock } from 'vitest';
import { ApiError } from '../../core/api/api-error';
import { validationError } from '../workforce/workforce.fixtures';
import { Permission } from '../../core/session/permissions';
import { SessionState } from '../../core/session/session-state';
import { buttonNamed, settle, text, texts, type } from '../../testing/dom';
import { signInForTest } from '../../testing/session-fixture';
import { ReceivingApi } from './receiving-api';
import { ReceivingOrderPage } from './receiving-order-page';
import {
  deliveryFixture,
  partlyDelivered,
  receivingLine,
  receivingOrder,
} from './receiving.fixtures';
import { ReceivingOrder } from './receiving.models';

@Component({ template: '', changeDetection: ChangeDetectionStrategy.OnPush })
class Blank {}

type Call = (...args: unknown[]) => Observable<unknown>;
const stub = (implementation: Call): Mock<Call> => vi.fn<Call>(implementation);

async function open(order: ReceivingOrder, api: Record<string, Mock<Call>> = {}) {
  const getOrder = stub(() => of(order));
  TestBed.configureTestingModule({
    providers: [
      provideRouter(
        [
          { path: 'receiving/:id', component: ReceivingOrderPage },
          { path: 'receiving', component: Blank },
        ],
        withComponentInputBinding(),
      ),
      provideHttpClient(),
      provideHttpClientTesting(),
      { provide: ReceivingApi, useValue: { getOrder, ...api } },
    ],
  });
  await signInForTest(TestBed.inject(SessionState), TestBed.inject(HttpTestingController), {
    permissions: [Permission.projectsRead, Permission.receivingRecord],
    roles: ['WarehouseKeeper'],
  });
  const harness = await RouterTestingHarness.create();
  await harness.navigateByUrl(`/receiving/${order.id}`, ReceivingOrderPage);
  await settle(harness.fixture);
  return { harness, root: harness.fixture.nativeElement as HTMLElement, getOrder };
}

const PARTLY = partlyDelivered({
  deliveryCount: 2,
  deliveries: [
    deliveryFixture({ id: 'delivery-2', deliveryReceiptNumber: null, note: 'Gate 2', version: 3 }),
    deliveryFixture({
      id: 'delivery-1',
      receivedDate: '2026-10-27',
      status: 'Void',
      canVoid: false,
      voidedByName: 'Alma Accountant',
      voidedAtUtc: '2026-10-29T03:00:00Z',
    }),
  ],
});

describe('ReceivingOrderPage', () => {
  it('leads an order with something left to come with Record delivery, and nothing else', async () => {
    const { root } = await open(receivingOrder());

    expect(texts(root, '.summary__actions .btn')).toEqual(['Record delivery']);
    expect(text(root, '.summary__actions .btn')).toBe('Record delivery');
    expect(root.querySelector('.summary__actions .btn--primary')).not.toBeNull();
    expect(text(root, '.summary__info')).toContain('Nothing delivered yet');
    expect(text(root, '.summary__info')).toContain('Expected 5 Nov 2026');
  });

  it('shows where it is going and the items with ordered, received and still to come, in quantities', async () => {
    const { root } = await open(PARTLY);

    expect(text(root, '[aria-labelledby="order-where"]')).toContain('Riverside Residences');
    expect(text(root, '[aria-labelledby="order-where"]')).toContain('QT-884');
    const items = root.querySelectorAll('[aria-labelledby="order-items"] .line');
    expect(items).toHaveLength(2);
    expect(text(items[0]!, '.line__description')).toBe('Rebar 16 mm');
    expect(items[0]!.textContent).toContain('Ordered 10 pc · Received 4 pc');
    expect(items[0]!.textContent).toContain('6 pc still to come');
    expect(items[1]!.textContent).toContain('3 kg still to come');
  });

  it('shows no amount of money anywhere on the page', async () => {
    const { root } = await open(PARTLY);

    expect(root.textContent).not.toMatch(/₱|PHP|VAT|price|cost|total|invoice|committed/i);
  });

  it('says an item that has all arrived is settled, and offers no delivery when everything is in', async () => {
    const { root } = await open(
      receivingOrder({
        deliveryState: 'FullyReceived',
        canRecord: false,
        lines: [receivingLine({ receivedQuantity: 10, remainingQuantity: 0 })],
      }),
    );

    expect(text(root, '.line__done')).toBe('All received');
    expect(root.querySelector('.summary__actions')).toBeNull();
    expect(text(root, '.summary__info')).toContain('Delivered in full');
  });

  it('lists the deliveries, newest first, with a voided one marked and no reason shown', async () => {
    const { root } = await open(PARTLY);

    const rows = root.querySelectorAll('[aria-labelledby="order-deliveries"] .line');
    expect(rows).toHaveLength(2);
    expect(text(rows[0]!, '.line__description')).toBe('28 Oct 2026');
    expect(text(rows[0]!, '.line__text')).toContain('Gate 2');
    expect(text(rows[0]!, '.line__text')).toContain('4 pc Rebar 16 mm');
    expect(text(rows[1]!, '.line__description')).toBe('27 Oct 2026 · DR-0012 Voided');
    expect(text(rows[1]!, '.line__text')).toContain('Voided by Alma Accountant');
    expect(text(rows[1]!, '.line__text')).not.toContain('Reason');
    // Only a delivery that still stands can be voided.
    expect(texts(rows[0]!, 'button')).toEqual(['Void delivery']);
    expect(rows[1]!.querySelector('button')).toBeNull();
  });

  it('shows nothing about deliveries before any exists', async () => {
    const { root } = await open(receivingOrder());

    expect(root.querySelector('[aria-labelledby="order-deliveries"]')).toBeNull();
  });

  it('says in words why a draft, a closed or a cancelled order takes no delivery, and shows no action', async () => {
    for (const [status, words] of [
      ['Draft', 'has not approved this order yet'],
      ['Closed', 'This order is closed'],
      ['Cancelled', 'This order was cancelled'],
    ] as const) {
      const { root } = await open(receivingOrder({ status, canRecord: false }));

      expect(text(root, 'app-alert')).toContain(words);
      expect(root.querySelector('.summary__actions')).toBeNull();
      TestBed.resetTestingModule();
    }
  });

  it('says to ask the office when the project is archived', async () => {
    const { root } = await open(receivingOrder({ canRecord: false }));

    expect(text(root, 'app-alert')).toContain('Ask the office to restore the project');
    expect(root.querySelector('.summary__actions')).toBeNull();
  });

  it('voids a delivery with a reason and the version it opened, then shows the order again', async () => {
    const voidDelivery = stub(() => of(receivingOrder()));
    const { harness, root, getOrder } = await open(PARTLY, { voidDelivery });

    buttonNamed(root, 'Void delivery').click();
    await settle(harness.fixture);
    expect(document.body.textContent).toContain('Void this delivery?');
    const dialog = document.querySelector('app-reason-dialog')!;
    type(dialog, 'textarea', 'Counted the wrong truck');
    (dialog.querySelector('button[type="submit"]') as HTMLButtonElement).click();
    await settle(harness.fixture);

    expect(voidDelivery).toHaveBeenCalledWith(
      'order-1',
      'delivery-2',
      'Counted the wrong truck',
      3,
    );
    expect(getOrder).toHaveBeenCalledTimes(2);
  });

  it('records a delivery from the dialog and shows the order the server sends back, without asking for it again', async () => {
    const recordDelivery = stub(() => of({ ...PARTLY, version: 9 }));
    const { harness, root, getOrder } = await open(receivingOrder(), { recordDelivery });

    buttonNamed(root, 'Record delivery').click();
    await settle(harness.fixture);
    expect(document.body.textContent).toContain('What the supplier brought for PO-0001');
    const submit = document.querySelector<HTMLButtonElement>(
      'app-receive-delivery-dialog button[type="submit"]',
    )!;
    submit.click();
    await settle(harness.fixture);

    expect(recordDelivery).toHaveBeenCalledWith(
      'order-1',
      expect.objectContaining({
        lines: [
          { purchaseOrderLineId: 'line-1', quantity: 10 },
          { purchaseOrderLineId: 'line-2', quantity: 3 },
        ],
      }),
      expect.any(String),
    );
    expect(getOrder).toHaveBeenCalledTimes(1);
    expect(root.querySelector('[aria-labelledby="order-deliveries"]')).not.toBeNull();
    expect(document.querySelector('app-receive-delivery-dialog')).toBeNull();
  });

  it('keeps the dialog open with what was typed when the server refuses the delivery', async () => {
    const recordDelivery = stub(() =>
      throwError(() =>
        validationError({ deliveryReceiptNumber: ['This delivery receipt is already recorded.'] }),
      ),
    );
    const { harness, root } = await open(receivingOrder(), { recordDelivery });

    buttonNamed(root, 'Record delivery').click();
    await settle(harness.fixture);
    const dialog = document.querySelector('app-receive-delivery-dialog')!;
    type(dialog, 'input[formcontrolname="deliveryReceiptNumber"]', 'DR-0001');
    (dialog.querySelector('button[type="submit"]') as HTMLButtonElement).click();
    await settle(harness.fixture);

    expect(document.querySelector('app-receive-delivery-dialog')).not.toBeNull();
    expect(text(dialog, '.field__error')).toBe('This delivery receipt is already recorded.');
    expect(
      (dialog.querySelector('input[formcontrolname="deliveryReceiptNumber"]') as HTMLInputElement)
        .value,
    ).toBe('DR-0001');
  });

  it('says what happened when the order can’t be opened, with a way back', async () => {
    const getOrder = stub(() =>
      throwError(() => new ApiError({ status: 404, title: 'Not found', detail: 'We couldn’t find that order.' })),
    );
    TestBed.configureTestingModule({
      providers: [
        provideRouter(
          [
            { path: 'receiving/:id', component: ReceivingOrderPage },
            { path: 'receiving', component: Blank },
          ],
          withComponentInputBinding(),
        ),
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: ReceivingApi, useValue: { getOrder } },
      ],
    });
    await signInForTest(TestBed.inject(SessionState), TestBed.inject(HttpTestingController));
    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl('/receiving/missing', ReceivingOrderPage);
    await settle(harness.fixture);
    const root = harness.fixture.nativeElement as HTMLElement;

    expect(root.querySelector('app-api-error-alert')).not.toBeNull();
    expect(buttonNamed(root, 'Back to receiving').getAttribute('href')).toBe('/receiving');
  });
});
