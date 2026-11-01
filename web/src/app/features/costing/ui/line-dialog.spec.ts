import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { Observable, of, throwError } from 'rxjs';
import type { Mock } from 'vitest';
import { ApiError } from '../../../core/api/api-error';
import { SessionState } from '../../../core/session/session-state';
import { buttonNamed, settle, text, type } from '../../../testing/dom';
import { signInForTest } from '../../../testing/session-fixture';
import { ProjectContext } from '../../projects/project-context';
import { ProjectsApi } from '../../projects/projects-api';
import { CostingApi } from '../costing-api';
import { BudgetLine, CostEntry } from '../costing.models';
import {
  PROJECT_ID,
  SUB_PROJECT_ID,
  budgetLineFixture,
  costEntryFixture,
  projectFixture,
  referenceFixture,
} from '../testing/costing-fixtures';
import { LineDialog } from './line-dialog';
import { LineKind } from './line-form';

type Call = (...args: unknown[]) => Observable<unknown>;
const stub = (implementation: Call): Mock<Call> => vi.fn<Call>(implementation);

interface Options {
  readonly kind?: LineKind;
  readonly line?: BudgetLine | CostEntry | null;
  readonly api?: Record<string, Mock<Call>>;
}

async function open(options: Options = {}) {
  const api = { reference: stub(() => of(referenceFixture())), ...options.api };
  TestBed.configureTestingModule({
    providers: [
      provideRouter([]),
      provideHttpClient(),
      provideHttpClientTesting(),
      ProjectContext,
      { provide: ProjectsApi, useValue: {} },
      { provide: CostingApi, useValue: api },
    ],
  });
  await signInForTest(TestBed.inject(SessionState), TestBed.inject(HttpTestingController));
  TestBed.inject(ProjectContext).replace(projectFixture());

  const fixture = TestBed.createComponent(LineDialog);
  fixture.componentRef.setInput('kind', options.kind ?? 'budget');
  fixture.componentRef.setInput('line', options.line ?? null);
  const saved = vi.fn();
  const findExisting = vi.fn();
  const refreshed = vi.fn();
  fixture.componentInstance.saved.subscribe(saved);
  fixture.componentInstance.findExisting.subscribe(findExisting);
  fixture.componentInstance.refreshed.subscribe(refreshed);
  await settle(fixture);
  return {
    fixture,
    root: fixture.nativeElement as HTMLElement,
    api,
    saved,
    findExisting,
    refreshed,
  };
}

const choose = (root: HTMLElement, selector: string, value: string) => {
  const select = root.querySelector<HTMLSelectElement>(selector)!;
  select.value = value;
  select.dispatchEvent(new Event('change', { bubbles: true }));
};
/** Chooses the radio choice whose label says this (the radios carry no value attribute to find them by). */
const pick = (root: HTMLElement, label: string) => {
  const choice = Array.from(root.querySelectorAll<HTMLElement>('label.choice')).find(
    (item) => item.querySelector('.choice__label')?.textContent?.trim() === label,
  );
  if (!choice) throw new Error(`No choice named "${label}"`);
  choice.querySelector<HTMLInputElement>('input')!.click();
};
const checkedChoice = (root: HTMLElement, name: string): string =>
  root
    .querySelector(`input[formControlName="${name}"]:checked`)
    ?.closest('label')
    ?.querySelector('.choice__label')
    ?.textContent?.trim() ?? '';
const errorUnder = (root: HTMLElement, selector: string): string =>
  text(root.querySelector(selector)!.closest('app-form-field, app-field-group')!, '.field__error');

/** Fills the fields every line needs. */
function fillLine(root: HTMLElement, description = 'Ready-mix concrete 4000 psi') {
  type(root, '[formControlName="description"]', description);
  pick(root, 'Material');
  type(root, 'app-number-input input', '600');
  type(root, '[formControlName="unit"]', 'm³');
  type(root, 'app-money-input input', '5,800.00');
  pick(root, 'Unit cost includes VAT');
}

