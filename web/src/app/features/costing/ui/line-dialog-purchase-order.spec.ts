import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { Observable, of, throwError } from 'rxjs';
import type { Mock } from 'vitest';
import { SessionState } from '../../../core/session/session-state';
import { buttonNamed, settle, texts, type } from '../../../testing/dom';
import { signInForTest } from '../../../testing/session-fixture';
import { ProjectContext } from '../../projects/project-context';
import { ProjectsApi } from '../../projects/projects-api';
import { PurchasingApi } from '../../purchasing/purchasing-api';
import { OpenPurchaseOrderLine } from '../../purchasing/purchasing.models';
import { CostingApi } from '../costing-api';
import { CostEntry } from '../costing.models';
import { PROJECT_ID, costEntryFixture, projectFixture, referenceFixture } from '../testing/costing-fixtures';
import { LineDialog } from './line-dialog';

type Call = (...args: unknown[]) => Observable<unknown>;
const stub = (implementation: Call): Mock<Call> => vi.fn<Call>(implementation);

const rebar: OpenPurchaseOrderLine = {
  lineId: 'line-rebar',
  orderId: 'order-3',
  orderNumber: 'PO-0003',
  lineNumber: 2,
  supplierName: 'Visayas Steel Trading',
  description: 'Rebar 16 mm',
  grossMinor: 500_000,
  remainingNetMinor: 300_000,
  remainingGrossMinor: 300_000,
};

async function open(options: { lines?: readonly OpenPurchaseOrderLine[]; fails?: boolean; line?: CostEntry; permissions?: readonly string[] } = {}) {
  const createCostEntry = stub(() => of(costEntryFixture()));
  const updateCostEntry = stub(() => of(costEntryFixture()));
  const openLines = stub(() => (options.fails ? throwError(() => new Error('down')) : of(options.lines ?? [rebar])));
  TestBed.configureTestingModule({
    providers: [
      provideRouter([]),
      provideHttpClient(),
      provideHttpClientTesting(),
      ProjectContext,
      { provide: ProjectsApi, useValue: {} },
      { provide: CostingApi, useValue: { reference: () => of(referenceFixture()), createCostEntry, updateCostEntry } },
      { provide: PurchasingApi, useValue: { openLines } },
    ],
  });
  await signInForTest(
    TestBed.inject(SessionState),
    TestBed.inject(HttpTestingController),
    options.permissions ? { permissions: options.permissions } : {},
  );
  TestBed.inject(ProjectContext).replace(projectFixture());
  const fixture = TestBed.createComponent(LineDialog);
  fixture.componentRef.setInput('kind', 'cost');
  fixture.componentRef.setInput('line', options.line ?? null);
  await settle(fixture);
  return { fixture, root: fixture.nativeElement as HTMLElement, createCostEntry, updateCostEntry, openLines };
}

const select = (root: HTMLElement) => root.querySelector<HTMLSelectElement>('[formControlName="purchaseOrderLineId"]');

describe('The cost dialog "Against a purchase order" choice', () => {
  it('lists the open lines of approved orders, each with what remains', async () => {
    const { root, openLines } = await open();

    expect(openLines).toHaveBeenCalledWith(PROJECT_ID);
    expect(texts(root, '[formControlName="purchaseOrderLineId"] option')).toEqual([
      'Not against an order',
      'PO-0003 line 2, Rebar 16 mm, ₱3,000.00 remaining',
    ]);
  });

  it('is not offered when the project has no approved order lines', async () => {
    const { root } = await open({ lines: [] });

    expect(select(root)).toBeNull();
  });

  it('is not offered, and not even asked for, to someone who may not read purchasing', async () => {
    const { root, openLines } = await open({ permissions: ['costs.read', 'costs.write', 'projects.read'] });

    expect(select(root)).toBeNull();
    expect(openLines).not.toHaveBeenCalled();
  });

  it('records the cost the usual way when the lines cannot be loaded', async () => {
    const { root } = await open({ fails: true });

    expect(select(root)).toBeNull();
    expect(root.querySelector('[formControlName="sourceDealer"]')).not.toBeNull();
  });

  it('fills in the supplier and the description and sends the chosen line with the cost', async () => {
    const { fixture, root, createCostEntry } = await open();

    const choice = select(root)!;
    choice.value = 'line-rebar';
    choice.dispatchEvent(new Event('change', { bubbles: true }));
    await settle(fixture);

    expect(root.querySelector<HTMLInputElement>('[formControlName="sourceDealer"]')!.value).toBe('Visayas Steel Trading');
    expect(root.querySelector<HTMLInputElement>('[formControlName="description"]')!.value).toBe('Rebar 16 mm');

    type(root, '[formControlName="invoiceNumber"]', 'VST-9001');
    root.querySelector<HTMLInputElement>('label.choice input[type="radio"]')!.click();
    type(root, 'app-number-input input', '1');
    type(root, '[formControlName="unit"]', 'lot');
    type(root, 'app-money-input input', '3,000.00');
    Array.from(root.querySelectorAll<HTMLElement>('label.choice')).find((item) => item.textContent?.includes('No VAT'))?.querySelector('input')?.click();
    buttonNamed(root, 'Record cost').click();
    await settle(fixture);

    expect(createCostEntry).toHaveBeenCalledTimes(1);
    expect(createCostEntry.mock.calls[0]![1]).toMatchObject({ sourceDealer: 'Visayas Steel Trading', description: 'Rebar 16 mm', purchaseOrderLineId: 'line-rebar' });
  });

  it('leaves the match out of a cost that is not against an order, and keeps one that is when its order has closed', async () => {
    const { fixture, root, updateCostEntry } = await open({
      lines: [],
      line: costEntryFixture({ purchaseOrderLineId: 'line-old', purchaseOrderNumber: 'PO-0001' }),
    });

    expect(texts(root, '[formControlName="purchaseOrderLineId"] option')).toEqual(['Not against an order', 'PO-0001 (kept as recorded)']);
    expect(select(root)!.value).toBe('line-old');

    buttonNamed(root, 'Save cost').click();
    await settle(fixture);

    expect(updateCostEntry.mock.calls[0]![1]).toMatchObject({ purchaseOrderLineId: 'line-old' });
  });
});
