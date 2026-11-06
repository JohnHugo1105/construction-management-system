import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ChangeDetectionStrategy, Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { of } from 'rxjs';
import { SessionState } from '../../core/session/session-state';
import { Viewport } from '../../shared/ui/viewport/viewport';
import { buttonNamed, settle, text, texts } from '../../testing/dom';
import { pageOf, signInForTest } from '../../testing/session-fixture';
import { BondsApi } from './bonds-api';
import { BondsPage } from './bonds-page';
import { MANAGER, READER, listItem, totals } from './bonds.fixtures';
import { BondListItem, BondListParams, BondTotals } from './bonds.models';

@Component({ template: '', changeDetection: ChangeDetectionStrategy.OnPush })
class Blank {}

interface Options {
  items?: readonly BondListItem[];
  permissions?: readonly string[];
  tablet?: boolean;
  url?: string;
  totals?: BondTotals;
}

async function open(options: Options = {}) {
  const list = vi.fn((params: BondListParams) => {
    void params;
    return of(pageOf(options.items ?? [listItem(), expiring]));
  });
  const totalsCall = vi.fn(() => of(options.totals ?? totals()));
  const projects = vi.fn(() =>
    of([
      { id: 'p1', name: 'Riverside Residences', status: 'Active' },
      { id: 'p2', name: 'Harbor Warehouse', status: 'Archived' },
    ]),
  );
  TestBed.configureTestingModule({
    providers: [
      provideRouter([
        { path: 'bonds', component: BondsPage },
        { path: 'bonds/:id', component: Blank },
      ]),
      provideHttpClient(),
      provideHttpClientTesting(),
      { provide: BondsApi, useValue: { list, totals: totalsCall, projects } },
      {
        provide: Viewport,
        useValue: { isTablet: signal(options.tablet ?? true), isDesktop: signal(true) },
      },
    ],
  });
  await signInForTest(TestBed.inject(SessionState), TestBed.inject(HttpTestingController), {
    roles: ['Custom'],
    permissions: options.permissions ?? MANAGER,
  });
  const harness = await RouterTestingHarness.create();
  await harness.navigateByUrl(options.url ?? '/bonds', BondsPage);
  await settle(harness.fixture);
  return { harness, root: harness.fixture.nativeElement as HTMLElement, list, projects };
}

const expiring = listItem({
  id: 'b2',
  direction: 'Received',
  kind: 'Warranty',
  issuerName: 'Capital Bank',
  bondNumber: 'WB-9',
  otherPartyName: 'Dela Cruz Electrical',
  amountMinor: 30_000_000,
  validUntil: '2026-11-12',
  expiry: 'ExpiringSoon',
  daysLeft: 10,
});

