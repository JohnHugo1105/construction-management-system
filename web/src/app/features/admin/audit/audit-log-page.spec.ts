import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { Observable, of } from 'rxjs';
import type { Mock } from 'vitest';
import { SessionState } from '../../../core/session/session-state';
import { buttonNamed, settle, text, texts, type } from '../../../testing/dom';
import { pageOf, signInForTest } from '../../../testing/session-fixture';
import { AuditApi } from './audit-api';
import { AuditLogPage } from './audit-log-page';
import { AuditEntry, AuditListParams, AUDIT_ENTITY_TYPES } from './audit.models';

const entries: AuditEntry[] = [
  {
    id: 'a1',
    occurredAtUtc: '2026-12-03T06:15:00Z',
    userName: 'juan',
    action: 'Project.Archived',
    entityType: 'Project',
    entityId: 'p1',
    summary: 'Archived project Harbour Tower (finished).',
  },
  {
    id: 'a2',
    occurredAtUtc: '2026-12-02T23:05:00Z',
    userName: 'maria',
    action: 'BudgetLine.Created',
    entityType: 'SiteDiary',
    entityId: 'b1',
    summary: 'Added a budget line.',
  },
];

type Call = (params: AuditListParams) => Observable<unknown>;

async function open(url = '/admin/audit', timeZoneId = 'Asia/Manila') {
  const list: Mock<Call> = vi.fn<Call>(() => of(pageOf(entries, 60)));
  TestBed.configureTestingModule({
    providers: [
      provideRouter([{ path: 'admin/audit', component: AuditLogPage }]),
      provideHttpClient(),
      provideHttpClientTesting(),
      { provide: AuditApi, useValue: { list } },
    ],
  });
  await signInForTest(TestBed.inject(SessionState), TestBed.inject(HttpTestingController), {
    timeZoneId,
  });
  const harness = await RouterTestingHarness.create();
  await harness.navigateByUrl(url, AuditLogPage);
  await settle(harness.fixture);
  return {
    harness,
    list,
    root: harness.fixture.nativeElement as HTMLElement,
    router: TestBed.inject(Router),
  };
}

const lastRequest = (list: Mock<Call>) => list.mock.calls.at(-1)![0];

describe('AuditLogPage', () => {
  it('shows when, who and what in the company time zone, with the area in words', async () => {
    const { root } = await open();

    const rows = root.querySelectorAll('tbody tr');
    expect(rows).toHaveLength(2);
    expect(texts(rows[0]!, 'th, td')).toEqual([
      '3 Dec 2026, 2:15 pm',
      'juan',
      'Archived project Harbour Tower (finished).',
      'Projects',
    ]);
    // An area the page has no wording for yet is spaced out rather than shown as a code.
    expect(texts(rows[1]!, 'th, td')).toEqual([
      '3 Dec 2026, 7:05 am',
      'maria',
      'Added a budget line.',
      'Site diary',
    ]);
    expect(root.textContent).not.toContain('Project.Archived');
  });

  it('asks for the newest page, with nothing filtered', async () => {
    const { list } = await open();

    expect(lastRequest(list)).toEqual({
      search: '',
      entityType: '',
      from: '',
      to: '',
      includeSignIns: false,
      page: 1,
      pageSize: 25,
    });
  });

  it('turns a date range into the exact instants of the company day, and includes the end day', async () => {
    const { list } = await open('/admin/audit?from=2026-12-03&to=2026-12-05');

    // Manila is 8 hours ahead of UTC: 3 Dec starts at 16:00 UTC on 2 Dec; the range ends when 6 Dec starts.
    expect(lastRequest(list)).toMatchObject({
      from: '2026-12-02T16:00:00.000Z',
      to: '2026-12-05T16:00:00.000Z',
    });
  });

  it('uses the time zone of the company, not of the browser', async () => {
    const { list } = await open('/admin/audit?from=2026-12-03', 'America/Los_Angeles');

    expect(lastRequest(list)).toMatchObject({ from: '2026-12-03T08:00:00.000Z', to: '' });
  });

  it('reads search and area from a shared link, and opens the filters when some are set', async () => {
    const { list, root } = await open('/admin/audit?search=harbour&area=Project');

    expect(lastRequest(list)).toMatchObject({ search: 'harbour', entityType: 'Project' });
    expect(root.querySelector('#audit-filters')).not.toBeNull();
    expect((root.querySelector('input[type="search"]') as HTMLInputElement).value).toBe('harbour');
  });

  it('keeps the filters folded until asked, and says in a line what is applied when they are folded', async () => {
    const { harness, root } = await open();
    expect(root.querySelector('#audit-filters')).toBeNull();
    expect(buttonNamed(root, 'Filters').getAttribute('aria-expanded')).toBe('false');

    buttonNamed(root, 'Filters').click();
    await settle(harness.fixture);

    expect(root.querySelector('#audit-filters')).not.toBeNull();
    expect(buttonNamed(root, 'Filters').getAttribute('aria-expanded')).toBe('true');
    expect(texts(root, '#audit-filters .field__label')).toEqual(['Area', 'From', 'To']);
  });

  it('filters by area from the select and starts again at page 1', async () => {
    const { harness, list, root, router } = await open('/admin/audit?page=2');
    buttonNamed(root, 'Filters').click();
    await settle(harness.fixture);

    const select = root.querySelector<HTMLSelectElement>('#audit-filters select')!;
    select.value = 'User';
    select.dispatchEvent(new Event('change', { bubbles: true }));
    await settle(harness.fixture);

    expect(lastRequest(list)).toMatchObject({ entityType: 'User', page: 1 });
    expect(router.url).toBe('/admin/audit?area=User');
  });

  it('hides routine sign-ins until the person asks for them', async () => {
    const { harness, list, root, router } = await open('/admin/audit?page=2');
    buttonNamed(root, 'Filters').click();
    await settle(harness.fixture);

    const toggle = root.querySelector<HTMLInputElement>('#audit-filters input[type="checkbox"]')!;
    expect(toggle.checked).toBe(false);
    toggle.checked = true;
    toggle.dispatchEvent(new Event('change', { bubbles: true }));
    await settle(harness.fixture);

    expect(lastRequest(list)).toMatchObject({ includeSignIns: true, page: 1 });
    expect(router.url).toContain('signIns');
  });

  it('knows the areas the cost, payroll and stock features write to', () => {
    expect(AUDIT_ENTITY_TYPES.map((area) => area.value)).toEqual(
      expect.arrayContaining(['BudgetLine', 'CostEntry', 'Worker', 'AttendanceEntry', 'PayrollRun', 'InventoryItem']),
    );
  });

  it('tells the person when the end date is before the start date', async () => {
    const { root } = await open('/admin/audit?from=2026-12-05&to=2026-12-03');

    expect(text(root, '#audit-filters .field__error')).toBe(
      "The end date can't be before the start date.",
    );
  });

  it('searches after a pause in typing and keeps the filters', async () => {
    const { harness, list, root } = await open('/admin/audit?area=User');

    vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout'] });
    try {
      type(root, 'input[type="search"]', 'maria');
      vi.advanceTimersByTime(300);
    } finally {
      vi.useRealTimers();
    }
    await settle(harness.fixture);

    expect(lastRequest(list)).toMatchObject({ search: 'maria', entityType: 'User' });
  });

  it('pages with the pager', async () => {
    const { harness, list, root } = await open();
    expect(text(root, '.pager__summary')).toBe('Showing 1 to 25 of 60 entries');

    buttonNamed(root, 'Next').click();
    await settle(harness.fixture);

    expect(lastRequest(list)).toMatchObject({ page: 2 });
  });
});
