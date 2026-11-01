import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ChangeDetectionStrategy, Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter, withComponentInputBinding } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { Observable, of, throwError } from 'rxjs';
import type { Mock } from 'vitest';
import { ApiError } from '../../core/api/api-error';
import { ToastService } from '../../core/notifications/toast-service';
import { SessionState } from '../../core/session/session-state';
import { todayIn } from '../../shared/presentation/business-time';
import { buttonNamed, flushMicrotasks, settle, text, texts, type } from '../../testing/dom';
import { signInForTest } from '../../testing/session-fixture';
import { InventoryApi } from './inventory-api';
import { conflict, excavator, fieldProblem, hammers } from './inventory.fixtures';
import { ItemFormPage } from './item-form-page';

@Component({ template: '', changeDetection: ChangeDetectionStrategy.OnPush })
class Blank {}

type Call = (...args: unknown[]) => Observable<unknown>;
const stub = (implementation: Call): Mock<Call> => vi.fn<Call>(implementation);

async function open(url: string, api: Partial<Record<'register' | 'update' | 'get', Mock<Call>>>) {
  TestBed.configureTestingModule({
    providers: [
      provideRouter(
        [
          { path: 'stock/new', component: ItemFormPage },
          { path: 'stock/:id/edit', component: ItemFormPage },
          { path: 'stock/:id', component: Blank },
          { path: 'stock', component: Blank },
        ],
        withComponentInputBinding(),
      ),
      provideHttpClient(),
      provideHttpClientTesting(),
      { provide: InventoryApi, useValue: api },
    ],
  });
  await signInForTest(TestBed.inject(SessionState), TestBed.inject(HttpTestingController));
  const harness = await RouterTestingHarness.create();
  await harness.navigateByUrl(url, ItemFormPage);
  await settle(harness.fixture);
  return {
    harness,
    root: harness.fixture.nativeElement as HTMLElement,
    router: TestBed.inject(Router),
    toast: TestBed.inject(ToastService),
  };
}

const control = (root: HTMLElement, name: string) =>
  root.querySelector<HTMLInputElement>(`[formControlName="${name}"]`);
const choose = async (
  harness: RouterTestingHarness,
  root: HTMLElement,
  category: 'Tool' | 'Equipment' | 'Machinery',
) => {
  const choice = Array.from(root.querySelectorAll<HTMLElement>('label.choice')).find(
    (label) => label.textContent?.trim() === category,
  );
  choice!.querySelector<HTMLInputElement>('input[type="radio"]')!.click();
  await settle(harness.fixture);
};
const fieldError = (root: HTMLElement, name: string) =>
  control(root, name)
    ?.closest('app-form-field')
    ?.querySelector('.field__error')
    ?.textContent?.trim() ?? '';
const registerButton = (root: HTMLElement) => buttonNamed(root, 'Register item');
const labelsIn = (root: HTMLElement) => texts(root, '.field__label, .field__legend');