function validation(errors: Record<string, string[]>): ApiError {
  return new ApiError({
    status: 422,
    title: 'Some details need your attention',
    detail: 'Check the highlighted fields and try again.',
    code: 'validation_failed',
    errors,
  });
}

describe('LineDialog adding a budget line', () => {
  it('sends the fields in the API words, with the money in minor units and a request id', async () => {
    const createBudgetLine = stub(() => of(budgetLineFixture()));
    const { fixture, root, saved } = await open({ api: { createBudgetLine } });

    fillLine(root);
    choose(root, '[formControlName="division"]', '3');
    await settle(fixture);
    choose(root, '[formControlName="wbsChoice"]', '3300');
    buttonNamed(root, 'Add budget line').click();
    await settle(fixture);

    expect(createBudgetLine).toHaveBeenCalledTimes(1);
    const [projectId, fields, requestId] = createBudgetLine.mock.calls[0]!;
    expect(projectId).toBe(PROJECT_ID);
    expect(fields).toMatchObject({
      subProjectId: null,
      division: 3,
      mainScope: 'Concrete',
      subScope: 'Cast-In-Place Concrete',
      wbsCode: '3300',
      description: 'Ready-mix concrete 4000 psi',
      itemType: 'Material',
      quantity: 600,
      unit: 'm³',
      unitCostMinor: 580000,
      vatTreatment: 'Inclusive',
    });
    expect(requestId).toMatch(/^[0-9a-f-]{36}$/);
    expect(saved).toHaveBeenCalledOnce();
  });

  it('shows the line total as an exact preview that is labelled as one', async () => {
    const { fixture, root } = await open();
    expect(root.querySelector('.line-total')).toBeNull();

    type(root, 'app-number-input input', '123.4567');
    type(root, 'app-money-input input', '5,800.00');
    await settle(fixture);

    expect(text(root, '.line-total__row span')).toBe('Line total (preview)');
    expect(text(root, '.line-total__row strong')).toBe('₱716,048.86');
    expect(text(root, '.line-total__note')).toContain('worked out when you save');

    type(root, 'app-number-input input', '0.1');
    type(root, 'app-money-input input', '3.00');
    await settle(fixture);
    expect(text(root, '.line-total__row strong')).toBe('₱0.30');
  });

  it('starts with the item type and VAT choices empty and says what is missing, all at once', async () => {
    const createBudgetLine = stub(() => of(null));
    const { fixture, root } = await open({ api: { createBudgetLine } });

    expect(root.querySelectorAll('input[type="radio"]:checked')).toHaveLength(0);
    buttonNamed(root, 'Add budget line').click();
    await settle(fixture);

    expect(createBudgetLine).not.toHaveBeenCalled();
    expect(text(root, 'app-alert')).toContain('Some details need your attention');
    expect(errorUnder(root, '[formControlName="description"]')).toContain('Describe the item');
    expect(errorUnder(root, 'input[formControlName="itemType"]')).toBe(
      'Choose what this line pays for.',
    );
    expect(errorUnder(root, 'app-number-input')).toBe('Enter the quantity.');
    expect(errorUnder(root, '[formControlName="unit"]')).toContain('Enter the unit');
    expect(errorUnder(root, 'app-money-input')).toBe('Enter the cost of one unit.');
    expect(errorUnder(root, 'input[formControlName="vatTreatment"]')).toBe(
      'Choose whether the unit cost includes VAT.',
    );
    expect(root.querySelectorAll('.field__error').length).toBeGreaterThanOrEqual(6);
  });

  it('asks for the sub-scope name when "Use a custom code" is chosen, and hides it otherwise', async () => {
    const { fixture, root } = await open();
    expect(root.querySelector('[formControlName="wbsChoice"]')).toBeNull();

    choose(root, '[formControlName="division"]', '3');
    await settle(fixture);
    const options = Array.from(
      root.querySelectorAll<HTMLOptionElement>('[formControlName="wbsChoice"] option'),
    ).map((option) => option.textContent!.replace(/\s+/g, ' ').trim());
    expect(options).toEqual([
      'Choose a work item',
      '3100 · Concrete Forms and Accessories',
      '3300 · Cast-In-Place Concrete',
      'Use a custom code',
    ]);
    expect(root.querySelector('[formControlName="subScope"]')).toBeNull();

    choose(root, '[formControlName="wbsChoice"]', '__custom__');
    await settle(fixture);
    expect(root.querySelector('[formControlName="wbsCode"]')).not.toBeNull();
    expect(root.querySelector('[formControlName="subScope"]')).not.toBeNull();
  });

  it('offers only sub-projects that can still receive records, and sends the one chosen', async () => {
    const createBudgetLine = stub(() => of(budgetLineFixture()));
    const { fixture, root } = await open({ api: { createBudgetLine } });

    const names = Array.from(
      root.querySelectorAll<HTMLOptionElement>('[formControlName="subProjectId"] option'),
    ).map((option) => option.textContent!.trim());
    expect(names).toEqual(['Whole project', 'Tower A']);

    fillLine(root);
    choose(root, '[formControlName="subProjectId"]', SUB_PROJECT_ID);
    buttonNamed(root, 'Add budget line').click();
    await settle(fixture);

    expect(createBudgetLine.mock.calls[0]![1]).toMatchObject({ subProjectId: SUB_PROJECT_ID });
  });
});

