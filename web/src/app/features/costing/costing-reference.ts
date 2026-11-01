import { Injectable, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { ApiError, asApiError } from '../../core/api/api-error';
import { CostingApi } from './costing-api';
import { CostingReference } from './costing.models';

/**
 * The choices the budget and cost forms and filters offer (divisions and codes, unit suggestions, item types, VAT choices).
 * They never change while the program runs, so they are fetched once, the first time a screen needs them, and kept for the
 * session. A failed load can be tried again.
 */
@Injectable({ providedIn: 'root' })
export class CostingReferenceState {
  private readonly api = inject(CostingApi);
  private readonly current = signal<CostingReference | null>(null);
  private pending: Promise<void> | null = null;

  /** The loaded choices, or null before they arrive. */
  readonly reference = this.current.asReadonly();
  readonly loading = signal(false);
  readonly error = signal<ApiError | null>(null);

  readonly divisions = computed(() => this.current()?.divisions ?? []);
  readonly itemTypes = computed(() => this.current()?.itemTypes ?? []);

  /** Fetches the choices unless they are already here or on their way. */
  load(): Promise<void> {
    if (this.current() !== null) return Promise.resolve();
    this.pending ??= this.fetch();
    return this.pending;
  }

  private async fetch(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      this.current.set(await firstValueFrom(this.api.reference()));
    } catch (failure) {
      this.error.set(asApiError(failure));
    } finally {
      this.loading.set(false);
      this.pending = null;
    }
  }
}
