import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ChangeDetectionStrategy, Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { Observable, of } from 'rxjs';
import type { Mock } from 'vitest';
import { Permission } from '../../core/session/permissions';
import { SessionState } from '../../core/session/session-state';
import { buttonNamed, settle, text, texts, type } from '../../testing/dom';
import { signInForTest } from '../../testing/session-fixture';
import { OrderPage } from './order-page';
import { PurchasingApi } from './purchasing-api';
import { approvedOrder, orderFixture, orderLine, receiptFixture } from './purchasing.fixtures';
import { PurchaseOrder } from './purchasing.models';

@Component({ template: '', changeDetection: ChangeDetectionStrategy.OnPush })
class Blank {}

type Call = (...args: unknown[]) => Observable<unknown>;
const stub = (implementation: Call): Mock<Call> => vi.fn<Call>(implementation);

async function open(order: PurchaseOrder, permissions: readonly string[] | null = null, api: Record<string, Mock<Call>> = {}) {
  const getOrder = stub(() => of(order));
  TestBed.configureTestingModule({
    providers: [
      provideRouter([{ path: 'purchasing/orders/:id', component: OrderPage }, { path: 'purchasing', component: Blank }, { path: 'projects/:id', component: Blank }, { path: 'purchasing/orders/:id/edit', component: Blank }], withComponentInputBinding()),
      provideHttpClient(),
      provideHttpClientTesting(),
      { provide: PurchasingApi, useValue: { getOrder, ...api } },
    ],
  });
  await signInForTest(TestBed.inject(SessionState), TestBed.inject(HttpTestingController), permissions ? { permissions } : {});
  const harness = await RouterTestingHarness.create();
  await harness.navigateByUrl(`/purchasing/orders/${order.id}`, OrderPage);
  await settle(harness.fixture);
  return { harness, root: harness.fixture.nativeElement as HTMLElement, getOrder };
}

const READER = [Permission.purchasingRead, Permission.projectsRead, Permission.costsRead];

