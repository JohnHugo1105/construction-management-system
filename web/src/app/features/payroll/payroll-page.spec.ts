import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { Observable, of } from 'rxjs';
import { PagedResult } from '../../core/api/paged-result';
import { Permission } from '../../core/session/permissions';
import { SessionState } from '../../core/session/session-state';
import { Viewport } from '../../shared/ui/viewport/viewport';
import { buttonNamed, settle, text, texts } from '../../testing/dom';
import { pageOf, signInForTest } from '../../testing/session-fixture';
import { PayrollApi } from './payroll-api';
import { PayrollPage } from './payroll-page';
import { listRun } from './payroll.fixtures';
import { PayrollListParams, PayrollRunListItem } from './payroll.models';

const READER = [Permission.projectsRead, Permission.payrollRead];
const PREPARER = [...READER, Permission.payrollRun];

async function open(
  items: readonly PayrollRunListItem[],
  permissions: readonly string[],
  tablet = true,
) {
  const list = vi.fn((params: PayrollListParams): Observable<PagedResult<PayrollRunListItem>> => {
    void params;
    return of(pageOf(items));
  });
  TestBed.configureTestingModule({
    providers: [
      provideRouter([{ path: 'payroll', component: PayrollPage }]),
      provideHttpClient(),
      provideHttpClientTesting(),
      { provide: PayrollApi, useValue: { list } },
      { provide: Viewport, useValue: { isTablet: signal(tablet), isDesktop: signal(true) } },
    ],
  });
  await signInForTest(TestBed.inject(SessionState), TestBed.inject(HttpTestingController), {
    roles: ['Custom'],
    permissions,
  });
  const harness = await RouterTestingHarness.create();
  await harness.navigateByUrl('/payroll', PayrollPage);
  await settle(harness.fixture);
  return { root: harness.fixture.nativeElement as HTMLElement, list };
}

describe('PayrollPage', () => {
  it('lists the runs with period, state, workers and the API’s totals', async () => {
    const { root, list } = await open(
      [
        listRun(),
        listRun({
          id: 'run0',
          periodStart: '2026-10-04',
          periodEnd: '2026-10-17',
          status: 'Approved',
          workerCount: 1,
          totalGrossMinor: 500000,
          totalDeductionsMinor: 0,
          totalNetMinor: 500000,
        }),
      ],
      READER,
    );

    expect(list).toHaveBeenCalledWith({ page: 1, pageSize: 25 });
    const rows = root.querySelectorAll('tbody tr');
    expect(texts(rows[0]!, 'th, td')).toEqual([
      '18 Oct to 31 Oct 2026',
      'Draft',
      '2 workers',
      '₱12,625.00',
      '₱150.00',
      '₱12,475.00',
    ]);
    expect(texts(rows[1]!, 'th, td')).toEqual([
      '4 Oct to 17 Oct 2026',
      'Approved',
      '1 worker',
      '₱5,000.00',
      '₱0.00',
      '₱5,000.00',
    ]);
    expect(rows[0]!.querySelector('th a')!.getAttribute('href')).toBe('/payroll/run1');
  });

  it('shows cards with the same facts on a phone', async () => {
    const { root } = await open([listRun()], READER, false);

    expect(root.querySelector('table')).toBeNull();
    const card = root.querySelector('.row-card')!;
    expect(text(card, '.row-card__title')).toBe('18 Oct to 31 Oct 2026');
    expect(text(card, 'app-status-chip')).toBe('Draft');
    expect(text(card, '.run__net')).toBe('Net pay₱12,475.00');
  });

  it('offers a new payroll run only to a person who prepares payroll', async () => {
    const preparer = await open([listRun()], PREPARER);
    expect(buttonNamed(preparer.root, 'New payroll run').getAttribute('href')).toBe('/payroll/new');
    TestBed.resetTestingModule();

    const reader = await open([listRun()], READER);
    expect(texts(reader.root, 'a, button')).not.toContain('New payroll run');
  });

  it('names a 13th-month payroll by its year and offers it as a secondary action next to the payroll run', async () => {
    const { root } = await open(
      [
        listRun({
          id: 'run13',
          kind: 'ThirteenthMonth',
          year: 2026,
          periodStart: '2026-01-01',
          periodEnd: '2026-12-31',
        }),
      ],
      PREPARER,
    );

    expect(text(root, 'tbody th')).toBe('13th-month pay 2026');
    expect(buttonNamed(root, 'New payroll run').className).toContain('primary');
    const thirteenth = buttonNamed(root, '13th-month pay');
    expect(thirteenth.getAttribute('href')).toBe('/payroll/thirteenth-month');
    expect(thirteenth.className).not.toContain('primary');
    expect(texts(root, '.quiet-link')).toEqual([
      'Pay rules',
      'Holidays',
      'Contribution and tax tables',
    ]);
  });

  it('offers the one next step once when there are no runs yet', async () => {
    const { root } = await open([], PREPARER);

    expect(text(root, 'app-empty-state')).toContain('No payroll runs yet');
    expect(texts(root, 'a').filter((label) => label === 'New payroll run')).toHaveLength(1);
  });

  it('tells a person who cannot prepare payroll who can, when there are no runs yet', async () => {
    const { root } = await open([], READER);

    expect(text(root, 'app-empty-state')).toContain(
      'An HR officer or an accountant can create the first payroll run.',
    );
    expect(texts(root, 'a, button')).not.toContain('New payroll run');
  });
});