describe('ItemFormPage: choosing a category', () => {
  it('starts with no category chosen, and explains the difference in one sentence', async () => {
    const { root } = await open('/stock/new', {});

    const radios = root.querySelectorAll<HTMLInputElement>('input[type="radio"]');
    expect(texts(root, 'label.choice')).toEqual(['Tool', 'Equipment', 'Machinery']);
    expect(Array.from(radios).some((radio) => radio.checked)).toBe(false);
    expect(text(root, 'app-field-group .field__hint')).toBe(
      'Tools are counted. Equipment and machinery are single pieces tracked by serial number.',
    );
    // Nothing that depends on the category is shown yet.
    expect(labelsIn(root).join('|')).not.toContain('Quantity');
    expect(labelsIn(root).join('|')).not.toContain('Serial number');
  });

  it('asks a tool for how many, and offers no serial number', async () => {
    const { harness, root } = await open('/stock/new', {});

    await choose(harness, root, 'Tool');

    expect(labelsIn(root).join('|')).toContain('Quantity');
    expect(labelsIn(root).join('|')).not.toContain('Serial number');
    expect(root.querySelector('app-number-input')).not.toBeNull();
    expect(control(root, 'unit')!.value).toBe('pc');
  });

  it.each(['Equipment', 'Machinery'] as const)(
    'makes %s a single piece: the serial number is required and there is no quantity to type',
    async (category) => {
      const { harness, root } = await open('/stock/new', {});

      await choose(harness, root, category);

      expect(labelsIn(root).join('|')).toContain('Serial number');
      const serialField = control(root, 'serialNumber')!.closest('app-form-field')!;
      expect(serialField.querySelector('.field__optional')).toBeNull();
      expect(control(root, 'serialNumber')!.getAttribute('aria-required')).toBe('true');
      expect(root.querySelector('app-number-input')).toBeNull();
      expect(labelsIn(root).join('|')).not.toContain('Quantity');
      expect(root.textContent).toContain('One piece is received into the warehouse.');
      expect(control(root, 'unit')!.value).toBe('unit');
    },
  );

  it('drops the quantity box and requires the serial number when a tool becomes equipment', async () => {
    const { harness, root } = await open('/stock/new', {});
    await choose(harness, root, 'Tool');
    expect(root.querySelector('app-number-input')).not.toBeNull();

    await choose(harness, root, 'Equipment');

    expect(root.querySelector('app-number-input')).toBeNull();
    expect(control(root, 'serialNumber')).not.toBeNull();
  });

  it('keeps a unit the person typed when they change their mind about the category', async () => {
    const { harness, root } = await open('/stock/new', {});
    await choose(harness, root, 'Tool');

    type(root, '[formControlName="unit"]', 'set');
    await choose(harness, root, 'Equipment');

    expect(control(root, 'unit')!.value).toBe('set');
  });

  it('suggests units in a list, and still takes free text', async () => {
    const { root } = await open('/stock/new', {});

    const options = Array.from(
      root.querySelectorAll<HTMLOptionElement>('#unit-suggestions option'),
    );
    expect(options.map((option) => option.value)).toEqual(
      expect.arrayContaining(['pc', 'unit', 'set', 'bag', 'm³', 'l.s']),
    );
    expect(control(root, 'unit')!.getAttribute('list')).toBe('unit-suggestions');
  });
});