describe('OrderPage', () => {
  it('leads a draft with Approve for someone who may approve, with edit and cancel quieter', async () => {
    const { root } = await open(orderFixture());

    expect(text(root, '.summary__actions .btn--primary')).toBe('Approve purchase order');
    expect(texts(root, '.summary__actions .btn').sort()).toEqual(['Approve purchase order', 'Cancel order', 'Edit draft']);
    expect(text(root, '.summary__info')).toContain('Draft');
    expect(text(root, '.summary__info')).toContain('nothing is committed');
  });

  it('shows no step at all to someone who may only read orders', async () => {
    const { root } = await open(orderFixture(), READER);

    expect(root.querySelector('.summary__actions')).toBeNull();
    expect(root.textContent).toContain('Order details');
  });

  it('shows the cost engineer who drafts but does not approve an edit link and no Approve', async () => {
    const { root } = await open(orderFixture(), [...READER, Permission.purchasingManage]);

    expect(texts(root, '.summary__actions .btn')).toEqual(['Edit draft']);
  });

  it('shows what the server says is allowed: an approved order can be closed, and one with invoices cannot be cancelled', async () => {
    const { root } = await open(approvedOrder({ canCancel: false, invoiceCount: 1, canReceive: false }));

    expect(texts(root, '.summary__actions .btn')).toEqual(['Close order']);
  });

  it('shows the lines with what has been invoiced against each, and an overrun in plain words', async () => {
    const { root } = await open(
      approvedOrder({
        invoiceCount: 2,
        invoicedGrossMinor: 600_000,
        committedGrossMinor: 0,
        lines: [orderLine({ invoicedGrossMinor: 600_000, remainingGrossMinor: 0, overByGrossMinor: 100_000 })],
        invoices: [
          { costEntryId: 'c1', purchaseOrderLineId: 'line-1', lineNumber: 1, entryDate: '2026-10-25', sourceDealer: 'Visayas Steel Trading', invoiceNumber: 'VST-1', description: 'Rebar', netMinor: 600_000, grossMinor: 600_000 },
        ],
      }),
    );

    expect(text(root, '.line__text')).toContain('10 pc at ₱500.00');
    expect(text(root, '.line__over')).toBe('Over by ₱1,000.00');
    expect(text(root, '.totals')).toContain('Invoiced so far₱6,000.00');
    expect(text(root, '.totals')).toContain('Still committed₱0.00');
    expect(text(root, '#order-invoices')).toContain('Invoices against this order');
  });

  it('asks before approving, then approves with the version it opened', async () => {
    const approveOrder = stub(() => of(approvedOrder({ version: 1 })));
    const { harness, root } = await open(orderFixture({ version: 4 }), null, { approveOrder });

    buttonNamed(root, 'Approve purchase order').click();
    await settle(harness.fixture);
    expect(document.body.textContent).toContain('Approve this purchase order?');
    const confirm = Array.from(document.querySelectorAll<HTMLElement>('app-confirm-dialog button')).find((button) => button.textContent?.trim() === 'Approve purchase order')!;
    confirm.click();
    await settle(harness.fixture);

    expect(approveOrder).toHaveBeenCalledWith('order-1', 4);
    expect(text(root, '.summary__info')).toContain('Approved');
  });

  it('says why nothing can change on an archived project', async () => {
    const { root } = await open(orderFixture({ projectArchived: true, canEdit: false, canApprove: false, canCancel: false }));

    expect(text(root, 'app-alert')).toContain('Restore the project');
    expect(root.querySelector('.summary__actions')).toBeNull();
  });

  it('shows the reason a cancelled order was called off', async () => {
    const { root } = await open(orderFixture({ status: 'Cancelled', cancelReason: 'Re-tender', cancelledByName: 'Ana', cancelledAtUtc: '2026-10-22T01:00:00Z', canEdit: false, canApprove: false, canCancel: false }));

    expect(text(root, '.reason')).toBe('Reason: Re-tender');
    expect(text(root, '.history')).toContain('Cancelled by Ana');
  });

  describe('deliveries', () => {
    const PARTLY = approvedOrder({
      deliveryState: 'PartlyReceived',
      receiptCount: 2,
      receipts: [
        receiptFixture({ id: 'receipt-2', receivedDate: '2026-10-30', deliveryReceiptNumber: null, note: 'Gate 2', version: 3 }),
        receiptFixture({
          id: 'receipt-1',
          status: 'Void',
          canVoid: false,
          voidedByName: 'Alma Accountant',
          voidedAtUtc: '2026-10-29T03:00:00Z',
          voidReason: 'Counted the wrong truck',
        }),
      ],
      lines: [orderLine({ receivedQuantity: 4, toReceiveQuantity: 6, invoicedGrossMinor: 200_000, remainingGrossMinor: 300_000 })],
    });

    it('leads an approved order that still has something to receive with Record delivery, quieter steps after it', async () => {
      const { root } = await open(approvedOrder());

      expect(text(root, '.summary__actions .btn--primary')).toBe('Record delivery');
      expect(texts(root, '.summary__actions .btn--primary')).toHaveLength(1);
      expect(texts(root, '.summary__actions .btn')).toEqual(['Record delivery', 'Close order', 'Cancel order']);
    });

    it('offers no delivery to someone who may not receive, and none when the server says nothing is left or the project is archived', async () => {
      const reader = await open(approvedOrder(), [...READER, Permission.purchasingApprove]);
      expect(texts(reader.root, '.summary__actions .btn')).toEqual(['Close order', 'Cancel order']);
      TestBed.resetTestingModule();

      const complete = await open(approvedOrder({ canReceive: false }));
      expect(texts(complete.root, '.summary__actions .btn')).toEqual(['Close order', 'Cancel order']);
      TestBed.resetTestingModule();

      const archived = await open(approvedOrder({ canReceive: false, projectArchived: true, canClose: false, canCancel: false }));
      expect(archived.root.querySelector('.summary__actions')).toBeNull();
    });

    it('shows nothing about deliveries until one has been recorded', async () => {
      const { root } = await open(approvedOrder());

      expect(root.querySelector('[aria-labelledby="order-deliveries"]')).toBeNull();
      expect(root.querySelector('.line__flow')).toBeNull();
      expect(text(root, '.summary__info')).not.toContain('delivered');
    });

    it('lists the deliveries, newest first, a voided one muted with who voided it and why', async () => {
      const { root } = await open(PARTLY);

      expect(text(root, '.summary__info')).toContain('Partly delivered');
      const rows = root.querySelectorAll('[aria-labelledby="order-deliveries"] .line');
      expect(rows).toHaveLength(2);
      expect(text(rows[0]!, '.line__description')).toBe('30 Oct 2026');
      expect(text(rows[0]!, '.line__text')).toContain('4 pc Rebar 16 mm');
      expect(text(rows[0]!, '.line__text')).toContain('Gate 2');
      expect(text(rows[0]!, '.line__text')).toContain('Recorded by Wally Warehouse');
      expect(text(rows[1]!, '.line__description')).toBe('28 Oct 2026 · DR-0012 Voided');
      expect(text(rows[1]!, '.line__text')).toContain('Voided by Alma Accountant');
      expect(text(rows[1]!, '.line__text')).toContain('Counted the wrong truck');
      // Only a delivery that still stands can be voided.
      expect(texts(rows[0]!, 'button')).toEqual(['Void delivery']);
      expect(rows[1]!.querySelector('button')).toBeNull();
    });

    it('shows ordered, received and invoiced on the line once something has arrived', async () => {
      const { root } = await open(PARTLY);

      expect(text(root, '.line__flow')).toBe('Ordered 10 pc · Received 4 pc · Invoiced ₱2,000.00');
      expect(root.querySelector('.line__ahead')).toBeNull();
    });

    it('says an invoice ran ahead of delivery only on the line it applies to, and the invoice stands', async () => {
      const { root } = await open(
        approvedOrder({
          lines: [
            orderLine({ invoicedGrossMinor: 300_000, remainingGrossMinor: 200_000, invoicedAheadOfDelivery: true, receivedValueGrossMinor: 0 }),
            orderLine({ id: 'line-2', lineNumber: 2, description: 'Tie wire' }),
          ],
        }),
      );

      const notes = texts(root, '.line__ahead');
      expect(notes).toEqual(['Invoiced ahead of delivery: ₱3,000.00 invoiced, ₱0.00 delivered so far. The invoice stands.']);
    });

    it('voids a delivery with a reason and the version it opened, then shows the order again', async () => {
      const voidDelivery = stub(() => of(approvedOrder()));
      const { harness, root, getOrder } = await open(PARTLY, null, { voidDelivery });

      buttonNamed(root, 'Void delivery').click();
      await settle(harness.fixture);
      expect(document.body.textContent).toContain('Void this delivery?');
      const dialog = document.querySelector('app-reason-dialog')!;
      type(dialog, 'textarea', 'Counted the wrong truck');
      (dialog.querySelector('button[type="submit"]') as HTMLButtonElement).click();
      await settle(harness.fixture);

      expect(voidDelivery).toHaveBeenCalledWith('order-1', 'receipt-2', 'Counted the wrong truck', 3);
      expect(getOrder).toHaveBeenCalledTimes(2);
    });

    it('records a delivery from the dialog and shows the order the server sends back, without asking for it again', async () => {
      const recordDelivery = stub(() => of({ ...PARTLY, version: 9 }));
      const { harness, root, getOrder } = await open(approvedOrder(), null, { recordDelivery });

      buttonNamed(root, 'Record delivery').click();
      await settle(harness.fixture);
      expect(document.body.textContent).toContain('Record a delivery');
      const submit = Array.from(document.querySelectorAll<HTMLButtonElement>('app-record-delivery-dialog button[type="submit"]'))[0]!;
      submit.click();
      await settle(harness.fixture);

      expect(recordDelivery).toHaveBeenCalledWith(
        'order-1',
        expect.objectContaining({ lines: [{ purchaseOrderLineId: 'line-1', quantity: 10 }] }),
        expect.any(String),
      );
      expect(getOrder).toHaveBeenCalledTimes(1);
      expect(root.querySelector('[aria-labelledby="order-deliveries"]')).not.toBeNull();
      expect(document.querySelector('app-record-delivery-dialog')).toBeNull();
    });
  });
});
