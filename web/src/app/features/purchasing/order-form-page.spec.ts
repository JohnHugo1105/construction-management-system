import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ChangeDetectionStrategy, Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter, withComponentInputBinding } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { Observable, of, throwError } from 'rxjs';
import type { Mock } from 'vitest';
import { ApiError } from '../../core/api/api-error';
import { SessionState } from '../../core/session/session-state';
import { buttonNamed, settle, text, texts, type } from '../../testing/dom';
import { signInForTest } from '../../testing/session-fixture';
import { CostingApi } from '../costing/costing-api';
import { referenceFixture } from '../costing/testing/costing-fixtures';
import { OrderFormPage } from './order-form-page';
import { PurchasingApi } from './purchasing-api';
import { FIRM_ID, RIVERSIDE_ID, orderFixture } from './purchasing.fixtures';

@Component({ template: '', changeDetection: ChangeDetectionStrategy.OnPush })
class Blank {}

type Call = (...args: unknown[]) => Observable<unknown>;
const stub = (implementation: Call): Mock<Call> => vi.fn<Call>(implementation);

async function open(url = '/purchasing/orders/new', api: Record<string, Mock<Call>> = {}) {
  const purchasing = {
    supplierLookup: stub(() => of([{ id: FIRM_ID, name: 'Visayas Steel Trading', isSupplier: true, isSubcontractor: false }])),
    projectLookup: stub(() =>
      of([
        { id: RIVERSIDE_ID, name: 'Riverside Residences', status: 'Active', subProjects: [{ id: 'sub-1', name: 'Tower A', status: 'Active' }] },
      ]),
    ),
    createOrder: stub(() => of(orderFixture())),
    updateOrder: stub(() => of(orderFixture())),
    getOrder: stub(() => of(orderFixture())),
    ...api,
  };
  TestBed.configureTestingModule({
    providers: [
      provideRouter(
        [
          { path: 'purchasing/orders/new', component: OrderFormPage },
          { path: 'purchasing/orders/:id/edit', component: OrderFormPage },
          { path: 'purchasing/orders/:id', component: Blank },
          { path: 'purchasing', component: Blank },
          { path: 'purchasing/suppliers/new', component: Blank },
        ],
        withComponentInputBinding(),
      ),
      provideHttpClient(),
      provideHttpClientTesting(),
      { provide: PurchasingApi, useValue: purchasing },
      { provide: CostingApi, useValue: { reference: () => of(referenceFixture()) } },
    ],
  });
  await signInForTest(TestBed.inject(SessionState), TestBed.inject(HttpTestingController));
  const harness = await RouterTestingHarness.create();
  await harness.navigateByUrl(url, OrderFormPage);
  await settle(harness.fixture);
  return { harness, root: harness.fixture.nativeElement as HTMLElement, purchasing, router: TestBed.inject(Router) };
}

const choose = (root: HTMLElement, selector: string, value: string) => {
  const select = root.querySelector<HTMLSelectElement>(selector)!;
  select.value = value;
  select.dispatchEvent(new Event('change', { bubbles: true }));
};

/** Adds one line through the dialog the way a person does: 10 pieces of rebar at 500.00 with no VAT. */
async function addLine(harness: RouterTestingHarness, root: HTMLElement) {
  buttonNamed(root, 'Add a line').click();
  await settle(harness.fixture);
  const dialog = document.querySelector('app-order-line-dialog') as HTMLElement;
  type(dialog, '[formControlName="description"]', 'Rebar 16 mm');
  dialog.querySelector<HTMLInputElement>('label.choice input[type="radio"]')!.click();
  type(dialog, 'app-number-input input', '10');
  type(dialog, '[formControlName="unit"]', 'pc');
  type(dialog, 'app-money-input input', '500.00');
  Array.from(dialog.querySelectorAll<HTMLElement>('label.choice')).find((item) => item.textContent?.includes('No VAT'))!.querySelector('input')!.click();
  buttonNamed(dialog, 'Add line').click();
  await settle(harness.fixture);
}

