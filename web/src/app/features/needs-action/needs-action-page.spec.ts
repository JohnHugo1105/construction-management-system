import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ChangeDetectionStrategy, Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { Observable, Subject, of, throwError } from 'rxjs';
import { ApiError } from '../../core/api/api-error';
import { PageTitleState } from '../../core/layout/page-title-state';
import { Permission } from '../../core/session/permissions';
import { SessionState } from '../../core/session/session-state';
import { buttonNamed, settle, text, texts } from '../../testing/dom';
import { signInForTest } from '../../testing/session-fixture';
import { NeedsActionApi } from './needs-action-api';
import { NeedsActionPage } from './needs-action-page';
import { NeedsActionState } from './needs-action-state';
import { actionGroup, actionRow, everyGroup, warehouseKeeperResult } from './needs-action.fixtures';
import { NeedsActionResult } from './needs-action.models';

@Component({ template: '', changeDetection: ChangeDetectionStrategy.OnPush })
class Blank {}

interface Options {
  readonly result?: NeedsActionResult;
  readonly answers?: (() => Observable<NeedsActionResult>)[];
  readonly permissions?: readonly string[];
  readonly roles?: readonly string[];
  /** False to look at the page before its data arrives (a pending read keeps the page from settling). */
  readonly settle?: boolean;
}

async function open(options: Options = {}) {
  const answers = [...(options.answers ?? [])];
  const get = vi.fn(() => answers.shift()?.() ?? of(options.result ?? everyGroup()));
  TestBed.configureTestingModule({
    providers: [
      provideRouter([
        { path: 'needs-action', component: NeedsActionPage },
        { path: '**', component: Blank },
      ]),
      provideHttpClient(),
      provideHttpClientTesting(),
      { provide: NeedsActionApi, useValue: { get, count: vi.fn(() => of({ total: 0 })) } },
    ],
  });
  await signInForTest(TestBed.inject(SessionState), TestBed.inject(HttpTestingController), {
    ...(options.permissions ? { permissions: options.permissions } : {}),
    ...(options.roles ? { roles: options.roles } : {}),
  });
  const harness = await RouterTestingHarness.create();
  await harness.navigateByUrl('/needs-action', NeedsActionPage);
  if (options.settle === false) harness.fixture.detectChanges();
  else await settle(harness.fixture);
  return { harness, root: harness.fixture.nativeElement as HTMLElement, get };
}

const cards = (root: HTMLElement) =>
  Array.from(root.querySelectorAll<HTMLElement>('section.group'));
const card = (root: HTMLElement, heading: string) =>
  cards(root).find((section) => section.querySelector('h2 span')?.textContent?.trim() === heading)!;
const openLink = (section: HTMLElement) => section.querySelector<HTMLAnchorElement>('.item a')!;

