import { Injectable, Signal, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { ApiError, asApiError } from '../../core/api/api-error';
import { PayrollApi } from './payroll-api';
import { PayrollRun } from './payroll.models';

export type RunLoadStatus = 'loading' | 'ready' | 'error';

/**
 * The payroll run a page shows. The page provides one and loads it; the dialogs under it hand back the run each action
 * returned (every action answers with the whole run), so the figures on screen are always the API's latest.
 */
@Injectable()
export class PayrollRunState {
  private readonly api = inject(PayrollApi);
  private readonly current = signal<PayrollRun | null>(null);
  private requestedId: string | null = null;

  readonly status = signal<RunLoadStatus>('loading');
  readonly error = signal<ApiError | null>(null);
  readonly refreshing = signal(false);

  /** The loaded run. Read it only once `status()` is 'ready'. */
  readonly run: Signal<PayrollRun> = computed(() => {
    const run = this.current();
    if (!run) throw new Error('The payroll run has not loaded yet.');
    return run;
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

  /** Takes the run an action returned. */
  replace(run: PayrollRun): void {
    this.current.set(run);
    this.status.set('ready');
    this.error.set(null);
  }

  private async fetch(id: string): Promise<void> {
    this.refreshing.set(this.current() !== null);
    try {
      const run = await firstValueFrom(this.api.get(id));
      if (this.requestedId === id) this.replace(run);
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