describe('LineDialog and the request id', () => {
  it('sends the same id on every attempt of one form, and a new one for the next form', async () => {
    const attempts = [
      () => throwError(() => validation({ description: ['Too vague.'] })),
      () => of(budgetLineFixture()),
    ];
    const createBudgetLine = stub(() => attempts.shift()!());
    const first = await open({ api: { createBudgetLine } });

    fillLine(first.root);
    buttonNamed(first.root, 'Add budget line').click();
    await settle(first.fixture);
    // The refusal keeps the dialog open with what was typed; fixing it and saving again is the same request.
    expect(errorUnder(first.root, '[formControlName="description"]')).toBe('Too vague.');
    type(first.root, '[formControlName="description"]', 'Ready-mix concrete 4000 psi, delivered');
    buttonNamed(first.root, 'Add budget line').click();
    await settle(first.fixture);

    expect(createBudgetLine).toHaveBeenCalledTimes(2);
    const firstId = createBudgetLine.mock.calls[0]![2];
    expect(createBudgetLine.mock.calls[1]![2]).toBe(firstId);
    expect(first.saved).toHaveBeenCalledOnce();

    // The next record added is a new form session, so a new request.
    const second = TestBed.createComponent(LineDialog);
    second.componentRef.setInput('kind', 'budget');
    await settle(second);
    const secondRoot = second.nativeElement as HTMLElement;
    fillLine(secondRoot);
    createBudgetLine.mockImplementation(() => of(budgetLineFixture()));
    buttonNamed(secondRoot, 'Add budget line').click();
    await settle(second);
    expect(createBudgetLine.mock.calls[2]![2]).not.toBe(firstId);
  });

  it('ignores a second tap while the first save is still running', async () => {
    let finish: (() => void) | undefined;
    const createBudgetLine = stub(
      () =>
        new Observable((subscriber) => {
          finish = () => {
            subscriber.next(budgetLineFixture());
            subscriber.complete();
          };
        }),
    );
    const { fixture, root } = await open({ api: { createBudgetLine } });

    fillLine(root);
    const save = buttonNamed(root, 'Add budget line');
    save.click();
    save.click();
    await settle(fixture);

    expect(createBudgetLine).toHaveBeenCalledTimes(1);
    finish?.();
    await settle(fixture);
  });
});

