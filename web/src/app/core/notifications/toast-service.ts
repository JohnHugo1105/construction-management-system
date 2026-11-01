import { Injectable, signal } from '@angular/core';

export type ToastTone = 'success' | 'info' | 'error';

export interface Toast {
  readonly id: number;
  readonly message: string;
  readonly tone: ToastTone;
}

const SHOW_FOR_MS: Record<ToastTone, number> = { success: 5000, info: 5000, error: 9000 };

/**
 * Short confirmations that do not block ("Project saved"). A toast says something already happened; it is never the whole
 * implementation of a button. The ToastHost in the shell shows them and announces them to screen readers.
 */
@Injectable({ providedIn: 'root' })
export class ToastService {
  private readonly items = signal<readonly Toast[]>([]);
  private nextId = 1;

  readonly toasts = this.items.asReadonly();

  success(message: string): void {
    this.show(message, 'success');
  }

  info(message: string): void {
    this.show(message, 'info');
  }

  error(message: string): void {
    this.show(message, 'error');
  }

  dismiss(id: number): void {
    this.items.update((list) => list.filter((toast) => toast.id !== id));
  }

  private show(message: string, tone: ToastTone): void {
    const toast: Toast = { id: this.nextId++, message, tone };
    this.items.update((list) => [...list.slice(-1), toast]);
    setTimeout(() => this.dismiss(toast.id), SHOW_FOR_MS[tone]);
  }
}
