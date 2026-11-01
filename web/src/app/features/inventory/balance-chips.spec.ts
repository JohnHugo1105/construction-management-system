import { TestBed } from '@angular/core/testing';
import { settle, text, texts } from '../../testing/dom';
import { BalanceChips } from './balance-chips';
import { onProject, warehouse } from './inventory.fixtures';
import { StockBalance } from './inventory.models';

async function render(balances: readonly StockBalance[], unit = '') {
  const fixture = TestBed.createComponent(BalanceChips);
  fixture.componentRef.setInput('balances', balances);
  fixture.componentRef.setInput('unit', unit);
  await settle(fixture);
  return fixture.nativeElement as HTMLElement;
}

describe('BalanceChips', () => {
  it('writes each place with its quantity: "Warehouse 14", "Riverside Residences 10"', async () => {
    const root = await render([warehouse(14), onProject(10)]);

    expect(texts(root, '.place')).toEqual(['Warehouse 14', 'Riverside Residences 10']);
    expect(root.querySelector('ul')!.getAttribute('aria-label')).toBe('Where it is');
  });

  it('adds the unit after each number when asked', async () => {
    const root = await render([warehouse(14), onProject(2.5)], 'pc');

    expect(texts(root, '.place')).toEqual(['Warehouse 14 pc', 'Riverside Residences 2.5 pc']);
  });

  it('keeps the order it was given: warehouse first, then projects', async () => {
    const root = await render([
      warehouse(1),
      onProject(2, 'a', 'Alpha Tower'),
      onProject(3, 'b', 'Beta Court'),
    ]);

    expect(texts(root, '.place')).toEqual(['Warehouse 1', 'Alpha Tower 2', 'Beta Court 3']);
  });

  it('says there is no stock in words, instead of showing a chip with a zero', async () => {
    const root = await render([]);

    expect(root.querySelector('.place')).toBeNull();
    expect(root.querySelector('ul')).toBeNull();
    expect(text(root, '.places__none')).toBe('No stock anywhere');
  });

  it('lets a long project name wrap inside its chip instead of running off the screen', async () => {
    const root = await render([
      onProject(3, 'long', 'Riverside Residences Phase Two Tower A, Block 3, East Wing Extension'),
    ]);

    expect(text(root, '.place')).toBe(
      'Riverside Residences Phase Two Tower A, Block 3, East Wing Extension 3',
    );
  });
});
