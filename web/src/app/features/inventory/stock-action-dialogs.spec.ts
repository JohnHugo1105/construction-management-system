import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { Type } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { Observable, of, throwError } from 'rxjs';
import type { Mock } from 'vitest';
import { todayIn } from '../../shared/presentation/business-time';
import { buttonNamed, text, texts, type } from '../../testing/dom';
import { CountStockDialog } from './count-stock-dialog';
import {
  ApiStub,
  dialogProviders,
  errorUnder,
  openDialog,
  pick,
  submitButton,
} from './dialog-testing';
import {
  ANNEX_ID,
  RIVERSIDE_ID,
  excavator,
  fieldProblem,
  hammers,
  movement,
  ruleViolation,
} from './inventory.fixtures';
import { InventoryItem } from './inventory.models';
import { ReceiveStockDialog } from './receive-stock-dialog';
import { RetireItemDialog } from './retire-item-dialog';
import { ReverseMovementDialog } from './reverse-movement-dialog';
import { StockActionDialog } from './stock-action-dialog';
import { WriteOffDialog } from './write-off-dialog';

type Call = (...args: unknown[]) => Observable<unknown>;
const stub = (implementation: Call): Mock<Call> => vi.fn<Call>(implementation);

/** Opens a dialog with the router, HTTP set-up and a stand-in API every one of these specs needs. */
function openWith<T extends StockActionDialog>(
  dialogType: Type<T>,
  item: InventoryItem,
  api: ApiStub,
  inputs: Record<string, unknown> = {},
) {
  TestBed.configureTestingModule({
    providers: [
      provideRouter([]),
      provideHttpClient(),
      provideHttpClientTesting(),
      ...dialogProviders(api),
    ],
  });
  return openDialog(dialogType, item, inputs);
}

describe('ReceiveStockDialog', () => {
  const open = (item = hammers()) => {
    const receive = stub(() => of(hammers({ version: 4, totalQuantity: 34 })));
    return openWith(ReceiveStockDialog, item, { receive }).then((view) => ({ ...view, receive }));
  };

  it('says the stock goes into the warehouse, and asks how many with the unit', async () => {
    const { root } = await open();

    expect(text(root, 'h2')).toBe('Receive more stock');
    expect(text(root, '.stack > p')).toContain('The new stock goes into the warehouse.');
    expect(text(root, 'app-number-input')).toContain('pc');
  });

  it('sends the quantity, the date, the note and the version of the item', async () => {
    const { root, fixture, receive, done } = await open();

    type(root, 'app-number-input input', '10');
    type(root, 'textarea', '  Second delivery  ');
    submitButton(root).click();
    await fixture.whenStable();

    expect(receive).toHaveBeenCalledWith(
      'item-hammer',
      expect.objectContaining({
        quantity: 10,
        date: todayIn('Asia/Manila'),
        note: 'Second delivery',
        version: 3,
        clientRequestId: expect.stringMatching(/^[0-9a-f-]{36}$/),
      }),
    );
    expect(done).toHaveLength(1);
    expect(done[0]!.totalQuantity).toBe(34);
  });

  it('wants a quantity above zero before it asks the server', async () => {
    const { root, fixture, receive } = await open();

    submitButton(root).click();
    await fixture.whenStable();
    fixture.detectChanges();
    expect(
      root.querySelector('app-number-input')!.closest('app-form-field')!.textContent,
    ).toContain('Enter how many arrived.');

    type(root, 'app-number-input input', '0');
    submitButton(root).click();
    await fixture.whenStable();
    fixture.detectChanges();
    expect(
      root.querySelector('app-number-input')!.closest('app-form-field')!.textContent,
    ).toContain('Enter a quantity greater than zero.');
    expect(receive).not.toHaveBeenCalled();
  });
});

