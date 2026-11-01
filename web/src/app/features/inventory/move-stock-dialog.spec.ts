import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { Observable, of, throwError } from 'rxjs';
import type { Mock } from 'vitest';
import { ApiError } from '../../core/api/api-error';
import { todayIn } from '../../shared/presentation/business-time';
import { buttonNamed, text, texts, type } from '../../testing/dom';
import { dialogProviders, errorUnder, openDialog, pick, submitButton } from './dialog-testing';
import {
  ANNEX_ID,
  HARBOR_ID,
  RIVERSIDE_ID,
  conflict,
  excavator,
  fieldProblem,
  hammers,
  onProject,
  ruleViolation,
  warehouse,
} from './inventory.fixtures';
import { MoveStockDialog } from './move-stock-dialog';

type Call = (...args: unknown[]) => Observable<unknown>;
const stub = (implementation: Call): Mock<Call> => vi.fn<Call>(implementation);
const open = (
  item = hammers(),
  transfer = stub(() => of(hammers({ version: 4 }))),
  inputs = {},
) => {
  TestBed.configureTestingModule({
    providers: [
      provideRouter([]),
      provideHttpClient(),
      provideHttpClientTesting(),
      ...dialogProviders({ transfer }),
    ],
  });
  return openDialog(MoveStockDialog, item, inputs).then((view) => ({ ...view, transfer }));
};

const quantityBox = (root: ParentNode) => root.querySelector('app-number-input input');
const chargeBox = (root: ParentNode) =>
  root.querySelector<HTMLInputElement>('input[type="checkbox"][formControlName="charge"]');
const preview = (root: ParentNode) => text(root, 'app-alert');

describe('MoveStockDialog: the places', () => {
  it('offers only the places that hold stock as the From, each with how much is there', async () => {
    const { root } = await open();

    expect(texts(root, 'select[formControlName="from"] option')).toEqual([
      'Choose a place',
      'Warehouse (14 pc)',
      'Riverside Residences (10 pc)',
    ]);
    // With two places to choose from, nothing is chosen for the person.
    expect(root.querySelector<HTMLSelectElement>('select[formControlName="from"]')!.value).toBe('');
  });

  it('chooses the only place a single piece can be taken from', async () => {
    const { root } = await open(excavator());

    expect(root.querySelector<HTMLSelectElement>('select[formControlName="from"]')!.value).toBe(
      RIVERSIDE_ID,
    );
  });

  it('offers the warehouse and open projects as the To, never the place it leaves, with archived projects set apart', async () => {
    const { fixture, root } = await open();

    await pick(fixture, root, 'from', RIVERSIDE_ID);

    expect(texts(root, 'select[formControlName="to"] > option')).toEqual([
      'Choose a place',
      'Warehouse',
      'Harbor Warehouse',
    ]);
    expect(texts(root, 'select[formControlName="to"] optgroup option')).toEqual([
      'Old School Annex',
    ]);
    expect(root.querySelector('select[formControlName="to"] optgroup')!.getAttribute('label')).toBe(
      "Archived projects (can't receive stock)",
    );

    await pick(fixture, root, 'from', 'warehouse');
    expect(texts(root, 'select[formControlName="to"] > option')).toEqual([
      'Choose a place',
      'Warehouse',
      'Harbor Warehouse',
      'Riverside Residences',
    ]);
  });

  it('fixes a single piece at 1: no quantity to type, and a plain sentence says so', async () => {
    const { root } = await open(excavator());

    expect(quantityBox(root)).toBeNull();
    expect(text(root, 'form')).toContain('This is a single piece, so 1 is moved.');
  });

  it('asks a tool how many, with its unit', async () => {
    const { root } = await open();

    expect(quantityBox(root)).not.toBeNull();
    expect(text(root, 'app-number-input')).toContain('pc');
  });
});