describe('NeedsActionPage', () => {
  it('shows one card per group in plain words with its count, in the order the server ranked them', async () => {
    const { root } = await open();

    expect(texts(root, 'section.group h2 span:first-child')).toEqual([
      'Overdue bills to chase',
      'Bonds that have expired or expire soon',
      'Purchase orders waiting for your approval',
      'Change orders waiting for your approval',
      'Bills ready to issue',
      'Subcontract claims to certify',
      'Payroll waiting for approval',
      'Deliveries still to come',
      'Progress not updated for 14 days',
      'Attendance not recorded yesterday',
      'Contribution and tax tables need setup',
    ]);
    expect(text(card(root, 'Purchase orders waiting for your approval'), '.group__count')).toBe(
      '12',
    );
    expect(TestBed.inject(PageTitleState).title()).toBe('Needs your action');
  });

  it('says what each row is, where, why it waits and how much, and leads to the record’s own page', async () => {
    const { root } = await open();

    const order = card(root, 'Purchase orders waiting for your approval').querySelector('.item')!;
    expect(order.textContent).toContain('PO-0012, Visayas Steel');
    expect(order.textContent).toContain('Riverside Residences');
    expect(order.textContent).toContain('Waiting 3 days');
    expect(text(order, '.item__amount')).toBe('₱1,250,000.00');
    expect(texts(order, 'a, button')).toEqual(['Open']);
    expect(order.querySelector('a')!.getAttribute('aria-label')).toBe(
      'Open PO-0012, Visayas Steel',
    );

    expect(text(card(root, 'Change orders waiting for your approval'), '.item__amount')).toBe(
      '-₱15,000.00',
    );

    const links = Object.fromEntries(
      cards(root).map((section) => [
        section.querySelector('h2 span')!.textContent!.trim(),
        openLink(section).getAttribute('href'),
      ]),
    );
    expect(links).toEqual({
      'Overdue bills to chase': '/billing/bills/bill-3',
      'Bonds that have expired or expire soon': '/bonds/bond-1',
      'Purchase orders waiting for your approval': '/purchasing/orders/po-12',
      'Change orders waiting for your approval': '/projects/project-1/changes',
      'Bills ready to issue': '/billing/bills/bill-4',
      'Subcontract claims to certify': '/subcontracts/subcontract-4',
      'Payroll waiting for approval': '/payroll/run-1',
      'Deliveries still to come': '/purchasing/orders/po-7',
      'Progress not updated for 14 days': '/projects/project-1/progress',
      'Attendance not recorded yesterday': '/projects/project-2/team',
      'Contribution and tax tables need setup': '/payroll/statutory',
    });
  });

  it('marks an urgent row with words and an icon, and makes the first urgent row’s Open the one primary action', async () => {
    const { root } = await open();

    const bill = card(root, 'Overdue bills to chase');
    expect(text(bill, '.chip')).toBe('Overdue');
    expect(bill.querySelector('.chip app-icon')).not.toBeNull();
    expect(text(card(root, 'Bonds that have expired or expire soon'), '.chip')).toBe('Expired');
    expect(
      card(root, 'Purchase orders waiting for your approval').querySelector('.chip'),
    ).toBeNull();

    const primary = root.querySelectorAll('a.btn--primary');
    expect(primary).toHaveLength(1);
    expect(primary[0]!.getAttribute('href')).toBe('/billing/bills/bill-3');
    // No approve or issue buttons here: the record's own page is where decisions are made.
    expect(texts(root, 'button')).toEqual([]);
  });

  it('links "See all" to a list with the same filter, and otherwise says how many there are in total', async () => {
    const { root } = await open();

    const orders = card(root, 'Purchase orders waiting for your approval');
    const seeAll = buttonNamed(orders, 'See all 12');
    expect(seeAll.getAttribute('href')).toBe('/purchasing?status=Draft');

    const deliveries = card(root, 'Deliveries still to come');
    expect(buttonNamed(deliveries, 'See all 11').getAttribute('href')).toBe('/receiving');
    expect(card(root, 'Bills ready to issue').querySelector('.group__more')).toBeNull();
  });

  it('says "N in total" when no list shows the same records', async () => {
    const { root } = await open({
      permissions: [Permission.projectsRead, Permission.purchasingReceive],
      result: {
        groups: [actionGroup('deliveries-expected', [actionRow({ amountMinor: undefined })], 9)],
      },
    });

    const deliveries = card(root, 'Deliveries still to come');
    expect(text(deliveries, '.group__more')).toBe('9 in total');
    expect(deliveries.querySelector('.group__more a')).toBeNull();
    // Without purchasing.read the order opens on the receiving page.
    expect(openLink(deliveries).getAttribute('href')).toBe('/receiving/record-1');
  });

  it('shows a Warehouse Keeper their deliveries on the receiving page and no amount anywhere', async () => {
    const { root } = await open({
      roles: ['WarehouseKeeper'],
      permissions: [
        Permission.projectsRead,
        Permission.inventoryRead,
        Permission.receivingRecord,
        Permission.actionsRead,
      ],
      result: warehouseKeeperResult(),
    });

    expect(cards(root)).toHaveLength(1);
    expect(texts(root, '.item a')).toEqual(['Open', 'Open']);
    expect(
      Array.from(root.querySelectorAll('.item a')).map((link) => link.getAttribute('href')),
    ).toEqual(['/receiving/po-7', '/receiving/po-8']);
    expect(root.querySelectorAll('.item__amount')).toHaveLength(0);
    expect(root.textContent).not.toMatch(/₱|PHP|\d+\.\d{2}/);
    expect(text(root, '.chip')).toBe('Overdue');
  });

  it('leaves out groups with nothing waiting, and says so when nothing waits at all', async () => {
    const { root } = await open({
      result: {
        groups: [actionGroup('purchase-orders-to-approve', []), actionGroup('bonds-to-attend', [])],
      },
    });

    expect(cards(root)).toHaveLength(0);
    expect(text(root, '.empty__title')).toBe('Nothing needs your action right now.');
    expect(root.textContent).toContain('This page lists what is waiting on you');
  });

  it('sets the navigation badge from what the page read', async () => {
    await open();

    // 12 + 11 + one in each of the other nine groups.
    expect(TestBed.inject(NeedsActionState).count()).toBe(32);
  });

  it('shows a loading placeholder first, then says what went wrong and lets the person try again', async () => {
    const failure = new ApiError({
      status: 503,
      title: 'Service unavailable',
      detail: 'Try again in a moment.',
    });
    const { harness, root, get } = await open({
      answers: [() => throwError(() => failure)],
      result: {
        groups: [
          actionGroup('bills-to-issue', [actionRow({ label: 'Progress bill for ACME Realty' })]),
        ],
      },
    });

    expect(root.querySelector('[role="alert"], app-api-error-alert')?.textContent).toContain(
      'Try again in a moment.',
    );
    expect(cards(root)).toHaveLength(0);
    expect(root.querySelector('app-empty-state')).toBeNull();

    buttonNamed(root, 'Try again').click();
    await settle(harness.fixture);

    expect(get).toHaveBeenCalledTimes(2);
    expect(text(card(root, 'Bills ready to issue'), '.item__label')).toBe(
      'Progress bill for ACME Realty',
    );
  });

  it('shows a skeleton while the queue loads', async () => {
    const pending = new Subject<NeedsActionResult>();
    const { harness, root } = await open({ answers: [() => pending], settle: false });

    expect(root.querySelector('app-skeleton')).not.toBeNull();
    expect(cards(root)).toHaveLength(0);

    pending.next(everyGroup());
    pending.complete();
    await settle(harness.fixture);
    expect(root.querySelector('app-skeleton')).toBeNull();
    expect(cards(root)).toHaveLength(11);
  });
});
