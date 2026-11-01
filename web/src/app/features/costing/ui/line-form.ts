import {
  AbstractControl,
  FormControl,
  FormGroup,
  ValidationErrors,
  Validators,
} from '@angular/forms';
import {
  BudgetLine,
  CostEntry,
  CostEntryFields,
  ItemType,
  LineFields,
  VatTreatment,
  WbsDivision,
} from '../costing.models';

// The form shared by a budget line and a cost entry. They differ only in the supplier and invoice a cost needs and in the date
// rule, so one form and one request builder serve both.

export type LineKind = 'budget' | 'cost';

/** The "work item" choice that means "my code is not in the standard list". */
export const CUSTOM_CODE = '__custom__';

export function createLineForm(kind: LineKind, today: string) {
  const supplier = kind === 'cost';
  const form = new FormGroup({
    subProjectId: new FormControl('', { nonNullable: true }),
    division: new FormControl('', { nonNullable: true }),
    wbsChoice: new FormControl('', { nonNullable: true, validators: [requiredWithDivision] }),
    wbsCode: new FormControl('', {
      nonNullable: true,
      validators: [
        requiredWithCustomCode('Enter the work-breakdown code.'),
        Validators.maxLength(20),
      ],
    }),
    subScope: new FormControl('', {
      nonNullable: true,
      validators: [
        requiredWithCustomCode('Enter the name of the sub-scope this code covers.'),
        Validators.maxLength(160),
      ],
    }),
    description: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(200)],
    }),
    itemType: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    quantity: new FormControl<number | null>(null, {
      validators: [Validators.required, quantityAboveZero, quantityBelowLimit],
    }),
    unit: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(20)],
    }),
    unitCostMinor: new FormControl<number | null>(null, { validators: [Validators.required] }),
    vatTreatment: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    entryDate: new FormControl(today, {
      nonNullable: true,
      validators: [Validators.required, ...(kind === 'cost' ? [notAfter(today)] : [])],
    }),
    remarks: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(500)] }),
    sourceDealer: new FormControl('', {
      nonNullable: true,
      validators: supplier ? [Validators.required, Validators.maxLength(120)] : [],
    }),
    invoiceNumber: new FormControl('', {
      nonNullable: true,
      validators: supplier ? [Validators.required, Validators.maxLength(60)] : [],
    }),
    // The approved purchase order line this invoice is for ('' for none). Only a cost carries it.
    purchaseOrderLineId: new FormControl('', { nonNullable: true }),
  });

  // The code and its names are checked against the division and the work item, so check them again when those change.
  form.controls.division.valueChanges.subscribe(() =>
    form.controls.wbsChoice.updateValueAndValidity({ emitEvent: false }),
  );
  form.controls.wbsChoice.valueChanges.subscribe(() => {
    form.controls.wbsCode.updateValueAndValidity({ emitEvent: false });
    form.controls.subScope.updateValueAndValidity({ emitEvent: false });
  });
  return form;
}

export type LineForm = ReturnType<typeof createLineForm>;
export type LineFormValue = ReturnType<LineForm['getRawValue']>;

// ---- Validators ------------------------------------------------------------------------------------------------------------

function requiredWithDivision(control: AbstractControl): ValidationErrors | null {
  const division = control.parent?.get('division')?.value;
  return division && !control.value
    ? { message: 'Choose the work item, or use a custom code.' }
    : null;
}

function requiredWithCustomCode(message: string) {
  return (control: AbstractControl): ValidationErrors | null => {
    const choice = control.parent?.get('wbsChoice')?.value;
    return choice === CUSTOM_CODE && String(control.value ?? '').trim() === '' ? { message } : null;
  };
}

function quantityAboveZero(control: AbstractControl): ValidationErrors | null {
  const value = control.value;
  return typeof value === 'number' && value <= 0
    ? { message: 'Enter a quantity greater than zero.' }
    : null;
}

function quantityBelowLimit(control: AbstractControl): ValidationErrors | null {
  const value = control.value;
  return typeof value === 'number' && value >= 1_000_000_000
    ? { message: 'Enter a quantity below 1,000,000,000.' }
    : null;
}

/** A cost can't be dated after today. The server decides in the company's time zone; this only saves a round trip. */
function notAfter(today: string) {
  return (control: AbstractControl): ValidationErrors | null => {
    const value = control.value;
    return typeof value === 'string' && value !== '' && value > today
      ? { message: "The date can't be in the future. Use the date on the invoice or receipt." }
      : null;
  };
}

// ---- Classification --------------------------------------------------------------------------------------------------------

/** The scopes (codes) a division offers, in the order the reference lists them. */
export function scopesOf(divisions: readonly WbsDivision[], division: string) {
  return divisions.find((item) => String(item.number) === division)?.scopes ?? [];
}

export function isCustomCode(value: Pick<LineFormValue, 'division' | 'wbsChoice'>): boolean {
  return value.division !== '' && value.wbsChoice === CUSTOM_CODE;
}

/** Choosing another division clears the work item: the old code belongs to the old division. */
export function resetClassification(form: LineForm): void {
  form.controls.wbsChoice.setValue('');
  form.controls.wbsCode.setValue('');
  form.controls.subScope.setValue('');
}

// ---- To and from the API ---------------------------------------------------------------------------------------------------

