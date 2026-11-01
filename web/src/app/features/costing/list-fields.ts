import { ListQueryField, listField } from '../../shared/list-query/list-query';
import { CostOrigin, ItemType } from './costing.models';

// The query-string fields the budget and cost lists share, on top of the foundation's text, choice, flag, integer and date kinds.

const GUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

/** An identifier such as a sub-project's. Anything else in the address is ignored, so a hand-edited link never reaches the API. */
export function idField(): ListQueryField<string> {
  return {
    defaultValue: '',
    parse: (raw) => (raw !== null && GUID.test(raw) ? raw.toLowerCase() : ''),
    serialize: (value) => (value === '' ? null : value),
  };
}

/** A division from 1 to 16; 0 (the default, left out of the address) means every division. */
export function divisionField(): ListQueryField<number> {
  return listField.integer(0, 1, 16);
}

export const ITEM_TYPES: readonly ItemType[] = [
  'Material',
  'Labor',
  'Equipment',
  'Subcontract',
  'Other',
];

/** An item type, or empty for all of them. */
export function itemTypeField(): ListQueryField<'' | ItemType> {
  return listField.choice<'' | ItemType>(['', ...ITEM_TYPES], '');
}

export const COST_ORIGINS: readonly CostOrigin[] = ['Manual', 'Payroll', 'Inventory', 'Subcontract'];

/** Where a cost came from, or empty for all of them. */
export function originField(): ListQueryField<'' | CostOrigin> {
  return listField.choice<'' | CostOrigin>(['', ...COST_ORIGINS], '');
}