describe('ItemFormPage: registering', () => {
  it('registers a tool with its quantity, trimmed text, cost in centavos and a request id', async () => {
    const register = stub(() => of(hammers({ id: 'new-1' })));
    const { harness, root, router, toast } = await open('/stock/new', { register });
    const today = todayIn('Asia/Manila');

    await choose(harness, root, 'Tool');
    type(root, '[formControlName="description"]', '  Claw hammer 16 oz  ');
    type(root, '[formControlName="brand"]', 'Stanley');
    type(root, 'app-number-input input', '10');
    type(root, '[formControlName="supplier"]', 'Hardware Hub');
    type(root, '[formControlName="invoiceNumber"]', '   ');
    type(root, 'app-money-input input', '450.00');
    registerButton(root).click();
    await settle(harness.fixture);

    expect(register).toHaveBeenCalledTimes(1);
    const request = register.mock.calls[0]![0] as Record<string, unknown>;
    expect(request).toMatchObject({
      description: 'Claw hammer 16 oz',
      category: 'Tool',
      brand: 'Stanley',
      serialNumber: null,
      unit: 'pc',
      sourceDealer: 'Hardware Hub',
      invoiceNumber: null,
      purchaseDate: today,
      unitCostMinor: 45000,
      quantity: 10,
      remarks: null,
    });
    expect(request['clientRequestId']).toMatch(/^[0-9a-f-]{36}$/);
    await flushMicrotasks(6);
    expect(router.url).toBe('/stock/new-1');
    expect(toast.toasts().at(-1)?.message).toBe('Item registered.');
  });

  it('registers machinery as one piece with its serial number, and sends no typed quantity', async () => {
    const register = stub(() => of(excavator({ id: 'new-2' })));
    const { harness, root } = await open('/stock/new', { register });

    await choose(harness, root, 'Machinery');
    type(root, '[formControlName="description"]', 'Excavator PC200');
    type(root, '[formControlName="serialNumber"]', '  PC200-8-A41127  ');
    type(root, 'app-money-input input', '4,850,000.00');
    registerButton(root).click();
    await settle(harness.fixture);

    expect(register.mock.calls[0]![0]).toMatchObject({
      category: 'Machinery',
      serialNumber: 'PC200-8-A41127',
      quantity: 1,
      unit: 'unit',
      unitCostMinor: 485000000,
    });
  });

  it('sends a cost of zero when none is given, so the item simply cannot be charged yet', async () => {
    const register = stub(() => of(hammers({ id: 'new-3' })));
    const { harness, root } = await open('/stock/new', { register });

    await choose(harness, root, 'Tool');
    type(root, '[formControlName="description"]', 'Shovel');
    type(root, 'app-number-input input', '3');
    registerButton(root).click();
    await settle(harness.fixture);

    expect(register.mock.calls[0]![0]).toMatchObject({ unitCostMinor: 0, quantity: 3 });
  });

  it('asks for what is missing, next to each field, and sends nothing', async () => {
    const register = stub(() => of(hammers()));
    const { harness, root } = await open('/stock/new', { register });

    registerButton(root).click();
    await settle(harness.fixture);
    expect(register).not.toHaveBeenCalled();
    expect(text(root, 'app-field-group .field__error')).toContain(
      'Choose tool, equipment or machinery.',
    );
    expect(root.querySelector('app-alert')!.textContent).toContain(
      'Some details need your attention',
    );

    await choose(harness, root, 'Equipment');
    registerButton(root).click();
    await settle(harness.fixture);
    expect(fieldError(root, 'description')).toContain('Enter what the item is.');
    expect(fieldError(root, 'serialNumber')).toContain(
      'Enter the serial number. Equipment and machinery are tracked one by one.',
    );
    expect(register).not.toHaveBeenCalled();
  });

  it('asks a tool for a quantity greater than zero', async () => {
    const register = stub(() => of(hammers()));
    const { harness, root } = await open('/stock/new', { register });
    await choose(harness, root, 'Tool');
    type(root, '[formControlName="description"]', 'Shovel');

    registerButton(root).click();
    await settle(harness.fixture);
    expect(
      root.querySelector('app-number-input')!.closest('app-form-field')!.textContent,
    ).toContain('Enter how many you are receiving.');

    type(root, 'app-number-input input', '0');
    registerButton(root).click();
    await settle(harness.fixture);
    expect(
      root.querySelector('app-number-input')!.closest('app-form-field')!.textContent,
    ).toContain('Enter a quantity greater than zero.');
    expect(register).not.toHaveBeenCalled();
  });

  it('refuses a purchase date that has not happened yet, before asking the server', async () => {
    const register = stub(() => of(hammers()));
    const { harness, root } = await open('/stock/new', { register });
    await choose(harness, root, 'Tool');
    type(root, '[formControlName="description"]', 'Shovel');
    type(root, 'app-number-input input', '3');

    type(root, '[formControlName="purchaseDate"]', '2999-01-01');
    registerButton(root).click();
    await settle(harness.fixture);

    expect(fieldError(root, 'purchaseDate')).toContain("The purchase date can't be in the future.");
    expect(register).not.toHaveBeenCalled();
    expect(control(root, 'purchaseDate')!.getAttribute('max')).toBe(todayIn('Asia/Manila'));
  });

  it('shows "serial number already registered" under the serial number, in the server\'s words', async () => {
    const taken =
      'Serial number "PC200-8-A41127" is already registered to another item. Check the number.';
    const register = stub(() => throwError(() => fieldProblem({ serialNumber: [taken] })));
    const { harness, root } = await open('/stock/new', { register });

    await choose(harness, root, 'Machinery');
    type(root, '[formControlName="description"]', 'Excavator PC200');
    type(root, '[formControlName="serialNumber"]', 'PC200-8-A41127');
    registerButton(root).click();
    await settle(harness.fixture);

    expect(fieldError(root, 'serialNumber')).toBe(taken);
    expect(control(root, 'serialNumber')!.getAttribute('aria-invalid')).toBe('true');
    // Nothing above the form repeats a message that sits under its field.
    expect(root.querySelector('app-api-error-alert .alert')).toBeNull();
  });

  it("maps the API's names for the cost and the supplier onto the fields the person sees", async () => {
    const register = stub(() =>
      throwError(() =>
        fieldProblem({
          unitCostMinor: ["The unit cost can't be negative."],
          sourceDealer: ['The supplier is too long.'],
        }),
      ),
    );
    const { harness, root } = await open('/stock/new', { register });

    await choose(harness, root, 'Tool');
    type(root, '[formControlName="description"]', 'Shovel');
    type(root, 'app-number-input input', '3');
    registerButton(root).click();
    await settle(harness.fixture);

    expect(root.querySelector('app-money-input')!.closest('app-form-field')!.textContent).toContain(
      "The unit cost can't be negative.",
    );
    expect(fieldError(root, 'supplier')).toBe('The supplier is too long.');
  });
});