describe('BondsPage', () => {
  it('lists each bond with its kind, who gave it, the amount, the date and where it stands', async () => {
    const { root } = await open();

    const rows = root.querySelectorAll('tbody tr');
    expect(rows).toHaveLength(2);
    expect(text(rows[0]!, 'th a')).toBe('Performance bond PB-2026-014 · Metro Surety');
    expect(texts(rows[0]!, 'td')).toEqual(['₱5,000,000.00', '10 Feb 2027', 'Valid']);
    expect(text(rows[0]!, '.cell-sub')).toBe('Posted to the client · Riverside Residences');
    expect(texts(rows[1]!, 'td')).toEqual(['₱300,000.00', '12 Nov 2026', 'Expires in 10 days']);
    expect(text(rows[1]!, '.cell-sub')).toBe(
      'Received from Dela Cruz Electrical · Riverside Residences',
    );
    expect(rows[0]!.querySelector('a')!.getAttribute('href')).toBe('/bonds/b1');
  });

  it('shows cards on a phone', async () => {
    const { root } = await open({ tablet: false });

    expect(root.querySelector('table')).toBeNull();
    expect(root.querySelectorAll('.row-card')).toHaveLength(2);
    expect(text(root.querySelector('.row-card')!, '.row-card__title')).toBe(
      'Performance bond PB-2026-014 · Metro Surety',
    );
  });

  it('opens on the active bonds and asks for the first page', async () => {
    const { list } = await open();

    expect(list.mock.calls[0]![0]).toMatchObject({
      projectId: '',
      direction: '',
      kind: '',
      status: 'Active',
      expiry: '',
      page: 1,
      pageSize: 25,
    });
  });

  it('says what is posted and held in one line, leaving out a side that has nothing', async () => {
    const { root } = await open();
    expect(text(root, '.summary')).toBe(
      'Posted by us: ₱6,250,000.50 in 2 bonds. Received from firms: ₱300,000.00 in 1 bond.',
    );

    TestBed.resetTestingModule();
    const onlyReceived = await open({
      totals: totals({ posted: { activeCount: 0, activeAmountMinor: 0 } }),
    });
    expect(text(onlyReceived.root, '.summary')).toBe('Received from firms: ₱300,000.00 in 1 bond.');
  });

  it('shows no attention line when nothing has expired or is about to', async () => {
    const { root } = await open();

    expect(root.querySelector('.attention')).toBeNull();
  });

  it('offers shortcuts to the expired and expiring bonds only when there are some, and applies them', async () => {
    const { harness, root, list } = await open({
      totals: totals({ expiredCount: 1, expiringSoonCount: 2 }),
    });

    expect(texts(root, '.attention button')).toEqual(['1 has expired', '2 expire within 30 days']);
    buttonNamed(root, '2 expire within 30 days').click();
    await settle(harness.fixture);

    expect(harness.routeNativeElement).toBeTruthy();
    expect(list.mock.calls.at(-1)![0]).toMatchObject({
      status: '',
      expiry: 'ExpiringSoon',
      page: 1,
    });
  });

  it('keeps the filters folded until asked for, then applies a state', async () => {
    const { harness, root, list } = await open();

    expect(root.querySelector('#bond-filters')).toBeNull();
    buttonNamed(root, 'Filters').click();
    await settle(harness.fixture);
    const filters = root.querySelector('#bond-filters')!;
    expect(texts(filters, '.filters__label')).toEqual(['Show', 'Project', 'Whose', 'Kind']);
    // The register lists archived projects too: their bonds still lapse.
    expect(texts(filters, 'label:nth-child(2) option')).toEqual([
      'All projects',
      'Riverside Residences',
      'Harbor Warehouse',
    ]);

    const state = filters.querySelector<HTMLSelectElement>('select')!;
    state.value = 'Released';
    state.dispatchEvent(new Event('change'));
    await settle(harness.fixture);

    expect(list.mock.calls.at(-1)![0]).toMatchObject({ status: 'Released', expiry: '' });
  });

  it('opens with the filters showing when the address already carries one', async () => {
    const { root, list } = await open({ url: '/bonds?state=Expired&direction=Posted' });

    expect(root.querySelector('#bond-filters')).not.toBeNull();
    expect(list.mock.calls[0]![0]).toMatchObject({
      status: '',
      expiry: 'Expired',
      direction: 'Posted',
    });
  });

  it('offers "Add bond" to a manager and nothing to add to a reader', async () => {
    const manager = await open();
    expect(texts(manager.root, '.toolbar button')).toContain('Add bond');
    TestBed.resetTestingModule();

    const reader = await open({ permissions: READER });
    expect(reader.root.textContent).not.toContain('Add bond');
  });

  it('leads a manager with an empty register to the one next step', async () => {
    const { root } = await open({ items: [] });

    expect(text(root, 'app-empty-state h2')).toBe('No active bonds');
    expect(texts(root, 'app-empty-state button')).toEqual(['Add bond', 'Show every bond']);
    expect(root.querySelector('.toolbar')).toBeNull();
  });

  it('tells a reader with an empty register nothing can be added by them', async () => {
    const { root } = await open({ items: [], permissions: READER });

    expect(text(root, 'app-empty-state p')).toContain('appear here once someone records them');
    expect(texts(root, 'app-empty-state button')).toEqual(['Show every bond']);
  });

  it('says nothing matches when a filter finds no bond, and offers the way back', async () => {
    const { root } = await open({ items: [], url: '/bonds?state=Called' });

    expect(text(root, 'app-empty-state h2')).toBe('No bonds match');
    expect(texts(root, 'app-empty-state button')).toEqual(['Show active bonds']);
  });
});