describe('CountStockDialog', () => {
  const open = (item = hammers(), adjust = stub(() => of(hammers({ version: 4 })))) =>
    openWith(CountStockDialog, item, { adjust }).then((view) => ({ ...view, adjust }));

  it('lists every place, showing what the records say where they say something', async () => {
    const { root } = await open();

    expect(texts(root, 'select[formControlName="place"] option')).toEqual([
      'Choose a place',
      'Warehouse (14 pc)',
      'Harbor Warehouse',
      'Riverside Residences (10 pc)',
      'Old School Annex (archived)',
    ]);
  });

  it('tells the person what the records say about the place they chose', async () => {
    const { fixture, root } = await open();

    await pick(fixture, root, 'place', 'warehouse');
    expect(text(root, 'app-form-field .field__hint')).toBe('The records say 14 pc here.');

    await pick(fixture, root, 'place', ANNEX_ID);
    expect(text(root, 'app-form-field .field__hint')).toBe('The records say none here.');
  });

  it('chooses the place when stock is in only one', async () => {
    const { root } = await open(excavator());

    expect(root.querySelector<HTMLSelectElement>('select[formControlName="place"]')!.value).toBe(
      RIVERSIDE_ID,
    );
  });

  it('sends the place, the counted quantity (zero counts), the reason and the version', async () => {
    const { fixture, root, adjust } = await open();
    await pick(fixture, root, 'place', RIVERSIDE_ID);

    type(root, 'app-number-input input', '0');
    type(root, 'textarea', '  Crew took them to another site  ');
    submitButton(root).click();
    await fixture.whenStable();

    expect(adjust).toHaveBeenCalledWith(
      'item-hammer',
      expect.objectContaining({
        location: { kind: 'Project', projectId: RIVERSIDE_ID },
        countedQuantity: 0,
        reason: 'Crew took them to another site',
        version: 3,
        clientRequestId: expect.stringMatching(/^[0-9a-f-]{36}$/),
      }),
    );
  });

  it('asks for the place, the count and a reason, and a blank reason is no reason', async () => {
    const { fixture, root, adjust } = await open();

    submitButton(root).click();
    await fixture.whenStable();
    fixture.detectChanges();
    expect(errorUnder(root, 'place')).toBe('Choose where you counted.');
    expect(errorUnder(root, 'reason')).toBe('Say why the count is different.');
    expect(
      root.querySelector('app-number-input')!.closest('app-form-field')!.textContent,
    ).toContain('Enter how many you counted. Use 0 if there are none.');

    await pick(fixture, root, 'place', 'warehouse');
    type(root, 'app-number-input input', '12');
    type(root, 'textarea', '   ');
    submitButton(root).click();
    await fixture.whenStable();
    fixture.detectChanges();
    expect(errorUnder(root, 'reason')).toBe('Say why the count is different.');
    expect(adjust).not.toHaveBeenCalled();
  });

  it('counts a single piece as 0 or 1 only', async () => {
    const { fixture, root, adjust } = await open(excavator());

    expect(text(root, 'app-form-field:nth-of-type(2) .field__hint')).toBe(
      'Count 1 if the piece is there, or 0 if it is not.',
    );
    type(root, 'app-number-input input', '2');
    type(root, 'textarea', 'Counted twice');
    submitButton(root).click();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(
      root.querySelector('app-number-input')!.closest('app-form-field')!.textContent,
    ).toContain('A single piece is counted as 0 or 1.');
    expect(adjust).not.toHaveBeenCalled();
  });

  it('shows the rule "The count matches the records" in the server\'s words', async () => {
    const message = 'The count matches the records. Nothing to adjust.';
    const { fixture, root } = await open(
      hammers(),
      stub(() => throwError(() => ruleViolation(message))),
    );
    await pick(fixture, root, 'place', 'warehouse');
    type(root, 'app-number-input input', '14');
    type(root, 'textarea', 'Counted again');

    submitButton(root).click();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(text(root, 'app-api-error-alert')).toContain(message);
  });

  it("puts the server's message about the count under the count", async () => {
    const { fixture, root } = await open(
      hammers(),
      stub(() =>
        throwError(() =>
          fieldProblem({
            countedQuantity: ['Enter the counted quantity (zero or more, up to 4 decimals).'],
          }),
        ),
      ),
    );
    await pick(fixture, root, 'place', 'warehouse');
    type(root, 'app-number-input input', '14');
    type(root, 'textarea', 'Counted again');

    submitButton(root).click();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(
      root.querySelector('app-number-input')!.closest('app-form-field')!.textContent,
    ).toContain('Enter the counted quantity (zero or more, up to 4 decimals).');
  });
});