describe('LineDialog editing', () => {
  it('fills the form from the line and sends the version it loaded', async () => {
    const updateBudgetLine = stub(() => of(budgetLineFixture({ version: 5 })));
    const line = budgetLineFixture({ version: 4 });
    const { fixture, root, saved } = await open({ line, api: { updateBudgetLine } });

    expect(root.querySelector<HTMLInputElement>('[formControlName="description"]')!.value).toBe(
      'Ready-mix concrete 4000 psi',
    );
    expect(root.querySelector<HTMLSelectElement>('[formControlName="division"]')!.value).toBe('3');
    expect(root.querySelector<HTMLSelectElement>('[formControlName="wbsChoice"]')!.value).toBe(
      '3300',
    );
    expect(root.querySelector<HTMLInputElement>('app-number-input input')!.value).toBe('600');
    expect(root.querySelector<HTMLInputElement>('app-money-input input')!.value).toBe('5,800.00');
    expect(checkedChoice(root, 'itemType')).toBe('Material');
    expect(checkedChoice(root, 'vatTreatment')).toBe('Unit cost includes VAT');
    expect(text(root, 'app-field-group .field__hint')).toBe(
      'This budget line keeps the 12% VAT rate it was entered with.',
    );

    type(root, 'app-number-input input', '650');
    buttonNamed(root, 'Save budget line').click();
    await settle(fixture);

    expect(updateBudgetLine).toHaveBeenCalledWith(
      'bl-1',
      expect.objectContaining({ quantity: 650, wbsCode: '3300' }),
      4,
    );
    expect(saved).toHaveBeenCalledOnce();
  });

  it('tells the person someone else changed the line, and reloading brings the latest and its version', async () => {
    const conflict = new ApiError({
      status: 409,
      title: 'Someone else changed this',
      detail: 'Reload to see the latest, then make your change again.',
      code: 'concurrency_conflict',
    });
    const updateBudgetLine = stub(() => throwError(() => conflict));
    const latest = budgetLineFixture({ description: 'Ready-mix concrete 5000 psi', version: 9 });
    const getBudgetLine = stub(() => of(latest));
    const { fixture, root, refreshed } = await open({
      line: budgetLineFixture(),
      api: { updateBudgetLine, getBudgetLine },
    });

    buttonNamed(root, 'Save budget line').click();
    await settle(fixture);
    expect(text(root, 'app-api-error-alert')).toContain('Someone else changed this');

    buttonNamed(root, 'Reload this budget line').click();
    await settle(fixture);

    expect(getBudgetLine).toHaveBeenCalledWith('bl-1');
    expect(root.querySelector<HTMLInputElement>('[formControlName="description"]')!.value).toBe(
      'Ready-mix concrete 5000 psi',
    );
    expect(root.querySelector('app-api-error-alert')?.textContent?.trim() ?? '').toBe('');
    expect(refreshed).toHaveBeenCalledOnce();

    updateBudgetLine.mockImplementation(() => of(latest));
    buttonNamed(root, 'Save budget line').click();
    await settle(fixture);
    expect(updateBudgetLine.mock.calls[1]![2]).toBe(9);
  });
});

describe('LineDialog and messages above the form', () => {
  it('scrolls the form back to the top when the server refuses with a message above it, so it is seen', async () => {
    const scrollTo = vi.fn();
    Object.defineProperty(Element.prototype, 'scrollTo', { value: scrollTo, configurable: true });
    try {
      const updateBudgetLine = stub(() =>
        throwError(
          () =>
            new ApiError({
              status: 409,
              title: 'Someone else changed this',
              detail: 'Reload to see the latest.',
              code: 'concurrency_conflict',
            }),
        ),
      );
      const { fixture, root } = await open({
        line: budgetLineFixture(),
        api: { updateBudgetLine },
      });

      buttonNamed(root, 'Save budget line').click();
      await settle(fixture);

      expect(text(root, 'app-api-error-alert')).toContain('Someone else changed this');
      expect(scrollTo).toHaveBeenCalledWith({ top: 0, behavior: 'auto' });
    } finally {
      delete (Element.prototype as { scrollTo?: unknown }).scrollTo;
    }
  });
});

