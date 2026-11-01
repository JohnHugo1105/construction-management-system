import {
  DELIVERY_STATE,
  ORDER_STATUS,
  aheadOfDeliveryNote,
  formatQuantity,
  invoicedSummary,
  kindLabel,
  lineFlow,
  listDeliveryChip,
  openLineLabel,
  presentDelivery,
  presentStatus,
} from './purchasing-presentation';
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
    expect(
      invoicedSummary(
        { invoicedGrossMinor: 0, remainingGrossMinor: 500_000, overByGrossMinor: 0 },
        'PHP',
      ),
    ).toBe('Not invoiced yet');
    expect(
      invoicedSummary(
        { invoicedGrossMinor: 200_000, remainingGrossMinor: 300_000, overByGrossMinor: 0 },
        'PHP',
      ),
    ).toBe('₱3,000.00 remaining');
    expect(
      invoicedSummary(
        { invoicedGrossMinor: 500_000, remainingGrossMinor: 0, overByGrossMinor: 0 },
        'PHP',
      ),
    ).toBe('Fully invoiced');
    expect(
      invoicedSummary(
        { invoicedGrossMinor: 600_000, remainingGrossMinor: 0, overByGrossMinor: 100_000 },
        'PHP',
      ),
    ).toBe('Over by ₱1,000.00');
  });

  it('says a closed order released what was not invoiced instead of calling it remaining', () => {
    expect(
      invoicedSummary(
        { invoicedGrossMinor: 0, remainingGrossMinor: 500_000, overByGrossMinor: 0 },
        'PHP',
        true,
      ),
    ).toBe('₱5,000.00 released');
    expect(
      invoicedSummary(
        { invoicedGrossMinor: 500_000, remainingGrossMinor: 0, overByGrossMinor: 0 },
        'PHP',
        true,
      ),
    ).toBe('Fully invoiced');
  });

  it('gives every status words, an icon and a meaning, and survives one it does not know', () => {
    expect(Object.keys(ORDER_STATUS)).toEqual(['Draft', 'Approved', 'Closed', 'Cancelled']);
    expect(presentStatus('Approved').label).toBe('Approved');
    expect(presentStatus('Paused').label).toBe('Paused');
  });

  it('describes a firm by what it is to the company', () => {
    expect(kindLabel({ isSupplier: true, isSubcontractor: false })).toBe('Supplier');
    expect(kindLabel({ isSupplier: false, isSubcontractor: true })).toBe('Subcontractor');
    expect(kindLabel({ isSupplier: true, isSubcontractor: true })).toBe(
      'Supplier and subcontractor',
    );
  });

  it('words each delivery state, and survives one it does not know', () => {
    expect(Object.keys(DELIVERY_STATE)).toEqual(['NotReceived', 'PartlyReceived', 'FullyReceived']);
    expect(presentDelivery('PartlyReceived').label).toBe('Partly delivered');
    expect(presentDelivery('FullyReceived')).toMatchObject({
      label: 'Delivered in full',
      tone: 'success',
    });
    expect(presentDelivery('Lost').label).toBe('Lost');
  });

  it('shows a delivery chip on the list only for an approved order that has had something arrive or says when it is expected', () => {
    const base = { status: 'Approved', deliveryState: 'NotReceived', expectedDate: null } as const;

    expect(listDeliveryChip(base)).toBeNull();
    expect(listDeliveryChip({ ...base, expectedDate: '2026-11-10' })?.label).toBe(
      'Expected 10 Nov 2026',
    );
    expect(listDeliveryChip({ ...base, deliveryState: 'PartlyReceived' })?.label).toBe(
      'Partly delivered',
    );
    expect(
      listDeliveryChip({ ...base, deliveryState: 'FullyReceived', expectedDate: '2026-11-10' })
        ?.label,
    ).toBe('Delivered in full');
    // A draft, a closed order and one the server gave no state for show nothing, whatever the dates say.
    expect(listDeliveryChip({ ...base, status: 'Draft', expectedDate: '2026-11-10' })).toBeNull();
    expect(
      listDeliveryChip({ ...base, status: 'Closed', deliveryState: 'PartlyReceived' }),
    ).toBeNull();
    expect(
      listDeliveryChip({ status: 'Approved', deliveryState: null, expectedDate: '2026-11-10' }),
    ).toBeNull();
  });

  it('writes quantities the way a person says them', () => {
    expect(formatQuantity(10)).toBe('10');
    expect(formatQuantity(2.5)).toBe('2.5');
    expect(formatQuantity(0.0001)).toBe('0.0001');
    expect(formatQuantity(1250)).toBe('1,250');
  });

  it('puts ordered, received and invoiced side by side for one line', () => {
    expect(
      lineFlow(
        { quantity: 10, unit: 'pc', receivedQuantity: 4, invoicedGrossMinor: 200_000 },
        'PHP',
      ),
    ).toBe('Ordered 10 pc · Received 4 pc · Invoiced ₱2,000.00');
    expect(
      lineFlow({ quantity: 3, unit: 'kg', receivedQuantity: 1.5, invoicedGrossMinor: 0 }, 'PHP'),
    ).toBe('Ordered 3 kg · Received 1.5 kg · Invoiced ₱0.00');
  });

  it('warns about an invoice ahead of delivery in words, and only when the server says so', () => {
    const line = {
      invoicedAheadOfDelivery: true,
      invoicedGrossMinor: 300_000,
      receivedValueGrossMinor: 200_000,
    };

    expect(aheadOfDeliveryNote(line, 'PHP')).toBe(
      'Invoiced ahead of delivery: ₱3,000.00 invoiced, ₱2,000.00 delivered so far. The invoice stands.',
    );
    expect(aheadOfDeliveryNote({ ...line, invoicedAheadOfDelivery: false }, 'PHP')).toBeNull();
  });
});