describe('WriteOffDialog', () => {
  const open = (item = hammers(), writeOff = stub(() => of(hammers({ version: 4 })))) =>
    openWith(WriteOffDialog, item, { writeOff }).then((view) => ({ ...view, writeOff }));

  it('offers only the places that hold stock, with how much', async () => {
    const { root } = await open();

    expect(texts(root, 'select[formControlName="place"] option')).toEqual([
      'Choose a place',
      'Warehouse (14 pc)',
      'Riverside Residences (10 pc)',
    ]);
  });

  it('writes off a quantity of a tool, with the reason and the version', async () => {
    const { fixture, root, writeOff, done } = await open();
    await pick(fixture, root, 'place', 'warehouse');

    type(root, 'app-number-input input', '2');
    type(root, 'textarea', 'Broken handles');
    submitButton(root).click();
    await fixture.whenStable();

    expect(writeOff).toHaveBeenCalledWith(
      'item-hammer',
      expect.objectContaining({
        location: { kind: 'Warehouse' },
        quantity: 2,
        reason: 'Broken handles',
        version: 3,
      }),
    );
    expect(done).toHaveLength(1);
    expect(root.querySelector('app-alert')).toBeNull();
  });

  it('warns that writing off a single piece retires it, and asks no quantity', async () => {
    const { fixture, root, writeOff } = await open(excavator());

    expect(text(root, 'app-alert')).toContain('This will retire the item');
    expect(text(root, 'app-alert')).toContain('You can bring it back by reversing the write-off.');
    expect(root.querySelector('app-number-input')).toBeNull();

    type(root, 'textarea', 'Engine failed beyond repair');
    submitButton(root).click();
    await fixture.whenStable();

    expect(writeOff).toHaveBeenCalledWith(
      'item-excavator',
      expect.objectContaining({
        location: { kind: 'Project', projectId: RIVERSIDE_ID },
        quantity: 1,
        reason: 'Engine failed beyond repair',
        version: 5,
      }),
    );
  });

  it('needs a place, a quantity for a tool, and a reason', async () => {
    const { fixture, root, writeOff } = await open();

    submitButton(root).click();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(errorUnder(root, 'place')).toBe('Choose where the stock was.');
    expect(errorUnder(root, 'reason')).toBe('Say why the stock is being written off.');
    expect(
      root.querySelector('app-number-input')!.closest('app-form-field')!.textContent,
    ).toContain('Enter how many to write off.');
    expect(writeOff).not.toHaveBeenCalled();
  });

  it('shows "Only 14 pc are there, but you asked for 20" in the server\'s words', async () => {
    const message = 'Only 14 pc are there, but you asked for 20.';
    const { fixture, root } = await open(
      hammers(),
      stub(() => throwError(() => ruleViolation(message))),
    );
    await pick(fixture, root, 'place', 'warehouse');
    type(root, 'app-number-input input', '20');
    type(root, 'textarea', 'Lost');

    submitButton(root).click();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(text(root, 'app-api-error-alert')).toContain(message);
  });
});