describe('ItemFormPage: the request id', () => {
  it('keeps the same id when a try fails, so the retry cannot register a second item', async () => {
    const register = stub(() => of(hammers({ id: 'new-4' }))).mockReturnValueOnce(
      throwError(
        () =>
          new ApiError({
            status: 500,
            title: 'Something went wrong',
            detail: "We couldn't finish that. Try again in a moment.",
            traceId: 'ref-1',
          }),
      ),
    );
    const { harness, root, router } = await open('/stock/new', { register });
    await choose(harness, root, 'Tool');
    type(root, '[formControlName="description"]', 'Shovel');
    type(root, 'app-number-input input', '3');

    registerButton(root).click();
    await settle(harness.fixture);
    expect(text(root, 'app-api-error-alert')).toContain('Reference: ref-1');
    // What was typed is still there.
    expect(control(root, 'description')!.value).toBe('Shovel');

    registerButton(root).click();
    await settle(harness.fixture);
    await flushMicrotasks(6);

    const [first, second] = register.mock.calls.map(
      ([request]) => (request as { clientRequestId: string }).clientRequestId,
    );
    expect(first).toMatch(/^[0-9a-f-]{36}$/);
    expect(second).toBe(first);
    expect(router.url).toBe('/stock/new-4');
  });

  it('gives each new form its own id', async () => {
    const register = stub(() => of(hammers({ id: 'new-5' })));
    const first = await open('/stock/new', { register });
    await choose(first.harness, first.root, 'Tool');
    type(first.root, '[formControlName="description"]', 'Shovel');
    type(first.root, 'app-number-input input', '3');
    registerButton(first.root).click();
    await settle(first.harness.fixture);
    TestBed.resetTestingModule();

    const second = await open('/stock/new', { register });
    await choose(second.harness, second.root, 'Tool');
    type(second.root, '[formControlName="description"]', 'Shovel');
    type(second.root, 'app-number-input input', '3');
    registerButton(second.root).click();
    await settle(second.harness.fixture);

    const ids = register.mock.calls.map(
      ([request]) => (request as { clientRequestId: string }).clientRequestId,
    );
    expect(ids).toHaveLength(2);
    expect(ids[1]).not.toBe(ids[0]);
  });

  it('ignores a second press while the first is still being sent', async () => {
    let finish: (value: unknown) => void = () => undefined;
    const register = stub(
      () =>
        new Observable((subscriber) => {
          finish = (value) => {
            subscriber.next(value);
            subscriber.complete();
          };
        }),
    );
    const { harness, root } = await open('/stock/new', { register });
    await choose(harness, root, 'Tool');
    type(root, '[formControlName="description"]', 'Shovel');
    type(root, 'app-number-input input', '3');

    registerButton(root).click();
    registerButton(root).click();
    await settle(harness.fixture);

    expect(register).toHaveBeenCalledTimes(1);
    expect(registerButton(root).getAttribute('aria-busy')).toBe('true');
    finish(hammers({ id: 'new-6' }));
    await settle(harness.fixture);
  });
});