describe('MoveStockDialog: charging the move to the project', () => {
  it('offers the charge only when the destination is a project', async () => {
    const { fixture, root } = await open();
    await pick(fixture, root, 'from', 'warehouse');
    expect(chargeBox(root)).toBeNull();

    await pick(fixture, root, 'to', RIVERSIDE_ID);
    expect(chargeBox(root)).not.toBeNull();
    expect(text(root, '.choice')).toContain('Charge this move to the project');
    expect(text(root, '.choice__hint')).toBe(
      "Adds the cost to the project's costs at the item's unit cost. Reversing the move cancels it.",
    );

    await pick(fixture, root, 'to', 'warehouse');
    expect(chargeBox(root)).toBeNull();
  });

  it('is off until the person asks for it, and shows no amount while it is off', async () => {
    const { fixture, root } = await open();
    await pick(fixture, root, 'from', 'warehouse');
    await pick(fixture, root, 'to', RIVERSIDE_ID);
    type(root, 'app-number-input input', '14');
    await fixture.whenStable();
    fixture.detectChanges();

    expect(chargeBox(root)!.checked).toBe(false);
    expect(preview(root)).toBe('');
  });

  it('previews 14 x 450.00 as ₱6,300.00, labelled a preview, and follows the quantity', async () => {
    const { fixture, root } = await open();
    await pick(fixture, root, 'from', 'warehouse');
    await pick(fixture, root, 'to', RIVERSIDE_ID);
    type(root, 'app-number-input input', '14');
    chargeBox(root)!.click();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(text(root, 'app-alert .alert__title')).toBe('Preview');
    expect(preview(root)).toContain('This move will charge ₱6,300.00 to Riverside Residences.');
    expect(preview(root)).toContain('The saved amount is worked out when you move the stock.');

    type(root, 'app-number-input input', '4');
    await fixture.whenStable();
    fixture.detectChanges();
    expect(preview(root)).toContain('This move will charge ₱1,800.00 to Riverside Residences.');
  });

  it('previews 1 x 4,850,000.00 for a single piece without asking for a quantity', async () => {
    const { fixture, root } = await open(excavator());
    await pick(fixture, root, 'to', HARBOR_ID);

    chargeBox(root)!.click();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(preview(root)).toContain('This move will charge ₱4,850,000.00 to Harbor Warehouse.');
  });

  it('previews 0.5 x 3.00 as ₱1.50', async () => {
    const { fixture, root } = await open(hammers({ unitCostMinor: 300 }));
    await pick(fixture, root, 'from', 'warehouse');
    await pick(fixture, root, 'to', RIVERSIDE_ID);
    type(root, 'app-number-input input', '0.5');
    chargeBox(root)!.click();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(preview(root)).toContain('This move will charge ₱1.50 to Riverside Residences.');
  });

  it('warns when the charge would work out to nothing', async () => {
    const { fixture, root } = await open(hammers({ unitCostMinor: 1 }));
    await pick(fixture, root, 'from', 'warehouse');
    await pick(fixture, root, 'to', RIVERSIDE_ID);
    type(root, 'app-number-input input', '0.0001');
    chargeBox(root)!.click();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(preview(root)).toContain('At this quantity the charge works out to nothing.');
    expect(preview(root)).not.toContain('This move will charge');
  });

  it('explains instead of offering a charge for an item with no cost on record', async () => {
    const { fixture, root } = await open(hammers({ unitCostMinor: 0 }));
    await pick(fixture, root, 'from', 'warehouse');
    await pick(fixture, root, 'to', RIVERSIDE_ID);

    expect(chargeBox(root)).toBeNull();
    expect(text(root, 'form')).toContain(
      "This item has no cost on record, so this move can't be charged to the project.",
    );
  });

  it('sends the charge only for a project destination that was ticked', async () => {
    const { fixture, root, transfer } = await open();
    await pick(fixture, root, 'from', 'warehouse');
    await pick(fixture, root, 'to', RIVERSIDE_ID);
    type(root, 'app-number-input input', '4');
    chargeBox(root)!.click();
    await fixture.whenStable();
    // The person changes their mind about the place after ticking: no charge goes to the warehouse.
    await pick(fixture, root, 'to', 'warehouse');

    submitButton(root).click();
    await fixture.whenStable();

    expect(transfer.mock.calls[0]![1]).toMatchObject({ chargeToProject: false });
  });
});