describe('ReverseMovementDialog', () => {
  const transfer = movement({ id: 'move-transfer', quantity: 4, chargedAmountMinor: 180000 });
  const open = (
    reverse = stub(() => of(hammers({ version: 4 }))),
    line = transfer,
    item = hammers(),
  ) =>
    openWith(ReverseMovementDialog, item, { reverse }, { movement: line }).then((view) => ({
      ...view,
      reverse,
    }));

  it('asks the pending question and says what reversing will do, including the charge', async () => {
    const { root } = await open();

    expect(text(root, 'h2')).toBe('Reverse this movement?');
    expect(text(root, '.stack .stack--tight')).toContain(
      'Transfer: 4 pc, Warehouse to Riverside Residences',
    );
    expect(text(root, '.stack .stack--tight')).toContain(
      'Recorded for 1 Aug 2026 by Wilson Warehouse-Keeper',
    );
    expect(text(root, '.stack .stack--tight')).toContain(
      'Moves 4 pc back from Riverside Residences to Warehouse. The ₱1,800.00 charged to Riverside Residences is cancelled too.',
    );
  });

  it('sends the reason for that line, with the item version and a request id', async () => {
    const { root, fixture, reverse, done } = await open();

    type(root, 'textarea', '  Moved to the wrong site  ');
    submitButton(root).click();
    await fixture.whenStable();

    expect(reverse).toHaveBeenCalledWith(
      'item-hammer',
      'move-transfer',
      expect.objectContaining({
        reason: 'Moved to the wrong site',
        version: 3,
        clientRequestId: expect.stringMatching(/^[0-9a-f-]{36}$/),
      }),
    );
    expect(done).toHaveLength(1);
  });

  it('needs a reason, and spaces are not a reason', async () => {
    const { root, fixture, reverse } = await open();

    submitButton(root).click();
    await fixture.whenStable();
    fixture.detectChanges();
    expect(errorUnder(root, 'reason')).toBe('Say why this movement is being reversed.');

    type(root, 'textarea', '    ');
    submitButton(root).click();
    await fixture.whenStable();
    fixture.detectChanges();
    expect(errorUnder(root, 'reason')).toBe('Say why this movement is being reversed.');
    expect(reverse).not.toHaveBeenCalled();
  });

  it("shows it in the server's words when the stock is no longer where the move put it", async () => {
    const message = 'Only 2 pc are there, but you asked for 4.';
    const { root, fixture, done } = await open(
      stub(() => throwError(() => ruleViolation(message))),
    );

    type(root, 'textarea', 'Wrong site');
    submitButton(root).click();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(text(root, 'app-api-error-alert')).toContain(message);
    expect(done).toHaveLength(0);
  });
});

describe('RetireItemDialog', () => {
  const none = { totalQuantity: 0, balances: [] };

  it('explains what to do first when stock is still on record, with no form that could not work', async () => {
    const { root } = await openWith(RetireItemDialog, hammers(), {});

    expect(text(root, 'h2')).toBe('Retire this item?');
    expect(text(root, 'app-alert')).toContain('Stock is still on record (24 pc).');
    expect(text(root, 'app-alert')).toContain('Move it out or write it off first');
    expect(root.querySelector('form')).toBeNull();
    expect(texts(root, '.dialog-actions .btn')).toEqual(['Close']);
  });

  it('asks only for the reason when there is no stock, and sends it with the version', async () => {
    const retire = stub(() => of(hammers({ ...none, status: 'Retired', version: 4 })));
    const { root, fixture, done } = await openWith(RetireItemDialog, hammers(none), { retire });

    expect(text(root, 'form')).toContain('Reason');
    type(root, 'textarea', '  Sold to another company  ');
    submitButton(root).click();
    await fixture.whenStable();

    expect(retire).toHaveBeenCalledWith('item-hammer', {
      reason: 'Sold to another company',
      version: 3,
    });
    expect(done).toHaveLength(1);
    expect(buttonNamed(root, 'Cancel')).toBeTruthy();
  });

  it('needs a reason', async () => {
    const retire = stub(() => of(hammers()));
    const { root, fixture } = await openWith(RetireItemDialog, hammers(none), { retire });

    submitButton(root).click();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(errorUnder(root, 'reason')).toBe('Say why the item is being retired.');
    expect(retire).not.toHaveBeenCalled();
  });
});
