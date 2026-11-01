import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { SessionState } from '../../../core/session/session-state';
import { settle, text } from '../../../testing/dom';
import { signInForTest } from '../../../testing/session-fixture';
import { CommitmentPosition } from '../costing.models';
import { CommitmentLine } from './commitment-line';
import { CostPosition } from './cost-position';

async function show(commitments: CommitmentPosition | null | undefined) {
  TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
  await signInForTest(TestBed.inject(SessionState), TestBed.inject(HttpTestingController));
  const fixture = TestBed.createComponent(CommitmentLine);
  fixture.componentRef.setInput('commitments', commitments);
  await settle(fixture);
  return fixture.nativeElement as HTMLElement;
}

const position = (forecastHealth: CommitmentPosition['forecastHealth']): CommitmentPosition => ({
  committedMinor: 80_000_000,
  forecastMinor: 120_000_000,
  forecastRemainingMinor: 5_000_000,
  forecastHealth,
});

describe('CommitmentLine', () => {
  it('shows nothing when nothing is committed', async () => {
    expect((await show(null)).textContent?.trim()).toBe('');
    TestBed.resetTestingModule();
    expect((await show(undefined)).textContent?.trim()).toBe('');
  });

  it('shows what is committed and the forecast, with no chip while the forecast is on track', async () => {
    const root = await show(position('OnTrack'));

    expect(text(root, '.commitments')).toBe('Committed ₱800,000.00Forecast ₱1,200,000.00');
    expect(root.querySelector('app-status-chip')).toBeNull();
  });

  it('warns, in the warning tone and in words, when the forecast is over budget or near the limit', async () => {
    const over = await show(position('OverBudget'));
    expect(text(over, 'app-status-chip')).toBe('Forecast over budget');
    expect(over.querySelector('.chip--warning')).not.toBeNull();
    expect(over.querySelector('.chip--danger')).toBeNull();
    TestBed.resetTestingModule();

    expect(text(await show(position('NearLimit')), 'app-status-chip')).toBe('Forecast near the limit');
  });

  it('sits under the figures of the cost position when given, and leaves them alone when not', async () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    await signInForTest(TestBed.inject(SessionState), TestBed.inject(HttpTestingController));
    const fixture = TestBed.createComponent(CostPosition);
    fixture.componentRef.setInput('figures', { budgetMinor: 100, actualMinor: 50, remainingMinor: 50, usedBps: 5000, health: 'OnTrack' });
    await settle(fixture);
    expect(fixture.nativeElement.querySelector('app-commitment-line')).toBeNull();

    fixture.componentRef.setInput('commitments', position('OverBudget'));
    await settle(fixture);
    expect(text(fixture.nativeElement, 'app-commitment-line')).toContain('Committed ₱800,000.00');
  });
});
