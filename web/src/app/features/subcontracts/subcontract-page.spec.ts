import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { Observable, of, throwError } from 'rxjs';
import type { Mock } from 'vitest';
import { SessionState } from '../../core/session/session-state';
import { Viewport } from '../../shared/ui/viewport/viewport';
import { buttonNamed, settle, text, texts } from '../../testing/dom';
import { signInForTest } from '../../testing/session-fixture';
import { ruleViolation } from '../workforce/workforce.fixtures';
import { SubcontractPage } from './subcontract-page';
import { SubcontractsApi } from './subcontracts-api';
import { APPROVER, DRAFTER, READER, claim, draftClaim, subcontract } from './subcontracts.fixtures';
import { Subcontract } from './subcontracts.models';

type Call = (...args: unknown[]) => Observable<unknown>;
const stub = (implementation: Call): Mock<Call> => vi.fn<Call>(implementation);

async function open(
  current: Subcontract,
  permissions: readonly string[],
  api: Record<string, Mock<Call>> = {},
) {
  TestBed.configureTestingModule({
    providers: [
      provideRouter(
        [{ path: 'subcontracts/:id', component: SubcontractPage }],
        withComponentInputBinding(),
      ),
      provideHttpClient(),
      provideHttpClientTesting(),
      { provide: SubcontractsApi, useValue: { get: stub(() => of(current)), ...api } },
      { provide: Viewport, useValue: { isTablet: signal(false), isDesktop: signal(false) } },
    ],
  });
  await signInForTest(TestBed.inject(SessionState), TestBed.inject(HttpTestingController), {
    roles: ['Custom'],
    permissions,
  });
  const harness = await RouterTestingHarness.create();
  await harness.navigateByUrl(`/subcontracts/${current.id}`, SubcontractPage);
  await settle(harness.fixture);
  return { fixture: harness.fixture, root: harness.fixture.nativeElement as HTMLElement };
}

describe('SubcontractPage', () => {
  it('leads with the server’s money position and shows a reader no buttons', async () => {
    const { root } = await open(subcontract(), READER);

    expect(text(root, '.head app-status-chip')).toBe('Awarded');
    expect(text(root, '.head__who')).toBe('Dela Cruz Electrical · Riverside Residences');
    expect(texts(root, '.figures__item')).toEqual([
      'Certified₱250,000.0025% of ₱1,000,000.00 before VAT',
      'Still to claim₱750,000.00before VAT',
      'Owed₱247,000.00on certified claims',
      'Retention held₱28,000.00paid at completion',
    ]);
    expect(root.querySelector('.actions')).toBeNull();
    expect(root.querySelectorAll('.claim__actions button')).toHaveLength(0);
  });

  it('says where the certified work is counted in the budget, or that it is not placed', async () => {
    const placed = await open(subcontract(), READER);
    expect(placed.root.querySelector('.text-muted')?.textContent).toContain(
      'Counted under Cast-In-Place Concrete',
    );
    TestBed.resetTestingModule();

    const unplaced = await open(subcontract({ wbsCode: null, workItem: null }), READER);
    expect(unplaced.root.querySelector('.text-muted')?.textContent).toContain(
      'Not placed in the work breakdown',
    );
  });

  it('lets an approver certify the open draft, saying what it posts, and shows the answer', async () => {
    const withDraft = subcontract({ claims: [draftClaim(), claim()], version: 5 });
    const certified = subcontract({
      claims: [claim({ id: 'c2', claimNumber: 2 }), claim()],
      version: 6,
    });
    const certifyClaim = stub(() => of(certified));
    const { fixture, root } = await open(withDraft, APPROVER, { certifyClaim });

    buttonNamed(root, 'Certify claim').click();
    await settle(fixture);
    expect(document.body.textContent).toContain(
      "Certifying posts ₱392,000.00 with VAT to the project's cost and makes ₱345,800.00 payable to Dela Cruz Electrical.",
    );
    const confirm = Array.from(document.querySelectorAll<HTMLButtonElement>('dialog button')).find(
      (button) => button.textContent?.trim() === 'Certify claim',
    )!;
    confirm.click();
    await settle(fixture);

    expect(certifyClaim).toHaveBeenCalledWith('s1', 'c2', 5);
    expect(texts(root, '.claim .row-card__title')).toEqual(['Claim 2', 'Claim 1']);
  });

  it('keeps a refusal in the dialog in the server’s words', async () => {
    const certifyClaim = stub(() =>
      throwError(() => ruleViolation('Open the claim and save it again to bring it up to date.')),
    );
    const { fixture, root } = await open(
      subcontract({ claims: [draftClaim(), claim()] }),
      APPROVER,
      { certifyClaim },
    );

    buttonNamed(root, 'Certify claim').click();
    await settle(fixture);
    Array.from(document.querySelectorAll<HTMLButtonElement>('dialog button'))
      .find((button) => button.textContent?.trim() === 'Certify claim')!
      .click();
    await settle(fixture);

    expect(document.querySelector('dialog')?.textContent).toContain(
      'Open the claim and save it again',
    );
  });

  it('gives a drafter a new claim and the draft edit, never money steps', async () => {
    const { root } = await open(subcontract(), DRAFTER);

    // An awarded subcontract's variations and cancellation belong to approvers.
    expect(texts(root, '.actions .btn')).toEqual(['New claim']);
    expect(root.textContent).not.toContain('Record payment');
    expect(root.textContent).not.toContain('Void claim');
  });

  it('offers paying the oldest owed claim once, as the primary step', async () => {
    const { root } = await open(subcontract(), APPROVER);

    expect(texts(root, '.actions .btn--primary')).toEqual(['Record payment for claim 1']);
    expect(texts(root, '.claim__actions .btn')).toEqual(['Void claim']);
  });

  it('shows what was paid and released, never zero tiles, once everything is settled', async () => {
    const settled = subcontract({
      status: 'Completed',
      outstandingMinor: 0,
      paidOnClaimsMinor: 24_700_000,
      retentionBalanceMinor: 0,
      retentionReleasedMinor: 2_800_000,
      claims: [claim({ outstandingMinor: 0, paidMinor: 24_700_000 })],
    });
    const { root } = await open(settled, READER);

    expect(texts(root, '.figures__item dt')).toEqual(['Certified', 'Paid', 'Retention released']);
  });

  it('is read-only on an archived project and says why', async () => {
    const { root } = await open(subcontract({ projectArchived: true }), [...DRAFTER, ...APPROVER]);

    expect(text(root, '.waiting')).toBe(
      'This project is archived, so its subcontracts can be read but not changed.',
    );
    expect(root.querySelector('.actions')).toBeNull();
  });
});
