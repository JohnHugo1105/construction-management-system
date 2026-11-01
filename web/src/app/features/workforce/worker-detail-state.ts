import { Injectable, Signal, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { ApiError, asApiError } from '../../core/api/api-error';
import { WorkforceApi } from './workforce-api';
import { WorkerDetail } from './workforce.models';

export type WorkerLoadStatus = 'loading' | 'ready' | 'error';

/**
 * The worker a detail page shows. The page provides one and loads it; the panels and dialogs under it read the worker from here
 * and hand back the record each action returned (every worker action answers with the whole worker), so no part of the page
 * keeps its own copy.
 */
@Injectable()
export class WorkerDetailState {
  private readonly api = inject(WorkforceApi);
  private readonly current = signal<WorkerDetail | null>(null);
  private requestedId: string | null = null;

  readonly status = signal<WorkerLoadStatus>('loading');
  readonly error = signal<ApiError | null>(null);
  readonly refreshing = signal(false);

  /** The loaded worker. Read it only once `status()` is 'ready'. */
  readonly worker: Signal<WorkerDetail> = computed(() => {
    const worker = this.current();
    if (!worker) throw new Error('The worker has not loaded yet.');
    return worker;
  });

  /**
   * Whether the API sent pay with this worker. It does that only for people who hold payroll.read, so this is how every pay
   * control on the page decides to exist: no pay in the response means no rate, no history and no way to change it.
   */
  readonly showsPay = computed(() => {
    const worker = this.current();
    return worker !== null && (worker.currentRateMinor !== null || worker.rates.length > 0);
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

  /** Takes the worker an action returned. */
  replace(worker: WorkerDetail): void {
    this.current.set(worker);
    this.status.set('ready');
    this.error.set(null);
  }

  private async fetch(id: string): Promise<void> {
    this.refreshing.set(this.current() !== null);
    try {
      const worker = await firstValueFrom(this.api.get(id));
      if (this.requestedId === id) this.replace(worker);
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