describe('MoveStockDialog: sending', () => {
  it('sends the places, quantity, date, charge, note and the version of the item it was opened on', async () => {
    const { fixture, root, transfer, done } = await open();
    await pick(fixture, root, 'from', 'warehouse');
    await pick(fixture, root, 'to', RIVERSIDE_ID);
    type(root, 'app-number-input input', '4');
    chargeBox(root)!.click();
    type(root, 'textarea', '  For the formwork crew  ');
    await fixture.whenStable();

    submitButton(root).click();
    await fixture.whenStable();
    await settleDone(fixture);

    expect(transfer).toHaveBeenCalledTimes(1);
    const [id, request] = transfer.mock.calls[0] as [string, Record<string, unknown>];
    expect(id).toBe('item-hammer');
    expect(request).toMatchObject({
      fromLocation: { kind: 'Warehouse' },
      toLocation: { kind: 'Project', projectId: RIVERSIDE_ID },
      quantity: 4,
      date: todayIn('Asia/Manila'),
      chargeToProject: true,
      note: 'For the formwork crew',
      version: 3,
    });
    expect(request['clientRequestId']).toMatch(/^[0-9a-f-]{36}$/);
    expect(done).toHaveLength(1);
    expect(done[0]!.version).toBe(4);
  });

  it('moves a single piece as quantity 1 without the person typing anything', async () => {
    const { fixture, root, transfer } = await open(excavator());
    await pick(fixture, root, 'to', 'warehouse');

    submitButton(root).click();
    await fixture.whenStable();

    expect(transfer.mock.calls[0]![1]).toMatchObject({
      fromLocation: { kind: 'Project', projectId: RIVERSIDE_ID },
      toLocation: { kind: 'Warehouse' },
      quantity: 1,
      chargeToProject: false,
      note: null,
      version: 5,
    });
  });

  it('asks for what is missing next to each field, and sends nothing', async () => {
    const { fixture, root, transfer } = await open();

    submitButton(root).click();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(errorUnder(root, 'from')).toBe('Choose where it is coming from.');
    expect(errorUnder(root, 'to')).toBe('Choose where it is going.');
    expect(
      root.querySelector('app-number-input')!.closest('app-form-field')!.textContent,
    ).toContain('Enter how many to move.');
    expect(transfer).not.toHaveBeenCalled();
  });

  it('does not take a date in the future or before the item was bought', async () => {
    const { fixture, root, transfer } = await open();
    await pick(fixture, root, 'from', 'warehouse');
    await pick(fixture, root, 'to', RIVERSIDE_ID);
    type(root, 'app-number-input input', '4');

    type(root, '[formControlName="date"]', '2999-01-01');
    submitButton(root).click();
    await fixture.whenStable();
    fixture.detectChanges();
    expect(errorUnder(root, 'date')).toBe("The date can't be in the future.");

    type(root, '[formControlName="date"]', '2026-05-31');
    submitButton(root).click();
    await fixture.whenStable();
    fixture.detectChanges();
    expect(errorUnder(root, 'date')).toBe(
      "The date can't be before the purchase date (1 Jun 2026).",
    );
    expect(transfer).not.toHaveBeenCalled();
    const date = root.querySelector('[formControlName="date"]')!;
    expect(date.getAttribute('min')).toBe('2026-06-01');
    expect(date.getAttribute('max')).toBe(todayIn('Asia/Manila'));
  });
});

