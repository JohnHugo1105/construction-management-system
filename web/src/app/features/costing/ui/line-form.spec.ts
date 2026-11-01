import { ApiError } from '../../../core/api/api-error';
import { applyServerErrors } from '../../../shared/forms/server-errors';
import { budgetLineFixture, costEntryFixture, referenceFixture } from '../testing/costing-fixtures';
import {
  CUSTOM_CODE,
  LineForm,
  buildLineFields,
  createLineForm,
  duplicateInvoiceDate,
  formValueFromLine,
  isDuplicateInvoiceMessage,
  resetClassification,
  scopesOf,
  routeServerErrors,
} from './line-form';

const divisions = referenceFixture().divisions;
const TODAY = '2026-11-02';

function fill(form: LineForm, values: Partial<ReturnType<LineForm['getRawValue']>>): void {
  form.patchValue({
    description: 'Ready-mix concrete',
    itemType: 'Material',
    quantity: 600,
    unit: 'm³',
    unitCostMinor: 580000,
    vatTreatment: 'Inclusive',
    ...values,
  });
}

describe('createLineForm', () => {
  it('starts with the required choices empty, so a leftover default cannot be saved by mistake', () => {
    const form = createLineForm('budget', TODAY);

    expect(form.controls.itemType.value).toBe('');
    expect(form.controls.vatTreatment.value).toBe('');
    expect(form.controls.entryDate.value).toBe(TODAY);
    expect(form.valid).toBe(false);
  });

  it('is valid for a budget line without a supplier, and a cost needs both supplier and invoice', () => {
    const budget = createLineForm('budget', TODAY);
    fill(budget, {});
    expect(budget.valid).toBe(true);

    const cost = createLineForm('cost', TODAY);
    fill(cost, {});
    expect(cost.valid).toBe(false);
    expect(cost.controls.sourceDealer.errors).toEqual({ required: true });
    expect(cost.controls.invoiceNumber.errors).toEqual({ required: true });
    cost.patchValue({ sourceDealer: 'Metro Concrete', invoiceNumber: 'MCB-1' });
    expect(cost.valid).toBe(true);
  });

  it('refuses a quantity of zero or below, and one above the server limit, in plain words', () => {
    const form = createLineForm('budget', TODAY);
    form.controls.quantity.setValue(0);
    expect(form.controls.quantity.errors).toEqual({
      message: 'Enter a quantity greater than zero.',
    });
    form.controls.quantity.setValue(1_000_000_000);
    expect(form.controls.quantity.errors).toEqual({
      message: 'Enter a quantity below 1,000,000,000.',
    });
    form.controls.quantity.setValue(0.0001);
    expect(form.controls.quantity.errors).toBeNull();
  });

  it('refuses a cost dated after today, and says to use the date on the invoice', () => {
    const cost = createLineForm('cost', TODAY);
    cost.controls.entryDate.setValue('2026-11-03');
    expect(cost.controls.entryDate.errors).toEqual({
      message: "The date can't be in the future. Use the date on the invoice or receipt.",
    });
    cost.controls.entryDate.setValue(TODAY);
    expect(cost.controls.entryDate.errors).toBeNull();

    // A budget line may be dated ahead.
    const budget = createLineForm('budget', TODAY);
    budget.controls.entryDate.setValue('2026-12-01');
    expect(budget.controls.entryDate.errors).toBeNull();
  });

  it('needs a work item once a division is chosen, and the code and sub-scope name for a custom code', () => {
    const form = createLineForm('budget', TODAY);
    fill(form, {});

    form.controls.division.setValue('3');
    expect(form.controls.wbsChoice.errors).toEqual({
      message: 'Choose the work item, or use a custom code.',
    });

    form.controls.wbsChoice.setValue(CUSTOM_CODE);
    expect(form.controls.wbsCode.errors).toEqual({ message: 'Enter the work-breakdown code.' });
    expect(form.controls.subScope.errors).toEqual({
      message: 'Enter the name of the sub-scope this code covers.',
    });

    form.controls.wbsCode.setValue('3999');
    form.controls.subScope.setValue('Special slab');
    expect(form.valid).toBe(true);
  });

  it('clears the work item when the division changes', () => {
    const form = createLineForm('budget', TODAY);
    form.patchValue({ division: '3', wbsChoice: '3300' });

    form.controls.division.setValue('15');
    resetClassification(form);

    expect(form.controls.wbsChoice.value).toBe('');
    expect(form.controls.wbsCode.value).toBe('');
  });
});