describe('OrderFormPage raising a new order', () => {
  it('offers the supplier firms and the open projects, and only the sub-projects of the project chosen', async () => {
    const { harness, root } = await open();

    expect(texts(root, '[formControlName="supplierId"] option')).toEqual(['Choose a supplier', 'Visayas Steel Trading']);
    expect(texts(root, '[formControlName="projectId"] option')).toEqual(['Choose a project', 'Riverside Residences']);
    expect(root.querySelector('[formControlName="subProjectId"]')).toBeNull();

    choose(root, '[formControlName="projectId"]', RIVERSIDE_ID);
    await settle(harness.fixture);
    expect(texts(root, '[formControlName="subProjectId"] option')).toEqual(['Whole project', 'Tower A']);
  });

  it('chooses the project when it is opened from a project', async () => {
    const { root } = await open(`/purchasing/orders/new?project=${RIVERSIDE_ID}`);

    expect(root.querySelector<HTMLSelectElement>('[formControlName="projectId"]')!.value).toBe(RIVERSIDE_ID);
  });

  it('will not save without a supplier, a project and at least one line, and sends nothing', async () => {
    const { harness, root, purchasing } = await open();

    buttonNamed(root, 'Save purchase order').click();
    await settle(harness.fixture);

    expect(purchasing.createOrder).not.toHaveBeenCalled();
    expect(text(root, 'app-alert')).toContain('Some details need your attention');
    expect(text(root, '.lines')).toContain('Add at least one line to the order.');
    expect(root.textContent).toContain('Choose the supplier this order is for.');
  });

  it('adds a line in a dialog, sends the order with a request id and goes to the new order', async () => {
    const { harness, root, purchasing, router } = await open();

    choose(root, '[formControlName="supplierId"]', FIRM_ID);
    choose(root, '[formControlName="projectId"]', RIVERSIDE_ID);
    await addLine(harness, root);
    expect(texts(root, '.lines__item')[0]).toContain('Rebar 16 mm');
    expect(texts(root, '.lines__item')[0]).toContain('10 pc at ₱500.00');

    buttonNamed(root, 'Save purchase order').click();
    await settle(harness.fixture);

    expect(purchasing.createOrder).toHaveBeenCalledTimes(1);
    const [fields, requestId] = purchasing.createOrder.mock.calls[0]!;
    expect(fields).toMatchObject({
      projectId: RIVERSIDE_ID,
      supplierId: FIRM_ID,
      subProjectId: null,
      expectedDate: null,
      lines: [{ description: 'Rebar 16 mm', itemType: 'Material', quantity: 10, unit: 'pc', unitCostMinor: 50_000, vatTreatment: 'Exempt' }],
    });
    expect(requestId).toMatch(/^[0-9a-f-]{36}$/);
    expect(router.url).toBe('/purchasing/orders/order-1');
  });

  it('keeps what was typed and shows the server message under the line list when it refuses the lines', async () => {
    const refusal = new ApiError({
      status: 422,
      title: 'Some details need your attention',
      detail: 'Check the highlighted fields and try again.',
      code: 'validation_failed',
      errors: { lines: ['Line 1: The unit cost can\'t be negative.'] },
    });
    const { harness, root, purchasing } = await open('/purchasing/orders/new', { createOrder: stub(() => throwError(() => refusal)) });

    choose(root, '[formControlName="supplierId"]', FIRM_ID);
    choose(root, '[formControlName="projectId"]', RIVERSIDE_ID);
    await addLine(harness, root);
    buttonNamed(root, 'Save purchase order').click();
    await settle(harness.fixture);

    expect(purchasing.createOrder).toHaveBeenCalledTimes(1);
    expect(text(root, '.lines .field__error')).toBe("Line 1: The unit cost can't be negative.");
    expect(texts(root, '.lines__item')).toHaveLength(1);
    expect(root.querySelector<HTMLSelectElement>('[formControlName="supplierId"]')!.value).toBe(FIRM_ID);
  });

  it('warns that there is no supplier to order from, with the next step', async () => {
    const { root } = await open('/purchasing/orders/new', { supplierLookup: stub(() => of([])) });

    expect(text(root, 'app-alert')).toContain('There is no supplier to order from yet');
    expect(buttonNamed(root, 'Add a supplier').getAttribute('href')).toBe('/purchasing/suppliers/new');
  });
});

describe('OrderFormPage changing a draft', () => {
  it('fills the form from the order and saves with the version it opened', async () => {
    const { harness, root, purchasing } = await open('/purchasing/orders/order-1/edit', {
      getOrder: stub(() => of(orderFixture({ version: 3, notes: 'Deliver to gate 2' }))),
    });

    expect(root.querySelector<HTMLSelectElement>('[formControlName="supplierId"]')!.value).toBe(FIRM_ID);
    expect(root.querySelector<HTMLTextAreaElement>('[formControlName="notes"]')!.value).toBe('Deliver to gate 2');
    expect(texts(root, '.lines__item')[0]).toContain('Rebar 16 mm');
    expect(text(root, '.lines')).toContain('Saved total with VAT: ₱5,000.00');

    buttonNamed(root, 'Save draft').click();
    await settle(harness.fixture);

    expect(purchasing.updateOrder).toHaveBeenCalledTimes(1);
    expect(purchasing.updateOrder.mock.calls[0]![0]).toBe('order-1');
    expect(purchasing.updateOrder.mock.calls[0]![2]).toBe(3);
  });

  it('removes a line from the draft', async () => {
    const { harness, root } = await open('/purchasing/orders/order-1/edit');

    buttonNamed(root, 'Remove').click();
    await settle(harness.fixture);

    expect(root.querySelectorAll('.lines__item')).toHaveLength(0);
    expect(text(root, '.lines')).toContain('No lines yet');
  });
});
