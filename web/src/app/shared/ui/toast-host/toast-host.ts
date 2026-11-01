import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { ToastService } from '../../../core/notifications/toast-service';
import { Button } from '../button/button';
import { Icon, IconName } from '../icon/icon';

const ICONS: Record<string, IconName> = { success: 'check-circle', info: 'info', error: 'error' };

/** Shows the toasts, above the bottom navigation on a phone. The region is always present so screen readers announce each one. */
@Component({
  selector: 'app-toast-host',
  imports: [Button, Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="toast-region" role="status" aria-live="polite" aria-atomic="false">
      @for (toast of toasts.toasts(); track toast.id) {
        <div class="toast" [class]="'toast toast--' + toast.tone">
          <app-icon [name]="iconFor(toast.tone)" />
          <p class="toast__message">{{ toast.message }}</p>
          <button
            appButton
            variant="ghost"
            [icon]="true"
            type="button"
            aria-label="Dismiss message"
            (click)="toasts.dismiss(toast.id)"
          >
            <app-icon name="close" [size]="18" />
          </button>
        </div>
      }
    </div>
  `,
  styles: `
    .toast-region {
      position: fixed;
      right: var(--space-4);
      bottom: calc(var(--bottom-nav-height) + var(--safe-bottom) + var(--space-3));
      left: var(--space-4);
      z-index: 60;
      display: grid;
      gap: var(--space-2);
      justify-items: center;
      pointer-events: none;
    }

    .toast {
      display: flex;
      align-items: center;
      gap: var(--space-3);
      width: min(100%, 28rem);
      padding: var(--space-2) var(--space-2) var(--space-2) var(--space-4);
      border: 1px solid var(--color-border-strong);
      border-radius: var(--radius-md);
      background: var(--color-surface);
      color: var(--color-text);
      box-shadow: var(--shadow-2);
      pointer-events: auto;
      animation: toast-in var(--duration-base) ease-out;
    }

    .toast--success app-icon {
      color: var(--color-success);
    }

    .toast--error app-icon {
      color: var(--color-danger);
    }

    .toast--info app-icon {
      color: var(--color-info);
    }

    .toast__message {
      flex: 1 1 auto;
      min-width: 0;
    }

    @keyframes toast-in {
      from {
        opacity: 0;
        transform: translateY(8px);
      }
    }

    @media (min-width: 64rem) {
      .toast-region {
        bottom: var(--space-6);
        left: calc(var(--rail-width) + var(--space-6));
        justify-items: start;
      }
    }
  `,
})
export class ToastHost {
  protected readonly toasts = inject(ToastService);

  protected iconFor(tone: string): IconName {
    return ICONS[tone] ?? 'info';
  }
}
