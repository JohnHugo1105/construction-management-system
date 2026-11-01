import { Injectable, Signal, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { ApiError, asApiError } from '../../core/api/api-error';
import { BillingApi } from './billing-api';
import { Bill } from './billing.models';

export type BillLoadStatus = 'loading' | 'ready' | 'error';

/**
 * The bill a page shows. The page provides one and loads it; each action returns the whole bill, which replaces the one on screen,
 * so the figures shown are always the server's latest.
 */
@Injectable()
export class BillState {
  private readonly api = inject(BillingApi);
  private readonly current = signal<Bill | null>(null);
  private requestedId: string | null = null;

  readonly status = signal<BillLoadStatus>('loading');
  readonly error = signal<ApiError | null>(null);
  readonly refreshing = signal(false);

  /** The loaded bill. Read it only once `status()` is 'ready'. */
  readonly bill: Signal<Bill> = computed(() => {
    const bill = this.current();
    if (!bill) throw new Error('The bill has not loaded yet.');
    return bill;
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

  replace(bill: Bill): void {
    this.current.set(bill);
    this.status.set('ready');
    this.error.set(null);
  }

  private async fetch(id: string): Promise<void> {
    this.refreshing.set(this.current() !== null);
    try {
      const bill = await firstValueFrom(this.api.get(id));
      if (this.requestedId === id) this.replace(bill);
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
