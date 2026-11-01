import { PurchaseOrder, PurchaseOrderLine, PurchaseOrderListItem, Supplier } from './purchasing.models';

export const RIVERSIDE_ID = '11111111-1111-1111-1111-111111111111';
export const FIRM_ID = '22222222-2222-2222-2222-222222222222';

export function orderLine(overrides: Partial<PurchaseOrderLine> = {}): PurchaseOrderLine {
  return {
    id: 'line-1',
    lineNumber: 1,
    division: 3,
    mainScope: 'Concrete',
    subScope: 'Concrete Reinforcement',
    wbsCode: '3200',
    description: 'Rebar 16 mm',
    itemType: 'Material',
    quantity: 10,
    unit: 'pc',
    unitCostMinor: 50_000,
    vatTreatment: 'Exempt',
    vatRateBps: 1200,
    netMinor: 500_000,
    vatMinor: 0,
    grossMinor: 500_000,
    invoicedNetMinor: 0,
    invoicedGrossMinor: 0,
    remainingNetMinor: 500_000,
    remainingGrossMinor: 500_000,
    overByGrossMinor: 0,
    ...overrides,
  };
}

/** A draft of one line, 5,000.00 in all, for a project that is open. */
export function orderFixture(overrides: Partial<PurchaseOrder> = {}): PurchaseOrder {
  return {
    id: 'order-1',
    number: 'PO-0001',
    status: 'Draft',
    supplierId: FIRM_ID,
    supplierName: 'Visayas Steel Trading',
    projectId: RIVERSIDE_ID,
    projectName: 'Riverside Residences',
    projectArchived: false,
    subProjectId: null,
    subProjectName: null,
    orderDate: '2026-10-20',
    expectedDate: '2026-11-10',
    supplierReference: 'Quote 88',
    notes: null,
    netMinor: 500_000,
    vatMinor: 0,
    grossMinor: 500_000,
    invoicedNetMinor: 0,
    invoicedGrossMinor: 0,
    committedNetMinor: 0,
    committedGrossMinor: 0,
    lines: [orderLine()],
    invoices: [],
    invoiceCount: 0,
    createdByName: 'Carla Cost-Engineer',
    createdAtUtc: '2026-10-20T01:00:00Z',
    approvedByName: null,
    approvedAtUtc: null,
    closedByName: null,
    closedAtUtc: null,
    closeReason: null,
    cancelledByName: null,
    cancelledAtUtc: null,
    cancelReason: null,
    canEdit: true,
    canApprove: true,
    canCancel: true,
    canClose: false,
    version: 0,
    ...overrides,
  };
}

export function approvedOrder(overrides: Partial<PurchaseOrder> = {}): PurchaseOrder {
  return orderFixture({
    status: 'Approved',
    approvedByName: 'Alma Accountant',
    approvedAtUtc: '2026-10-21T01:00:00Z',
    committedNetMinor: 500_000,
    committedGrossMinor: 500_000,
    canEdit: false,
    canApprove: false,
    canClose: true,
    version: 1,
    ...overrides,
  });
}

export function listItem(overrides: Partial<PurchaseOrderListItem> = {}): PurchaseOrderListItem {
  return {
    id: 'order-1',
    number: 'PO-0001',
    supplierId: FIRM_ID,
    supplierName: 'Visayas Steel Trading',
    projectId: RIVERSIDE_ID,
    projectName: 'Riverside Residences',
    orderDate: '2026-10-20',
    expectedDate: null,
    status: 'Approved',
    lineCount: 2,
    netMinor: 500_000,
    vatMinor: 0,
    grossMinor: 500_000,
    ...overrides,
  };
}

export function supplierFixture(overrides: Partial<Supplier> = {}): Supplier {
  return {
    id: FIRM_ID,
    name: 'Visayas Steel Trading',
    isSupplier: true,
    isSubcontractor: false,
    tin: null,
    contactPerson: 'Mara Co',
    phone: '0917 555 0104',
    email: null,
    address: null,
    notes: null,
    isArchived: false,
    archivedAtUtc: null,
    version: 0,
    ...overrides,
  };
}
