import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Observable, of, throwError } from 'rxjs';
import type { Mock } from 'vitest';
import { SessionState } from '../../core/session/session-state';
import { settle, text, texts, type } from '../../testing/dom';
import { signInForTest } from '../../testing/session-fixture';
import { ApiError } from '../../core/api/api-error';
import { ruleViolation, validationError } from '../workforce/workforce.fixtures';
import { PurchasingApi } from './purchasing-api';
import { approvedOrder, orderLine } from './purchasing.fixtures';
import { PurchaseOrder } from './purchasing.models';
import { RecordDeliveryDialog } from './record-delivery-dialog';

type Call = (...args: unknown[]) => Observable<unknown>;
const stub = (implementation: Call): Mock<Call> => vi.fn<Call>(implementation);

/** Two lines: rebar (10 ordered, 4 already received) and tie wire (3 ordered, none yet), and a third that has all arrived. */
const ORDER: PurchaseOrder = approvedOrder({
  lines: [
    orderLine({ id: 'line-1', lineNumber: 1, receivedQuantity: 4, toReceiveQuantity: 6 }),
    orderLine({
      id: 'line-2',
      lineNumber: 2,
      description: 'Tie wire',
      quantity: 3,
      unit: 'kg',
      toReceiveQuantity: 3,
    }),
    orderLine({
      id: 'line-3',
      lineNumber: 3,
      description: 'Cement',
      quantity: 5,
      unit: 'bag',
      receivedQuantity: 5,
      toReceiveQuantity: 0,
    }),
  ],
});

async function open(api: Record<string, Mock<Call>>) {
  TestBed.configureTestingModule({
    providers: [
      provideHttpClient(),
      provideHttpClientTesting(),
      { provide: PurchasingApi, useValue: api },
    ],
  });
  await signInForTest(TestBed.inject(SessionState), TestBed.inject(HttpTestingController));
  const fixture = TestBed.createComponent(RecordDeliveryDialog);
  fixture.componentRef.setInput('order', ORDER);
  const saved: PurchaseOrder[] = [];
  fixture.componentInstance.saved.subscribe((order) => saved.push(order));
  await settle(fixture);
  const root = fixture.nativeElement as HTMLElement;
  const submit = async () => {
    (root.querySelector('button[type="submit"]') as HTMLButtonElement).click();
    await settle(fixture);
  };
  return { fixture, root, saved, submit };
}

const quantities = (root: HTMLElement) =>
  Array.from(root.querySelectorAll<HTMLInputElement>('app-number-input input')).map(
    (input) => input.value,
  );

