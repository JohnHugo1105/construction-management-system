import { Injectable, Signal, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { ApiError, asApiError } from '../../core/api/api-error';
import { SubcontractsApi } from './subcontracts-api';
import { Subcontract } from './subcontracts.models';

export type LoadStatus = 'loading' | 'ready' | 'error';

/**
 * The subcontract a page shows. The page provides and loads it; every action answers with the whole subcontract, which replaces
 * this one, so the figures on screen are always the server's latest.
 */
@Injectable()
export class SubcontractState {
  private readonly api = inject(SubcontractsApi);
  private readonly current = signal<Subcontract | null>(null);
  private requestedId: string | null = null;

  readonly status = signal<LoadStatus>('loading');
  readonly error = signal<ApiError | null>(null);
  readonly refreshing = signal(false);

  /** The loaded subcontract. Read it only once `status()` is 'ready'. */
  readonly subcontract: Signal<Subcontract> = computed(() => {
    const value = this.current();
    if (!value) throw new Error('The subcontract has not loaded yet.');
    return value;
  });

  async load(id: string): Promise<void> {
    if (this.requestedId !== id) {
      this.requestedId = id;
      this.current.set(null);
      this.status.set('loading');
      this.error.set(null);
    }
    await this.fetch(id);
  }

  async reload(): Promise<void> {
    if (this.requestedId) await this.fetch(this.requestedId);
  }

  replace(subcontract: Subcontract): void {
    this.current.set(subcontract);
    this.status.set('ready');
    this.error.set(null);
  }

  private async fetch(id: string): Promise<void> {
    this.refreshing.set(this.current() !== null);
    try {
      const value = await firstValueFrom(this.api.get(id));
      if (this.requestedId === id) this.replace(value);
    } catch (failure) {
      if (this.requestedId === id && this.current() === null) {
        this.error.set(asApiError(failure));
        this.status.set('error');
      }
    } finally {
      this.refreshing.set(false);
    }
  }
}