/** The form's values for a record being edited. */
export function formValueFromLine(
  line: BudgetLine | CostEntry,
  divisions: readonly WbsDivision[],
): Partial<LineFormValue> {
  const division = line.division === null ? '' : String(line.division);
  const standard =
    division !== '' && scopesOf(divisions, division).some((scope) => scope.code === line.wbsCode);
  const custom = division !== '' && !standard;
  return {
    subProjectId: line.subProjectId ?? '',
    division,
    wbsChoice: division === '' ? '' : standard ? (line.wbsCode ?? '') : CUSTOM_CODE,
    wbsCode: custom ? (line.wbsCode ?? '') : '',
    subScope: custom ? (line.subScope ?? '') : '',
    description: line.description,
    itemType: line.itemType,
    quantity: line.quantity,
    unit: line.unit,
    unitCostMinor: line.unitCostMinor,
    vatTreatment: line.vatTreatment,
    entryDate: line.entryDate,
    remarks: line.remarks ?? '',
    sourceDealer: 'sourceDealer' in line ? line.sourceDealer : '',
    invoiceNumber: 'invoiceNumber' in line ? (line.invoiceNumber ?? '') : '',
    purchaseOrderLineId: 'purchaseOrderLineId' in line ? (line.purchaseOrderLineId ?? '') : '',
  };
}

/**
 * What the API is sent. A standard code names its own division and scopes; a custom code carries the sub-scope the person
 * typed. Call it only when the form is valid (quantity and unit cost are then numbers).
 */
export function buildLineFields(
  kind: LineKind,
  value: LineFormValue,
  divisions: readonly WbsDivision[],
): LineFields | CostEntryFields {
  const division = value.division === '' ? null : Number(value.division);
  const owner = divisions.find((item) => item.number === division);
  const standard = !isCustomCode(value)
    ? owner?.scopes.find((s) => s.code === value.wbsChoice)
    : undefined;

  let classification: Pick<LineFields, 'division' | 'mainScope' | 'subScope' | 'wbsCode'>;
  if (division === null) {
    classification = { division: null, mainScope: null, subScope: null, wbsCode: null };
  } else if (isCustomCode(value)) {
    classification = {
      division,
      mainScope: owner?.mainScope ?? null,
      subScope: value.subScope.trim(),
      wbsCode: value.wbsCode.trim(),
    };
  } else {
    classification = {
      division,
      mainScope: owner?.mainScope ?? null,
      subScope: standard?.name ?? null,
      wbsCode: standard?.code ?? null,
    };
  }

  const fields: LineFields = {
    subProjectId: value.subProjectId === '' ? null : value.subProjectId,
    ...classification,
    description: value.description.trim(),
    itemType: value.itemType as ItemType,
    quantity: value.quantity ?? 0,
    unit: value.unit.trim(),
    unitCostMinor: value.unitCostMinor ?? 0,
    vatTreatment: value.vatTreatment as VatTreatment,
    entryDate: value.entryDate,
    remarks: value.remarks.trim() === '' ? null : value.remarks.trim(),
  };
  return kind === 'cost'
    ? {
        ...fields,
        sourceDealer: value.sourceDealer.trim(),
        invoiceNumber: value.invoiceNumber.trim(),
        ...(value.purchaseOrderLineId === '' ? {} : { purchaseOrderLineId: value.purchaseOrderLineId }),
      }
    : fields;
}

/**
 * The API names fields as it received them, and the form names the work item choice differently from the code it stands for.
 * A server message about the code or its sub-scope belongs under the input the person is actually using: the work item choice
 * for a standard code (several messages for it are joined, not overwritten), the code and sub-scope inputs for a custom one.
 * Returns the errors keyed by form control name, ready for `applyServerErrors`.
 */
export function routeServerErrors(
  errors: Readonly<Record<string, readonly string[]>>,
  value: Pick<LineFormValue, 'division' | 'wbsChoice'>,
): Record<string, string[]> {
  const custom = isCustomCode(value);
  const routed: Record<string, string[]> = {};
  for (const [field, messages] of Object.entries(errors)) {
    const target = !custom && (field === 'wbsCode' || field === 'subScope') ? 'wbsChoice' : field;
    routed[target] = [...(routed[target] ?? []), ...messages];
  }
  return routed;
}

/** The server's "this supplier invoice is already recorded" message, which names the date of the earlier entry. */
export function isDuplicateInvoiceMessage(message: string | null | undefined): boolean {
  return typeof message === 'string' && /already recorded/i.test(message);
}

const MONTHS = ['jan', 'feb', 'mar', 'apr', 'may', 'jun', 'jul', 'aug', 'sep', 'oct', 'nov', 'dec'];

/**
 * The date the duplicate-invoice message gives for the earlier entry ("dated 3 Dec 2026" gives "2026-12-03"), or null when the
 * message names none. It lets "Open the existing entry" show that day's entries instead of searching for text the server
 * matched more loosely (it ignores case, spaces and punctuation in an invoice number; a text search does not).
 */
export function duplicateInvoiceDate(message: string | null | undefined): string | null {
  const match = /\bdated (\d{1,2}) ([A-Za-z]{3}) (\d{4})\b/.exec(message ?? '');
  if (!match) return null;
  const month = MONTHS.indexOf(match[2]!.toLowerCase()) + 1;
  const day = Number(match[1]);
  if (month === 0 || day < 1 || day > 31) return null;
  return `${match[3]}-${String(month).padStart(2, '0')}-${String(day).padStart(2, '0')}`;
}