describe('ItemFormPage: editing details', () => {
  it('fills the form from the item, fixes the category, and has no quantity to type', async () => {
    const get = stub(() => of(hammers()));
    const { root } = await open('/stock/item-hammer/edit', { get });

    expect(get).toHaveBeenCalledWith('item-hammer');
    expect(root.querySelectorAll('input[type="radio"]')).toHaveLength(0);
    expect(text(root, 'form')).toContain(
      "The category can't be changed once the item is registered.",
    );
    expect(control(root, 'description')!.value).toBe('Claw hammer 16 oz');
    expect(control(root, 'brand')!.value).toBe('Stanley');
    expect(control(root, 'unit')!.value).toBe('pc');
    expect(control(root, 'purchaseDate')!.value).toBe('2026-06-01');
    expect((root.querySelector('app-money-input input') as HTMLInputElement).value).toBe('450.00');
    expect(root.querySelector('app-number-input')).toBeNull();
    expect(buttonNamed(root, 'Save details')).toBeTruthy();
  });

  it('keeps the serial number of a single piece required', async () => {
    const update = stub(() => of(excavator()));
    const { harness, root } = await open('/stock/item-excavator/edit', {
      get: stub(() => of(excavator())),
      update,
    });

    type(root, '[formControlName="serialNumber"]', '   ');
    buttonNamed(root, 'Save details').click();
    await settle(harness.fixture);

    expect(fieldError(root, 'serialNumber')).toContain('Enter the serial number.');
    expect(update).not.toHaveBeenCalled();
  });

  it('saves with the version it opened and goes back to the item', async () => {
    const update = stub(() => of(hammers({ version: 4 })));
    const { harness, root, router, toast } = await open('/stock/item-hammer/edit', {
      get: stub(() => of(hammers())),
      update,
    });

    type(root, '[formControlName="description"]', 'Claw hammer 20 oz');
    buttonNamed(root, 'Save details').click();
    await settle(harness.fixture);
    await flushMicrotasks(6);

    expect(update).toHaveBeenCalledWith(
      'item-hammer',
      expect.objectContaining({
        description: 'Claw hammer 20 oz',
        unitCostMinor: 45000,
        version: 3,
        purchaseDate: '2026-06-01',
      }),
    );
    expect(router.url).toBe('/stock/item-hammer');
    expect(toast.toasts().at(-1)?.message).toBe('Details saved.');
  });

  it('says someone else changed the item, and Reload fetches it again', async () => {
    const get = stub(() =>
      of(hammers({ description: 'Claw hammer (renamed)', version: 9 })),
    ).mockReturnValueOnce(of(hammers()));
    const update = stub(() => throwError(() => conflict()));
    const { harness, root } = await open('/stock/item-hammer/edit', { get, update });

    buttonNamed(root, 'Save details').click();
    await settle(harness.fixture);
    expect(text(root, 'app-api-error-alert')).toContain(
      'Someone else changed this record while you were editing.',
    );

    buttonNamed(root, 'Reload item').click();
    await settle(harness.fixture);

    expect(get).toHaveBeenCalledTimes(2);
    expect(control(root, 'description')!.value).toBe('Claw hammer (renamed)');
    expect(root.querySelector('app-api-error-alert .alert')).toBeNull();
  });

  it('does not let a retired item be edited, and says how to get it back', async () => {
    const { root } = await open('/stock/item-excavator/edit', {
      get: stub(() => of(excavator({ status: 'Retired', totalQuantity: 0, balances: [] }))),
    });

    expect(text(root, 'app-alert')).toContain('This item is retired');
    expect(text(root, 'app-alert')).toContain("A retired item can't be edited.");
    expect(root.querySelector('form')).toBeNull();
    expect(buttonNamed(root, 'Go to item').getAttribute('href')).toBe('/stock/item-excavator');
  });
});