describe('MoveStockDialog: what the server refuses', () => {
  const fill = async (view: Awaited<ReturnType<typeof open>>, quantity: string) => {
    await pick(view.fixture, view.root, 'from', 'warehouse');
    await pick(view.fixture, view.root, 'to', RIVERSIDE_ID);
    type(view.root, 'app-number-input input', quantity);
    await view.fixture.whenStable();
  };

  it("shows a rule in the server's own words, keeps what was typed, and changes nothing", async () => {
    const message = 'Only 14 pc are there, but you asked for 15.';
    const view = await open(
      hammers(),
      stub(() => throwError(() => ruleViolation(message))),
    );
    await fill(view, '15');

    submitButton(view.root).click();
    await view.fixture.whenStable();
    view.fixture.detectChanges();

    expect(text(view.root, 'app-api-error-alert')).toContain(message);
    expect(view.root.querySelector('app-api-error-alert .alert--warning')).not.toBeNull();
    expect((view.root.querySelector('app-number-input input') as HTMLInputElement).value).toBe(
      '15',
    );
    expect(view.root.querySelector<HTMLSelectElement>('select[formControlName="to"]')!.value).toBe(
      RIVERSIDE_ID,
    );
    expect(view.done).toHaveLength(0);
    expect(view.dismissed).toHaveLength(0);
  });

  it('keeps a conflict message when the person edits, because only Reload resolves it', async () => {
    const view = await open(
      hammers(),
      stub(() => throwError(() => conflict())),
    );
    await fill(view, '4');
    submitButton(view.root).click();
    await view.fixture.whenStable();
    view.fixture.detectChanges();

    type(view.root, 'app-number-input input', '3');
    await view.fixture.whenStable();
    view.fixture.detectChanges();

    expect(text(view.root, 'app-api-error-alert')).toContain(
      'Someone else changed this record while you were editing.',
    );
  });

  it('drops a rule message once the person changes what it was about', async () => {
    const message = 'Only 14 pc are there, but you asked for 15.';
    const view = await open(
      hammers(),
      stub(() => throwError(() => ruleViolation(message))),
    );
    await fill(view, '15');
    submitButton(view.root).click();
    await view.fixture.whenStable();
    view.fixture.detectChanges();
    expect(text(view.root, 'app-api-error-alert')).toContain(message);

    type(view.root, 'app-number-input input', '14');
    await view.fixture.whenStable();
    view.fixture.detectChanges();

    expect(view.root.querySelector('app-api-error-alert .alert')).toBeNull();
  });

  it('shows the refusal to move onto an archived project, after warning about it', async () => {
    const message =
      'The project "Old School Annex" is archived. Restore it before moving stock there.';
    const view = await open(
      hammers(),
      stub(() => throwError(() => ruleViolation(message))),
    );
    await pick(view.fixture, view.root, 'from', 'warehouse');
    await pick(view.fixture, view.root, 'to', ANNEX_ID);
    type(view.root, 'app-number-input input', '1');
    await view.fixture.whenStable();
    view.fixture.detectChanges();

    expect(text(view.root, 'form app-alert')).toContain('This project is archived');
    expect(text(view.root, 'form app-alert')).toContain("can't receive stock until it is restored");
    expect(chargeBox(view.root)).toBeNull();

    submitButton(view.root).click();
    await view.fixture.whenStable();
    view.fixture.detectChanges();

    expect(text(view.root, 'app-api-error-alert')).toContain(message);
  });

  it('puts a server message about a field under that field', async () => {
    const view = await open(
      hammers(),
      stub(() =>
        throwError(() =>
          fieldProblem({
            date: ["The date can't be before the purchase date (1 Jun 2026)."],
            toLocation: [
              "That project isn't one of your company's projects. Choose a project from the list.",
            ],
          }),
        ),
      ),
    );
    await fill(view, '4');

    submitButton(view.root).click();
    await view.fixture.whenStable();
    view.fixture.detectChanges();

    expect(errorUnder(view.root, 'date')).toBe(
      "The date can't be before the purchase date (1 Jun 2026).",
    );
    expect(errorUnder(view.root, 'to')).toBe(
      "That project isn't one of your company's projects. Choose a project from the list.",
    );
  });

  it('shows a message about the charge above the form, since the charge has no field of its own', async () => {
    const view = await open(
      hammers(),
      stub(() =>
        throwError(() =>
          fieldProblem({
            chargeToProject: [
              "The charge works out to nothing at this quantity and unit cost. Leave the charge off, or check the item's unit cost.",
            ],
          }),
        ),
      ),
    );
    await fill(view, '4');

    submitButton(view.root).click();
    await view.fixture.whenStable();
    view.fixture.detectChanges();

    expect(text(view.root, 'app-api-error-alert')).toContain('The charge works out to nothing');
  });

  it('says someone else changed the item, with a Reload that keeps the form and sends the fresh version', async () => {
    const transfer = stub(() => of(hammers({ version: 8 }))).mockReturnValueOnce(
      throwError(() => conflict()),
    );
    const view = await open(hammers(), transfer);
    await fill(view, '4');

    submitButton(view.root).click();
    await view.fixture.whenStable();
    view.fixture.detectChanges();
    expect(text(view.root, 'app-api-error-alert')).toContain(
      'Someone else changed this record while you were editing.',
    );
    expect(view.root.querySelector('app-api-error-alert .alert--warning')).not.toBeNull();

    // The page reloads the item and hands the dialog the fresh one.
    buttonNamed(view.root, 'Reload item').click();
    view.fixture.componentRef.setInput(
      'item',
      hammers({ version: 7, balances: [warehouse(13), onProject(10)], totalQuantity: 23 }),
    );
    await view.fixture.whenStable();
    view.fixture.detectChanges();

    expect(view.reloaded).toHaveLength(1);
    expect(view.root.querySelector('app-api-error-alert .alert')).toBeNull();
    expect(texts(view.root, 'select[formControlName="from"] option')).toContain(
      'Warehouse (13 pc)',
    );
    expect((view.root.querySelector('app-number-input input') as HTMLInputElement).value).toBe('4');

    submitButton(view.root).click();
    await view.fixture.whenStable();

    const [first, second] = transfer.mock.calls.map(
      ([, request]) => request as Record<string, unknown>,
    );
    expect(first).toMatchObject({ version: 3 });
    expect(second).toMatchObject({ version: 7 });
    // Nothing was done by the refused request, so the same request id is still the right one.
    expect(second!['clientRequestId']).toBe(first!['clientRequestId']);
  });
});

