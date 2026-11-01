import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Observable, of, throwError } from 'rxjs';
import type { Mock } from 'vitest';
import { Permission } from '../../core/session/permissions';
import { SessionState } from '../../core/session/session-state';
import { settle, text, type } from '../../testing/dom';
import { signInForTest } from '../../testing/session-fixture';
import { validationError } from '../workforce/workforce.fixtures';
import { BillingApi } from './billing-api';
import { projectBilling } from './billing.fixtures';
import { BillingTerms } from './billing.models';
import { TermsDialog } from './terms-dialog';

type Call = (...args: unknown[]) => Observable<unknown>;
const stub = (implementation: Call): Mock<Call> => vi.fn<Call>(implementation);

const TERMS: BillingTerms = { ...projectBilling().terms, advanceBps: 1_000, vatWithholdingBps: 500 };

@Component({
  imports: [TermsDialog],
  template: `<app-terms-dialog projectId="p1" [terms]="terms" (saved)="saved.set($event)" />`,
})
class Host {
  readonly terms = TERMS;
  readonly saved = signal<BillingTerms | null>(null);
}

async function open(saveTerms: Mock<Call>) {
  TestBed.configureTestingModule({
    providers: [
      provideHttpClient(),
      provideHttpClientTesting(),
      { provide: BillingApi, useValue: { saveTerms } },
    ],
  });
  await signInForTest(TestBed.inject(SessionState), TestBed.inject(HttpTestingController), {
    roles: ['Custom'],
    permissions: [Permission.projectsRead, Permission.billingRead, Permission.billingManage],
  });
  const fixture = TestBed.createComponent(Host);
  await settle(fixture);
  const dialog = () => document.querySelector('app-terms-dialog')!;
  const input = (name: string) =>
    dialog().querySelector<HTMLInputElement>(`[formcontrolname="${name}"] input`)!;
  const government = () =>
    dialog().querySelector<HTMLInputElement>('input[type="checkbox"][formcontrolname]')!;
  const submit = async () => {
    dialog().querySelector<HTMLButtonElement>('button[type="submit"]')!.click();
    await settle(fixture);
  };
  return { fixture, dialog, input, government, submit };
}

describe('TermsDialog', () => {
  it('shows the saved down payment, VAT withheld and government setting, and saves all of them', async () => {
    const saveTerms = stub(() => of({ ...TERMS, isGovernmentClient: true, version: 2 }));
    const { fixture, dialog, input, government, submit } = await open(saveTerms);

    expect(input('advanceBps').value).toBe('10');
    expect(input('vatWithholdingBps').value).toBe('5');
    expect(government().checked).toBe(false);
    expect(text(dialog(), '.choice__label')).toBe('Government client');

    type(dialog(), '[formcontrolname="advanceBps"] input', '15');
    government().click();
    await settle(fixture);
    await submit();

    expect(saveTerms).toHaveBeenCalledWith('p1', {
      retentionBps: 1_000,
      creditableWithholdingBps: 200,
      paymentTermsDays: 30,
      vatWithholdingBps: 500,
      advanceBps: 1_500,
      isGovernmentClient: true,
      version: 1,
    });
    expect(fixture.componentInstance.saved()?.isGovernmentClient).toBe(true);
  });

  it('puts the server’s refusal of a government down payment under the down payment field', async () => {
    const message = 'A government project allows a down payment of up to 15% of the contract price.';
    const saveTerms = stub(() => throwError(() => validationError({ advanceBps: [message] })));
    const { dialog, government, fixture, submit } = await open(saveTerms);

    government().click();
    await settle(fixture);
    type(dialog(), '[formcontrolname="advanceBps"] input', '20');
    await submit();

    expect(text(dialog(), '.field__error')).toBe(message);
  });
});
