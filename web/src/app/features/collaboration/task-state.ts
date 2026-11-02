import { Injectable, Signal, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { ApiError, asApiError } from '../../core/api/api-error';
import { CollaborationApi } from './collaboration-api';
import { WorkTask } from './collaboration.models';

export type LoadStatus = 'loading' | 'ready' | 'error';

/**
 * The task a page shows. The page provides and loads it; every step answers with the whole task, which replaces this one, so what is on
 * screen is always the server's latest.
 */
@Injectable()
export class TaskState {
  private readonly api = inject(CollaborationApi);
  private readonly current = signal<WorkTask | null>(null);
  private requestedId: string | null = null;

  readonly status = signal<LoadStatus>('loading');
  readonly error = signal<ApiError | null>(null);
  readonly refreshing = signal(false);

  /** The loaded task. Read it only once `status()` is 'ready'. */
  readonly task: Signal<WorkTask> = computed(() => {
    const value = this.current();
    if (!value) throw new Error('The task has not loaded yet.');
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

  replace(task: WorkTask): void {
    this.current.set(task);
    this.status.set('ready');
    this.error.set(null);
  }

  private async fetch(id: string): Promise<void> {
    this.refreshing.set(this.current() !== null);
    try {
      const value = await firstValueFrom(this.api.getTask(id));
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