describe('buildLineFields', () => {
  it('sends a standard code with its own division and names, trimmed text and amounts in minor units', () => {
    const form = createLineForm('budget', TODAY);
    fill(form, {
      subProjectId: 'sub-1',
      division: '3',
      wbsChoice: '3300',
      description: '  Ready-mix concrete 4000 psi ',
      quantity: 123.4567,
      remarks: '   ',
    });

    expect(buildLineFields('budget', form.getRawValue(), divisions)).toEqual({
      subProjectId: 'sub-1',
      division: 3,
      mainScope: 'Concrete',
      subScope: 'Cast-In-Place Concrete',
      wbsCode: '3300',
      description: 'Ready-mix concrete 4000 psi',
      itemType: 'Material',
      quantity: 123.4567,
      unit: 'm³',
      unitCostMinor: 580000,
      vatTreatment: 'Inclusive',
      entryDate: TODAY,
      remarks: null,
    });
  });

  it('sends the sub-scope name the person typed for a custom code', () => {
    const form = createLineForm('budget', TODAY);
    fill(form, {
      division: '3',
      wbsChoice: CUSTOM_CODE,
      wbsCode: ' 3999 ',
      subScope: ' Special slab ',
    });

    expect(buildLineFields('budget', form.getRawValue(), divisions)).toMatchObject({
      division: 3,
      mainScope: 'Concrete',
      wbsCode: '3999',
      subScope: 'Special slab',
    });
  });

  it('sends no classification at all when no division is chosen', () => {
    const form = createLineForm('budget', TODAY);
    fill(form, {});

    expect(buildLineFields('budget', form.getRawValue(), divisions)).toMatchObject({
      subProjectId: null,
      division: null,
      mainScope: null,
      subScope: null,
      wbsCode: null,
    });
  });

  it('adds the supplier and invoice number for a cost only', () => {
    const cost = createLineForm('cost', TODAY);
    fill(cost, { sourceDealer: ' Metro Concrete ', invoiceNumber: ' MCB-1 ' });
    expect(buildLineFields('cost', cost.getRawValue(), divisions)).toMatchObject({
      sourceDealer: 'Metro Concrete',
      invoiceNumber: 'MCB-1',
    });

    const budget = createLineForm('budget', TODAY);
    fill(budget, { sourceDealer: 'ignored' });
    expect(buildLineFields('budget', budget.getRawValue(), divisions)).not.toHaveProperty(
      'sourceDealer',
    );
  });
});

describe('formValueFromLine', () => {
  it('fills a standard code as the work item and leaves the custom fields empty', () => {
    const value = formValueFromLine(budgetLineFixture({ subProjectId: 's1' }), divisions);

    expect(value).toMatchObject({
      subProjectId: 's1',
      division: '3',
      wbsChoice: '3300',
      wbsCode: '',
      subScope: '',
      quantity: 600,
      unitCostMinor: 580000,
      itemType: 'Material',
      vatTreatment: 'Inclusive',
    });
  });

  it('recognises a custom code and shows it as a custom code', () => {
    const value = formValueFromLine(
      budgetLineFixture({ division: 3, wbsCode: '3999', subScope: 'Special slab' }),
      divisions,
    );

    expect(value).toMatchObject({
      division: '3',
      wbsChoice: CUSTOM_CODE,
      wbsCode: '3999',
      subScope: 'Special slab',
    });
  });

  it('shows an unclassified line with no division', () => {
    const value = formValueFromLine(
      budgetLineFixture({ division: null, wbsCode: null, subScope: null, mainScope: null }),
      divisions,
    );

    expect(value).toMatchObject({ division: '', wbsChoice: '' });
  });

  it('carries a cost entry supplier and invoice number', () => {
    expect(formValueFromLine(costEntryFixture(), divisions)).toMatchObject({
      sourceDealer: 'Cebu Plumbing Supply',
      invoiceNumber: 'CPS-7781',
    });
  });

  it('lists the codes a division offers', () => {
    expect(scopesOf(divisions, '3').map((scope) => scope.code)).toEqual(['3100', '3300']);
    expect(scopesOf(divisions, '')).toEqual([]);
  });
});