describe('MoveStockDialog: the request id', () => {
  it('stays the same after a failure so a retry can never move the stock twice', async () => {
    const transfer = stub(() => of(hammers({ version: 4 }))).mockReturnValueOnce(
      throwError(
        () =>
          new ApiError({
            status: 500,
            title: 'Something went wrong',
            detail: "We couldn't finish that. Try again in a moment.",
            traceId: 'ref-9',
          }),
      ),
    );
    const view = await open(hammers(), transfer);
    await pick(view.fixture, view.root, 'from', 'warehouse');
    await pick(view.fixture, view.root, 'to', RIVERSIDE_ID);
    type(view.root, 'app-number-input input', '4');

    submitButton(view.root).click();
    await view.fixture.whenStable();
    view.fixture.detectChanges();
    expect(text(view.root, 'app-api-error-alert')).toContain('Reference: ref-9');

    submitButton(view.root).click();
    await view.fixture.whenStable();

    const ids = transfer.mock.calls.map(
      ([, request]) => (request as { clientRequestId: string }).clientRequestId,
    );
    expect(ids).toHaveLength(2);
    expect(ids[1]).toBe(ids[0]);
    expect(view.done).toHaveLength(1);
  });

  it('is new for each dialog that is opened', async () => {
    const first = await open();
    await pick(first.fixture, first.root, 'from', 'warehouse');
    await pick(first.fixture, first.root, 'to', RIVERSIDE_ID);
    type(first.root, 'app-number-input input', '4');
    submitButton(first.root).click();
    await first.fixture.whenStable();
    const firstId = (first.transfer.mock.calls[0]![1] as { clientRequestId: string })
      .clientRequestId;

    TestBed.resetTestingModule();
    const second = await open();
    await pick(second.fixture, second.root, 'from', 'warehouse');
    await pick(second.fixture, second.root, 'to', RIVERSIDE_ID);
    type(second.root, 'app-number-input input', '4');
    submitButton(second.root).click();
    await second.fixture.whenStable();
    const secondId = (second.transfer.mock.calls[0]![1] as { clientRequestId: string })
      .clientRequestId;

    expect(secondId).not.toBe(firstId);
  });
});

describe('MoveStockDialog as the "Move to the warehouse" shortcut', () => {
  it('fixes both places, offers everything that is on the project, and sends no charge', async () => {
    const { root, transfer, fixture } = await open(hammers(), undefined, {
      returnFrom: RIVERSIDE_ID,
    });

    expect(text(root, 'h2')).toBe('Move to the warehouse');
    expect(root.querySelector('select')).toBeNull();
    expect(texts(root, '.def-list dd')).toEqual(['Riverside Residences (10 pc)', 'Warehouse']);
    expect((root.querySelector('app-number-input input') as HTMLInputElement).value).toBe('10');
    expect(chargeBox(root)).toBeNull();

    submitButton(root).click();
    await fixture.whenStable();

    expect(transfer.mock.calls[0]![1]).toMatchObject({
      fromLocation: { kind: 'Project', projectId: RIVERSIDE_ID },
      toLocation: { kind: 'Warehouse' },
      quantity: 10,
      chargeToProject: false,
    });
  });

  it('moves a single piece back as 1', async () => {
    const { transfer, fixture, root } = await open(excavator(), undefined, {
      returnFrom: RIVERSIDE_ID,
    });

    expect(quantityBox(root)).toBeNull();
    submitButton(root).click();
    await fixture.whenStable();

    expect(transfer.mock.calls[0]![1]).toMatchObject({ quantity: 1 });
  });
});

/** Lets the answer of a send reach the dialog's outputs. */
async function settleDone(fixture: { whenStable(): Promise<unknown> }): Promise<void> {
  await fixture.whenStable();
  await Promise.resolve();
}
