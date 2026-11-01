import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { Observable, of } from 'rxjs';
import type { Mock } from 'vitest';
import { Permission } from '../../core/session/permissions';
import { SessionState } from '../../core/session/session-state';
import { Viewport } from '../../shared/ui/viewport/viewport';
import { settle, text, texts } from '../../testing/dom';
import { signInForTest } from '../../testing/session-fixture';
import { BillingApi } from './billing-api';
import { BillingPage } from './billing-page';
import { receivables } from './billing.fixtures';
import { Receivables } from './billing.models';

type Call = (...args: unknown[]) => Observable<unknown>;
const stub = (implementation: Call): Mock<Call> => vi.fn<Call>(implementation);

async function open(data: Receivables, tablet: boolean) {
  const api = { receivables: stub(() => of(data)) };
  TestBed.configureTestingModule({
    providers: [
      provideRouter([{ path: 'billing', component: BillingPage }]),
      provideHttpClient(),
      provideHttpClientTesting(),
      { provide: BillingApi, useValue: api },
      { provide: Viewport, useValue: { isTablet: signal(tablet), isDesktop: signal(tablet) } },
    ],
  });
  await signInForTest(TestBed.inject(SessionState), TestBed.inject(HttpTestingController), {
    roles: ['Custom'],
    permissions: [Permission.projectsRead, Permission.billingRead],
  });
  const harness = await RouterTestingHarness.create();
  await harness.navigateByUrl('/billing', BillingPage);
  await settle(harness.fixture);
  return { root: harness.fixture.nativeElement as HTMLElement, api };
}

describe('BillingPage', () => {
  it('shows the company’s totals and each project’s money still to collect, overdue first, with one way in', async () => {
    const { root, api } = await open(receivables(), true);

    expect(api.receivables).toHaveBeenCalledWith(1, 25);
    expect(texts(root, '.totals__item')).toEqual([
      'Still to collect₱3,940,000.00',
      'Overdue₱976,000.00',
      'Retention held₱560,000.00',
    ]);
    const row = root.querySelector('tbody tr')!;
    expect(texts(row, 'th, td')).toEqual([
      'Riverside Residences ₱976,000.00 overdue',
      '₱3,940,000.00',
      '₱560,000.00',
      'Open',
    ]);
    expect(row.querySelector('a')!.getAttribute('href')).toBe('/projects/p1/billing');
  });

  it('shows cards on a phone, and says when there is nothing to collect', async () => {
    const phone = await open(receivables(), false);
    expect(text(phone.root, '.row-card__title')).toBe('Riverside Residences');
    TestBed.resetTestingModule();

    const nothing = await open(
      receivables({
        outstandingMinor: 0,
        overdueMinor: 0,
        retentionHeldMinor: 0,
        rows: { items: [], total: 0, page: 1, pageSize: 25 },
      }),
      false,
    );
    expect(text(nothing.root, 'app-empty-state h2')).toBe('Nothing to collect');
  });
});