describe('server field errors', () => {
  function problem(errors: Record<string, string[]>): ApiError {
    return new ApiError({
      status: 422,
      title: 'Some details need your attention',
      detail: 'Check the highlighted fields and try again.',
      code: 'validation_failed',
      errors,
    });
  }

  /** What the dialog does with a refused save. */
  function show(form: LineForm, failure: ApiError): string[] {
    return applyServerErrors(form, {
      errors: routeServerErrors(failure.errors, form.getRawValue()),
    });
  }

  it('puts every message under the field it is about, all at once', () => {
    const form = createLineForm('cost', TODAY);
    fill(form, { sourceDealer: 'Metro', invoiceNumber: 'MCB-1' });

    const unmatched = show(
      form,
      problem({
        description: ['Enter a description of the item.'],
        unitCostMinor: ["The unit cost can't be negative."],
        entryDate: ["The date can't be in the future."],
        invoiceNumber: [
          'This supplier invoice is already recorded for this project, dated 3 Dec 2026.',
        ],
        vatRateBps: ['Enter a VAT rate between 0% and 100%.'],
      }),
    );

    expect(form.controls.description.errors).toEqual({
      server: 'Enter a description of the item.',
    });
    expect(form.controls.unitCostMinor.errors).toEqual({
      server: "The unit cost can't be negative.",
    });
    expect(form.controls.entryDate.errors).toEqual({ server: "The date can't be in the future." });
    expect(form.controls.invoiceNumber.errors?.['server']).toContain('already recorded');
    // A field this form does not have is handed back so the page can still show it.
    expect(unmatched).toEqual(['Enter a VAT rate between 0% and 100%.']);
  });

  it('puts messages about the code under the work item when a standard code is used, joined rather than overwritten', () => {
    const form = createLineForm('budget', TODAY);
    fill(form, { division: '3', wbsChoice: '3300' });

    const unmatched = show(
      form,
      problem({
        wbsCode: ['Code 3300 belongs to Division 3.'],
        subScope: ['Enter its sub-scope name.'],
      }),
    );

    expect(unmatched).toEqual([]);
    expect(form.controls.wbsChoice.errors).toEqual({
      server: 'Code 3300 belongs to Division 3. Enter its sub-scope name.',
    });
  });

  it('puts messages about the code under the code and sub-scope inputs when a custom code is used', () => {
    const form = createLineForm('budget', TODAY);
    fill(form, { division: '3', wbsChoice: CUSTOM_CODE, wbsCode: '3999', subScope: 'Slab' });

    show(
      form,
      problem({
        wbsCode: ['The work-breakdown code can be at most 20 characters.'],
        subScope: ['The sub-scope can be at most 160 characters.'],
      }),
    );

    expect(form.controls.wbsCode.errors?.['server']).toContain('at most 20 characters');
    expect(form.controls.subScope.errors?.['server']).toContain('at most 160 characters');
  });

  it('keeps what the person typed when the server refuses', () => {
    const form = createLineForm('budget', TODAY);
    fill(form, { description: 'Rebar', quantity: 5, unitCostMinor: 123 });

    show(form, problem({ description: ['Too vague.'] }));

    expect(form.controls.description.value).toBe('Rebar');
    expect(form.controls.quantity.value).toBe(5);
    expect(form.controls.unitCostMinor.value).toBe(123);
  });
});

describe('duplicateInvoiceDate', () => {
  it('reads the date the server names for the earlier entry', () => {
    expect(
      duplicateInvoiceDate(
        'This supplier invoice is already recorded for this project, dated 3 Dec 2026. Check it.',
      ),
    ).toBe('2026-12-03');
    expect(duplicateInvoiceDate('... dated 21 Jan 2027.')).toBe('2027-01-21');
    expect(duplicateInvoiceDate('... dated 1 nov 2026.')).toBe('2026-11-01');
  });

  it('gives nothing when there is no date to read', () => {
    expect(
      duplicateInvoiceDate('This supplier invoice is already recorded for this project.'),
    ).toBeNull();
    expect(duplicateInvoiceDate('... dated 3 Foo 2026.')).toBeNull();
    expect(duplicateInvoiceDate(null)).toBeNull();
  });
});

describe('isDuplicateInvoiceMessage', () => {
  it('recognises the "already recorded" answer and nothing else', () => {
    expect(
      isDuplicateInvoiceMessage(
        'This supplier invoice is already recorded for this project, dated 3 Dec 2026. Check the invoice number.',
      ),
    ).toBe(true);
    expect(
      isDuplicateInvoiceMessage('The invoice number needs at least one letter or digit.'),
    ).toBe(false);
    expect(isDuplicateInvoiceMessage(null)).toBe(false);
  });
});
