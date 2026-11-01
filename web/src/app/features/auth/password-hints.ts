import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { Icon } from '../../shared/ui/icon/icon';
import { checkPassword } from './password-policy';

/**
 * The password rules as a checklist that updates while the person types. Each rule has an icon and words ("done" or "not yet"
 * for screen readers), so the state never rests on colour alone.
 */
@Component({
  selector: 'app-password-hints',
  imports: [Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <ul class="hints" aria-label="Password rules">
      @for (check of checks(); track check.id) {
        <li class="hints__item" [class.hints__item--met]="check.met">
          <app-icon [name]="check.met ? 'check-circle' : 'minus-circle'" [size]="16" />
          <span>{{ check.text }}</span>
          <span class="visually-hidden">{{ check.met ? ': done' : ': not yet' }}</span>
        </li>
      }
    </ul>
  `,
  styles: `
    .hints {
      display: grid;
      gap: var(--space-1);
      margin: 0;
      padding: 0;
      list-style: none;
      font-size: var(--text-sm);
    }

    .hints__item {
      display: flex;
      align-items: center;
      gap: var(--space-2);
      color: var(--color-text-muted);
    }

    .hints__item--met {
      color: var(--color-success);
    }
  `,
})
export class PasswordHints {
  readonly password = input('');
  readonly username = input('');

  protected readonly checks = computed(() => checkPassword(this.password(), this.username()));
}