describe('LineDialog recording a cost', () => {
  async function openCost(createCostEntry: Mock<Call>) {
    const context = await open({ kind: 'cost', api: { createCostEntry } });
    fillLine(context.root, 'Pipe rental and fittings');
    type(context.root, '[formControlName="sourceDealer"]', 'Cebu Plumbing Supply');
    type(context.root, '[formControlName="invoiceNumber"]', 'CPS-7781');
    return context;
  }

  it('sends the supplier and invoice number along with the line', async () => {
    const createCostEntry = stub(() => of(costEntryFixture()));
    const { fixture, root, saved } = await openCost(createCostEntry);

    buttonNamed(root, 'Record cost').click();
    await settle(fixture);

    expect(createCostEntry).toHaveBeenCalledWith(
      PROJECT_ID,
      expect.objectContaining({
        sourceDealer: 'Cebu Plumbing Supply',
        invoiceNumber: 'CPS-7781',
        description: 'Pipe rental and fittings',
      }),
      expect.any(String),
    );
    expect(saved).toHaveBeenCalledOnce();
  });

  it('shows a duplicate invoice under the invoice field, with a way to open the existing entry', async () => {
    const message =
      'This supplier invoice is already recorded for this project, dated 3 Dec 2026. Check the invoice number, or void the earlier entry first if it was a mistake.';
    const createCostEntry = stub(() => throwError(() => validation({ invoiceNumber: [message] })));
    const { fixture, root, findExisting } = await openCost(createCostEntry);

    buttonNamed(root, 'Record cost').click();
    await settle(fixture);

    expect(errorUnder(root, '[formControlName="invoiceNumber"]')).toBe(message);
    // The message is under its field, not repeated above the form.
    expect(root.querySelector('app-api-error-alert')).toBeNull();
    expect(root.querySelector<HTMLInputElement>('[formControlName="invoiceNumber"]')!.value).toBe(
      'CPS-7781',
    );

    buttonNamed(root, 'Open the existing entry').click();
    // The date the server named comes along, so the list can show that day's entries from this supplier.
    expect(findExisting).toHaveBeenCalledWith({
      supplier: 'Cebu Plumbing Supply',
      invoiceNumber: 'CPS-7781',
      date: '2026-12-03',
    });
  });

  it('takes the "open the existing entry" offer away as soon as the invoice number changes', async () => {
    const createCostEntry = stub(() =>
      throwError(() =>
        validation({
          invoiceNumber: [
            'This supplier invoice is already recorded for this project, dated 3 Dec 2026.',
          ],
        }),
      ),
    );
    const { fixture, root } = await openCost(createCostEntry);
    buttonNamed(root, 'Record cost').click();
    await settle(fixture);
    expect(root.textContent).toContain('Open the existing entry');

    type(root, '[formControlName="invoiceNumber"]', 'CPS-7782');
    await settle(fixture);

    expect(root.textContent).not.toContain('Open the existing entry');
    expect(root.querySelectorAll('.field__error')).toHaveLength(0);
  });

  it('refuses a date in the future in plain words, before asking the server', async () => {
    const createCostEntry = stub(() => of(costEntryFixture()));
    const { fixture, root } = await openCost(createCostEntry);

    type(root, '[formControlName="entryDate"]', '2999-01-01');
    buttonNamed(root, 'Record cost').click();
    await settle(fixture);

    expect(createCostEntry).not.toHaveBeenCalled();
    expect(errorUnder(root, '[formControlName="entryDate"]')).toBe(
      "The date can't be in the future. Use the date on the invoice or receipt.",
    );
  });

  it('shows a server message about the date under the date', async () => {
    const createCostEntry = stub(() =>
      throwError(() => validation({ entryDate: ["The date can't be in the future."] })),
    );
    const { fixture, root } = await openCost(createCostEntry);

    buttonNamed(root, 'Record cost').click();
    await settle(fixture);

    expect(errorUnder(root, '[formControlName="entryDate"]')).toBe(
      "The date can't be in the future.",
    );
  });

  it('keeps the dialog open and says so when the project is archived', async () => {
    const createCostEntry = stub(() =>
      throwError(
        () =>
          new ApiError({
            status: 409,
            title: "That can't be done right now",
            detail: 'This project is archived. Restore it to record costs.',
            code: 'rule_violation',
          }),
      ),
    );
    const { fixture, root, saved } = await openCost(createCostEntry);

    buttonNamed(root, 'Record cost').click();
    await settle(fixture);

    expect(text(root, 'app-api-error-alert')).toContain('This project is archived');
    expect(saved).not.toHaveBeenCalled();
  });
});
