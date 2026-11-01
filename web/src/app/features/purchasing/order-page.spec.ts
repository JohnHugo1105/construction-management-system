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
import { buttonNamed, settle, text, texts } from '../../testing/dom';
import { signInForTest } from '../../testing/session-fixture';
import { OrderPage } from './order-page';
import { PurchasingApi } from './purchasing-api';
import { approvedOrder, orderFixture, orderLine } from './purchasing.fixtures';
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
    const { root } = await open(approvedOrder({ canCancel: false, invoiceCount: 1 }));

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
});
