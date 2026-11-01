import { ChipTone } from '../../shared/ui/status-chip/status-chip';
import { IconName } from '../../shared/ui/icon/icon';
import { formatScaledDecimal } from '../../shared/presentation/scaled-decimal';
import { CostOrigin, ItemType, VatTreatment } from './costing.models';

// Words for the lists, in one place. They match the labels the API's reference data gives the forms, so a row and the form
// that edits it say the same thing.

export const ITEM_TYPE_LABELS: Record<ItemType, string> = {
  Material: 'Material',
  Labor: 'Labor',
  Equipment: 'Equipment',
  Subcontract: 'Subcontract work',
  Other: 'Other',
};

export function itemTypeLabel(itemType: string): string {
  return Object.hasOwn(ITEM_TYPE_LABELS, itemType)
    ? ITEM_TYPE_LABELS[itemType as ItemType]
    : itemType;
}

/** How the unit cost relates to VAT, short enough to sit under the unit cost. */
export const VAT_TREATMENT_SHORT: Record<VatTreatment, string> = {
  Inclusive: 'includes VAT',
  Exclusive: 'before VAT',
  Exempt: 'no VAT',
};

export function vatTreatmentShort(treatment: string): string {
  return Object.hasOwn(VAT_TREATMENT_SHORT, treatment)
    ? VAT_TREATMENT_SHORT[treatment as VatTreatment]
    : '';
}

export interface OriginPresentation {
  readonly label: string;
  readonly tone: ChipTone;
  readonly icon: IconName;
  /** Where to correct an entry that was made by the system. Empty for a manual entry. */
  readonly correctIn: string;
}

/** Where a cost came from. A system-made cost is corrected where it was made, so the row says where. */
export const ORIGIN_PRESENTATION: Record<CostOrigin, OriginPresentation> = {
  Manual: { label: 'Manual', tone: 'neutral', icon: 'pencil', correctIn: '' },
  Payroll: { label: 'Payroll', tone: 'info', icon: 'payroll', correctIn: 'Payroll' },
  Inventory: { label: 'Inventory', tone: 'info', icon: 'stock', correctIn: 'Stock' },
};

export function presentOrigin(origin: string): OriginPresentation {
  return Object.hasOwn(ORIGIN_PRESENTATION, origin)
    ? ORIGIN_PRESENTATION[origin as CostOrigin]
    : ORIGIN_PRESENTATION.Manual;
}

/** "Made by payroll: correct it in Payroll". */
export function systemMadeNote(origin: string): string {
  const presented = presentOrigin(origin);
  return presented.correctIn === ''
    ? ''
    : `Made by ${presented.label.toLowerCase()}: correct it in ${presented.correctIn}`;
}

/** A quantity as people write it: 42,000 and 123.4567, never 42,000.0000. */
export function formatQuantity(quantity: number): string {
  return formatScaledDecimal(Math.round(quantity * 10_000), 4, { trimZeros: true });
}

/** The classification of a line in words, for the line under its description: "3300 Cast-In-Place Concrete". */
export function describeClassification(line: {
  readonly wbsCode: string | null;
  readonly subScope: string | null;
  readonly mainScope: string | null;
}): string {
  if (line.wbsCode === null) return 'Not classified';
  return [line.wbsCode, line.subScope ?? line.mainScope].filter(Boolean).join(' ');
}
