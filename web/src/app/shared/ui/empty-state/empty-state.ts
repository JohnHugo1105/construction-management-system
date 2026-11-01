import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { Icon, IconName } from '../icon/icon';

/**
 * What a list or panel shows when there is nothing in it yet: say what is missing and put the next step in a button below
 * (project it as content). "This project has no budget yet. Add your first budget line to start tracking cost."
 */
@Component({
  selector: 'app-empty-state',
  imports: [Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="empty">
      <app-icon [name]="icon()" [size]="32" />
      <h2 class="empty__title">{{ title() }}</h2>
      @if (message()) {
        <p class="empty__message">{{ message() }}</p>
      }
      <div class="empty__actions"><ng-content /></div>
    </div>
  `,
  styles: `
    :host {
      display: block;
    }

    .empty {
      display: grid;
      justify-items: center;
      gap: var(--space-2);
      padding: var(--space-8) var(--space-4);
      border: 1px dashed var(--color-border-strong);
      border-radius: var(--radius-lg);
      background: var(--color-surface);
      color: var(--color-text-muted);
      text-align: center;
    }

    .empty__title {
      font-size: var(--text-lg);
      color: var(--color-text);
    }

    .empty__message {
      max-width: 32rem;
    }

    .empty__actions {
      margin-top: var(--space-2);
    }

    .empty__actions:empty {
      display: none;
    }
  `,
})
export class EmptyState {
  readonly title = input.required<string>();
  readonly message = input('');
  readonly icon = input<IconName>('info');
}