describe('RecordDeliveryDialog', () => {
  it('starts every line that still has something to come at what is left, and leaves out a line that has all arrived', async () => {
    const { root } = await open({});

    expect(texts(root, '.input-group__affix')).toEqual(['pc', 'kg']);
    expect(quantities(root)).toEqual(['6', '3']);
    expect(texts(root, '.field__label').filter((label) => label.startsWith('Line'))).toEqual([
      'Line 1: Rebar 16 mm',
      'Line 2: Tie wire',
    ]);
    expect(texts(root, '.field__hint')).toContain('Ordered 10, received 4, 6 left.');
    expect(texts(root, '.field__hint')).toContain('Ordered 3.');
    expect(root.textContent).not.toContain('Cement');
    expect(text(root, 'button[type="submit"]')).toBe('Record delivery');
  });

  it('says in the opening words that a delivery moves no money', async () => {
    const { root } = await open({});

    expect(text(root, 'p.text-muted')).toContain('This does not change what is committed');
  });

  it('sends the day, the supplier’s number, the note and only the lines that came, with a request id', async () => {
    const recordDelivery = stub(() => of(approvedOrder({ version: 5 })));
    const { root, saved, submit } = await open({ recordDelivery });

    type(root, 'input[formcontrolname="deliveryReceiptNumber"]', ' DR-0045 ');
    type(root, 'textarea[formcontrolname="note"]', 'Gate 2');
    const [first, second] = Array.from(
      root.querySelectorAll<HTMLInputElement>('app-number-input input'),
    );
    type(root, 'app-number-input input', '2.5');
    first!.dispatchEvent(new Event('blur'));
    second!.value = '0';
    second!.dispatchEvent(new Event('input', { bubbles: true }));
    await submit();

    expect(recordDelivery).toHaveBeenCalledTimes(1);
    expect(recordDelivery).toHaveBeenCalledWith(
      'order-1',
      {
        receivedDate: expect.stringMatching(/^\d{4}-\d{2}-\d{2}$/),
        deliveryReceiptNumber: 'DR-0045',
        note: 'Gate 2',
        lines: [{ purchaseOrderLineId: 'line-1', quantity: 2.5 }],
      },
      expect.any(String),
    );
    expect(saved).toHaveLength(1);
    expect(saved[0]!.version).toBe(5);
  });

  it('asks for something to be entered instead of sending a delivery of nothing', async () => {
    const recordDelivery = stub(() => of(approvedOrder()));
    const { root, submit } = await open({ recordDelivery });
    for (const input of Array.from(
      root.querySelectorAll<HTMLInputElement>('app-number-input input'),
    )) {
      input.value = '0';
      input.dispatchEvent(new Event('input', { bubbles: true }));
    }

    await submit();

    expect(recordDelivery).not.toHaveBeenCalled();
    expect(text(root, 'app-alert')).toContain('Enter how much arrived for at least one line.');
  });

  it('shows the server’s refusal of a line and of the number in words, keeps what was typed, and retries under the same request id', async () => {
    const recordDelivery = stub((...args) => {
      void args;
      return throwError(() =>
        validationError({
          lines: ['Line 1 (Rebar 16 mm): only 6 pc are left to receive. Enter 6 or less.'],
          deliveryReceiptNumber: ['This delivery receipt is already recorded.'],
        }),
      );
    });
    const { root, submit } = await open({ recordDelivery });
    type(root, 'input[formcontrolname="deliveryReceiptNumber"]', 'DR-0001');

    await submit();

    expect(text(root, '.field__error')).toBe('This delivery receipt is already recorded.');
    expect(text(root, 'app-api-error-alert')).toContain(
      'Line 1 (Rebar 16 mm): only 6 pc are left to receive. Enter 6 or less.',
    );
    expect(
      (root.querySelector('input[formcontrolname="deliveryReceiptNumber"]') as HTMLInputElement)
        .value,
    ).toBe('DR-0001');
    expect(quantities(root)).toEqual(['6', '3']);

    // The person fixes the number, which clears the message on that field, and tries again: the attempt is the same request.
    type(root, 'input[formcontrolname="deliveryReceiptNumber"]', 'DR-0002');
    await submit();
    expect(recordDelivery).toHaveBeenCalledTimes(2);
    const [firstCall, secondCall] = recordDelivery.mock.calls;
    expect(secondCall![2]).toBe(firstCall![2]);
  });

  it('says why in the API’s own words when the order is no longer open, and offers a reload when it changed under the person', async () => {
    const recordDelivery = stub(() => throwError(() => ruleViolation('This order is closed.')));
    const { root, fixture, submit } = await open({ recordDelivery });
    const reloaded = vi.fn();
    fixture.componentInstance.reloaded.subscribe(reloaded);

    await submit();
    expect(text(root, 'app-api-error-alert')).toContain('This order is closed.');
    expect(root.querySelector('app-api-error-alert button')).toBeNull();
    expect(root.querySelector('app-dialog')).not.toBeNull();

    recordDelivery.mockImplementation(() =>
      throwError(
        () =>
          new ApiError({
            status: 409,
            title: 'Someone else changed this',
            detail: 'Reload the order and try again.',
            code: 'concurrency_conflict',
          }),
      ),
    );
    await submit();
    (root.querySelector('app-api-error-alert button') as HTMLButtonElement).click();

    expect(reloaded).toHaveBeenCalledTimes(1);
  });
});
