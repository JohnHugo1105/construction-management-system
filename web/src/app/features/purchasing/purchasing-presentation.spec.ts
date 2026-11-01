import { ORDER_STATUS, invoicedSummary, kindLabel, openLineLabel, presentStatus } from './purchasing-presentation';
import { OpenPurchaseOrderLine } from './purchasing.models';

const line: OpenPurchaseOrderLine = {
  lineId: 'line-1',
  orderId: 'order-1',
  orderNumber: 'PO-0003',
  lineNumber: 2,
  supplierName: 'Visayas Steel Trading',
  description: 'Rebar 16 mm',
  grossMinor: 500_000,
  remainingNetMinor: 300_000,
  remainingGrossMinor: 300_000,
};

describe('purchasing presentation', () => {
  it('names an order line in the choice list with what remains on it', () => {
    expect(openLineLabel(line, 'PHP')).toBe('PO-0003 line 2, Rebar 16 mm, ₱3,000.00 remaining');
    expect(openLineLabel({ ...line, remainingGrossMinor: 0 }, 'PHP')).toBe(
      'PO-0003 line 2, Rebar 16 mm, fully invoiced',
    );
  });

  it('says in plain words where a line stands against its invoices', () => {
    expect(invoicedSummary({ invoicedGrossMinor: 0, remainingGrossMinor: 500_000, overByGrossMinor: 0 }, 'PHP')).toBe('Not invoiced yet');
    expect(invoicedSummary({ invoicedGrossMinor: 200_000, remainingGrossMinor: 300_000, overByGrossMinor: 0 }, 'PHP')).toBe('₱3,000.00 remaining');
    expect(invoicedSummary({ invoicedGrossMinor: 500_000, remainingGrossMinor: 0, overByGrossMinor: 0 }, 'PHP')).toBe('Fully invoiced');
    expect(invoicedSummary({ invoicedGrossMinor: 600_000, remainingGrossMinor: 0, overByGrossMinor: 100_000 }, 'PHP')).toBe('Over by ₱1,000.00');
  });

  it('says a closed order released what was not invoiced instead of calling it remaining', () => {
    expect(invoicedSummary({ invoicedGrossMinor: 0, remainingGrossMinor: 500_000, overByGrossMinor: 0 }, 'PHP', true)).toBe('₱5,000.00 released');
    expect(invoicedSummary({ invoicedGrossMinor: 500_000, remainingGrossMinor: 0, overByGrossMinor: 0 }, 'PHP', true)).toBe('Fully invoiced');
  });

  it('gives every status words, an icon and a meaning, and survives one it does not know', () => {
    expect(Object.keys(ORDER_STATUS)).toEqual(['Draft', 'Approved', 'Closed', 'Cancelled']);
    expect(presentStatus('Approved').label).toBe('Approved');
    expect(presentStatus('Paused').label).toBe('Paused');
  });

  it('describes a firm by what it is to the company', () => {
    expect(kindLabel({ isSupplier: true, isSubcontractor: false })).toBe('Supplier');
    expect(kindLabel({ isSupplier: false, isSubcontractor: true })).toBe('Subcontractor');
    expect(kindLabel({ isSupplier: true, isSubcontractor: true })).toBe('Supplier and subcontractor');
  });
});
