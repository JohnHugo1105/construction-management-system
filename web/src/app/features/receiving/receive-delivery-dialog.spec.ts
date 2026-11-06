import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Observable, of, throwError } from 'rxjs';
import type { Mock } from 'vitest';
import { ApiError } from '../../core/api/api-error';
import { SessionState } from '../../core/session/session-state';
import { settle, text, texts, type } from '../../testing/dom';
import { signInForTest } from '../../testing/session-fixture';
import { ruleViolation, validationError } from '../workforce/workforce.fixtures';
import { ReceiveDeliveryDialog } from './receive-delivery-dialog';
import { ReceivingApi } from './receiving-api';
import { partlyDelivered, receivingLine, receivingOrder } from './receiving.fixtures';
import { ReceivingOrder } from './receiving.models';

type Call = (...args: unknown[]) => Observable<unknown>;
const stub = (implementation: Call): Mock<Call> => vi.fn<Call>(implementation);

/** Rebar (10 ordered, 4 received), tie wire (3 ordered, none yet) and cement, which has all arrived. */
const ORDER: ReceivingOrder = receivingOrder({
  lines: [
    receivingLine({ receivedQuantity: 4, remainingQuantity: 6 }),
    receivingLine({
      id: 'line-2',
      lineNumber: 2,
      description: 'Tie wire',
      unit: 'kg',
      orderedQuantity: 3,
      remainingQuantity: 3,
    }),
    receivingLine({
      id: 'line-3',
      lineNumber: 3,
      description: 'Cement',
      unit: 'bag',
      orderedQuantity: 5,
      receivedQuantity: 5,
      remainingQuantity: 0,
    }),
  ],
});

async function open(api: Record<string, Mock<Call>>) {
  TestBed.configureTestingModule({
    providers: [
      provideHttpClient(),
      provideHttpClientTesting(),
      { provide: ReceivingApi, useValue: api },
    ],
  });
  await signInForTest(TestBed.inject(SessionState), TestBed.inject(HttpTestingController));
  const fixture = TestBed.createComponent(ReceiveDeliveryDialog);
  fixture.componentRef.setInput('order', ORDER);
  const saved: ReceivingOrder[] = [];
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

const enter = (input: HTMLInputElement, value: string) => {
  input.value = value;
  input.dispatchEvent(new Event('input', { bubbles: true }));
};

describe('ReceiveDeliveryDialog', () => {
  it('starts every item that still has something to come at what is left, and leaves out an item that has all arrived', async () => {
    const { root } = await open({});

    expect(texts(root, '.input-group__affix')).toEqual(['pc', 'kg']);
    expect(quantities(root)).toEqual(['6', '3']);
    expect(texts(root, '.field__label').filter((label) => /^\d\./.test(label))).toEqual([
      '1. Rebar 16 mm',
      '2. Tie wire',
    ]);
    expect(texts(root, '.field__hint')).toContain('Ordered 10, received 4, 6 left.');
    expect(texts(root, '.field__hint')).toContain('Ordered 3.');
    expect(root.textContent).not.toContain('Cement');
    expect(text(root, 'button[type="submit"]')).toBe('Record delivery');
  });

  it('defaults the date to today and offers no price, rate or total', async () => {
    const { root } = await open({});

    const date = root.querySelector<HTMLInputElement>('input[type="date"]')!;
    expect(date.value).toMatch(/^\d{4}-\d{2}-\d{2}$/);
    expect(date.max).toBe(date.value);
    expect(root.textContent).not.toMatch(/₱|VAT|price|cost|total|invoice/i);
  });

  it('sends the day, the supplier’s number, the note and only the items that came, under a request id', async () => {
    const recordDelivery = stub(() => of(partlyDelivered({ version: 5 })));
    const { root, saved, submit } = await open({ recordDelivery });

    type(root, 'input[formcontrolname="deliveryReceiptNumber"]', ' DR-0045 ');
    type(root, 'textarea[formcontrolname="note"]', 'Gate 2');
    const [first, second] = Array.from(
      root.querySelectorAll<HTMLInputElement>('app-number-input input'),
    );
    enter(first!, '2.5');
    first!.dispatchEvent(new Event('blur'));
    enter(second!, '0');
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

  it('asks for a quantity to be entered instead of sending a delivery of nothing', async () => {
    const recordDelivery = stub(() => of(partlyDelivered()));
    const { root, submit } = await open({ recordDelivery });
    for (const input of Array.from(
      root.querySelectorAll<HTMLInputElement>('app-number-input input'),
    ))
      enter(input, '0');

    await submit();

    expect(recordDelivery).not.toHaveBeenCalled();
    expect(text(root, 'app-alert')).toContain('Enter how much arrived for at least one item.');
  });

  it('asks for a date and refuses a missing quantity in plain words, without calling the server', async () => {
    const recordDelivery = stub(() => of(partlyDelivered()));
    const { root, submit } = await open({ recordDelivery });
    type(root, 'input[type="date"]', '');
    enter(root.querySelector<HTMLInputElement>('app-number-input input')!, '');

    await submit();

    expect(recordDelivery).not.toHaveBeenCalled();
    expect(texts(root, '.field__error')).toEqual(
      expect.arrayContaining([
        'Choose the day the delivery arrived.',
        'Enter a quantity, or 0 if none came.',
      ]),
    );
  });

  it('shows the server’s refusal of an item and of the number in words, keeps what was typed, and retries under the same request id', async () => {
    const recordDelivery = stub(() =>
      throwError(() =>
        validationError({
          lines: ['Line 1 (Rebar 16 mm): only 6 pc are left to receive. Enter 6 or less.'],
          deliveryReceiptNumber: ['This delivery receipt is already recorded.'],
        }),
      ),
    );
    const { root, submit } = await open({ recordDelivery });
    type(root, 'input[formcontrolname="deliveryReceiptNumber"]', 'DR-0001');
    type(root, 'textarea[formcontrolname="note"]', 'Gate 2');

    await submit();

    expect(text(root, '.field__error')).toBe('This delivery receipt is already recorded.');
    expect(text(root, 'app-api-error-alert')).toContain(
      'Line 1 (Rebar 16 mm): only 6 pc are left to receive. Enter 6 or less.',
    );
    expect(
      (root.querySelector('input[formcontrolname="deliveryReceiptNumber"]') as HTMLInputElement)
        .value,
    ).toBe('DR-0001');
    expect(
      (root.querySelector('textarea[formcontrolname="note"]') as HTMLTextAreaElement).value,
    ).toBe('Gate 2');
    expect(quantities(root)).toEqual(['6', '3']);

    type(root, 'input[formcontrolname="deliveryReceiptNumber"]', 'DR-0002');
    await submit();
    expect(recordDelivery).toHaveBeenCalledTimes(2);
    const [firstCall, secondCall] = recordDelivery.mock.calls;
    expect(secondCall![2]).toBe(firstCall![2]);
  });

  it('says why in the server’s words when the order is no longer open, and offers a reload when it changed under the person', async () => {
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
