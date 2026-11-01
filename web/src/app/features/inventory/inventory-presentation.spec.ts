import {
  CATEGORY_FILTERS,
  balanceLabel,
  balanceName,
  categoryFilterLabel,
  categoryRules,
  formatQuantity,
  isSinglePiece,
  quantityWithUnit,
} from './inventory-presentation';
import { onProject, warehouse } from './inventory.fixtures';

describe('categoryRules: single pieces and counted tools', () => {
  it('makes a tool counted: the serial number is optional and the person types the quantity', () => {
    expect(categoryRules('Tool')).toEqual({
      singlePiece: false,
      serialRequired: false,
      fixedQuantity: null,
    });
    expect(isSinglePiece('Tool')).toBe(false);
  });

  it.each(['Equipment', 'Machinery'] as const)(
    'makes %s a single piece: serial number required, quantity fixed at 1',
    (category) => {
      expect(categoryRules(category)).toEqual({
        singlePiece: true,
        serialRequired: true,
        fixedQuantity: 1,
      });
      expect(isSinglePiece(category)).toBe(true);
    },
  );

  it('asks for nothing special before a category is chosen', () => {
    expect(categoryRules(null)).toEqual({
      singlePiece: false,
      serialRequired: false,
      fixedQuantity: null,
    });
  });
});

describe('quantities and places', () => {
  it('writes quantities plainly, with thousands separated and at most four decimals', () => {
    expect(formatQuantity(14)).toBe('14');
    expect(formatQuantity(2.5)).toBe('2.5');
    expect(formatQuantity(1200)).toBe('1,200');
    expect(formatQuantity(0.3333)).toBe('0.3333');
  });

  it('puts the unit after the number', () => {
    expect(quantityWithUnit(14, 'pc')).toBe('14 pc');
    expect(quantityWithUnit(1, '')).toBe('1');
  });

  it('names a balance by its place, and the compact chip reads "Warehouse 14"', () => {
    expect(balanceName(warehouse(14))).toBe('Warehouse');
    expect(balanceLabel(warehouse(14))).toBe('Warehouse 14');
    expect(balanceLabel(onProject(10))).toBe('Riverside Residences 10');
  });

  it('labels the category chips in the words people use', () => {
    expect(CATEGORY_FILTERS.map(categoryFilterLabel)).toEqual([
      'All',
      'Tools',
      'Equipment',
      'Machinery',
    ]);
  });
});
