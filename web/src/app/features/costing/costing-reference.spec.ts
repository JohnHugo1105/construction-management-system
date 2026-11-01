import { TestBed } from '@angular/core/testing';
import { Observable, of, throwError } from 'rxjs';
import type { Mock } from 'vitest';
import { ApiError } from '../../core/api/api-error';
import { CostingApi } from './costing-api';
import { CostingReferenceState } from './costing-reference';
import { referenceFixture } from './testing/costing-fixtures';

type Call = () => Observable<unknown>;

function setup(reference: Mock<Call>) {
  TestBed.configureTestingModule({ providers: [{ provide: CostingApi, useValue: { reference } }] });
  return TestBed.inject(CostingReferenceState);
}

describe('CostingReferenceState', () => {
  it('fetches the choices once, however many screens ask, and keeps them', async () => {
    const reference = vi.fn<Call>(() => of(referenceFixture()));
    const state = setup(reference);

    await Promise.all([state.load(), state.load()]);
    await state.load();

    expect(reference).toHaveBeenCalledTimes(1);
    expect(state.divisions().map((division) => division.number)).toEqual([3, 15]);
    expect(state.itemTypes().map((type) => type.label)).toContain('Subcontract work');
    expect(state.error()).toBeNull();
  });

  it('says it could not load, shows nothing meanwhile, and can be tried again', async () => {
    const failure = new ApiError({
      status: 500,
      title: 'Something went wrong',
      detail: 'Try again.',
    });
    let attempts = 0;
    const reference = vi.fn<Call>(() =>
      ++attempts === 1 ? throwError(() => failure) : of(referenceFixture()),
    );
    const state = setup(reference);

    await state.load();
    expect(state.error()?.title).toBe('Something went wrong');
    expect(state.reference()).toBeNull();
    expect(state.divisions()).toEqual([]);

    await state.load();
    expect(state.error()).toBeNull();
    expect(state.reference()?.vatRateBps).toBe(1200);
    expect(reference).toHaveBeenCalledTimes(2);
  });
});
