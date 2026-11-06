import {
  ReceivingDelivery,
  ReceivingLine,
  ReceivingOrder,
  ReceivingOrderListItem,
} from './receiving.models';

export function receivingLine(overrides: Partial<ReceivingLine> = {}): ReceivingLine {
  return {
    id: 'line-1',
    lineNumber: 1,
    description: 'Rebar 16 mm',
    unit: 'pc',
    orderedQuantity: 10,
    receivedQuantity: 0,
    remainingQuantity: 10,
    ...overrides,
  };
}

/** A delivery of 4 pc of the first line, recorded by the warehouse and still standing. */
export function deliveryFixture(overrides: Partial<ReceivingDelivery> = {}): ReceivingDelivery {
  return {
    id: 'delivery-1',
    receivedDate: '2026-10-28',
    deliveryReceiptNumber: 'DR-0012',
    note: null,
    status: 'Posted',
    recordedByName: 'Wally Warehouse',
    recordedAtUtc: '2026-10-28T03:00:00Z',
    voidedByName: null,
    voidedAtUtc: null,
    canVoid: true,
    lines: [
      {
        id: 'dl-1',
        orderLineId: 'line-1',
        lineNumber: 1,
        description: 'Rebar 16 mm',
        unit: 'pc',
        quantity: 4,
      },
    ],
    version: 1,
    ...overrides,
  };
}

/** An approved order of Riverside Residences with nothing delivered yet: rebar (10 pc) and tie wire (3 kg). */
export function receivingOrder(overrides: Partial<ReceivingOrder> = {}): ReceivingOrder {
  return {
    id: 'order-1',
    number: 'PO-0001',
    status: 'Approved',
    supplierName: 'Visayas Steel Trading',
    projectName: 'Riverside Residences',
    subProjectName: null,
    expectedDate: '2026-11-05',
    supplierReference: 'QT-884',
    deliveryState: 'NotReceived',
    canRecord: true,
    lines: [
      receivingLine(),
      receivingLine({
        id: 'line-2',
        lineNumber: 2,
        description: 'Tie wire',
        unit: 'kg',
        orderedQuantity: 3,
        remainingQuantity: 3,
      }),
    ],
    deliveries: [],
    deliveryCount: 0,
    version: 3,
    ...overrides,
  };
}

/** The order after 4 of the 10 pc of rebar arrived. */
export function partlyDelivered(overrides: Partial<ReceivingOrder> = {}): ReceivingOrder {
  return receivingOrder({
    deliveryState: 'PartlyReceived',
    lines: [
      receivingLine({ receivedQuantity: 4, remainingQuantity: 6 }),
      receivingLine({
        id: 'line-2',
        lineNumber: 2,
        description: 'Tie wire',
        unit: 'kg',
        orderedQuantity: 3,
        remainingQuantity: 3,
      }),
    ],
    deliveries: [deliveryFixture()],
    deliveryCount: 1,
    version: 4,
    ...overrides,
  });
}

export function listItem(overrides: Partial<ReceivingOrderListItem> = {}): ReceivingOrderListItem {
  return {
    id: 'order-1',
    number: 'PO-0001',
    supplierName: 'Visayas Steel Trading',
    projectName: 'Riverside Residences',
    subProjectName: null,
    expectedDate: '2026-11-05',
    deliveryState: 'NotReceived',
    version: 3,
    ...overrides,
  };
}
