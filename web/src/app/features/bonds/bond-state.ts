import { Injectable, Signal, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { ApiError, asApiError } from '../../core/api/api-error';
import { BondsApi } from './bonds-api';
import { Bond } from './bonds.models';

export type LoadStatus = 'loading' | 'ready' | 'error';

/**
 * The bond a page shows. The page provides and loads it; every step answers with the whole bond, which replaces this one, so what is
 * on screen is always the server's latest.
 */
@Injectable()
export class BondState {
  private readonly api = inject(BondsApi);
  private readonly current = signal<Bond | null>(null);
  private requestedId: string | null = null;

  readonly status = signal<LoadStatus>('loading');
  readonly error = signal<ApiError | null>(null);
  readonly refreshing = signal(false);

  /** The loaded bond. Read it only once `status()` is 'ready'. */
  readonly bond: Signal<Bond> = computed(() => {
    const value = this.current();
    if (!value) throw new Error('The bond has not loaded yet.');
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

  replace(bond: Bond): void {
    this.current.set(bond);
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
