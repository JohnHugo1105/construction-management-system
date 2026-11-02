import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  computed,
  effect,
  inject,
  untracked,
} from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { Icon } from '../../shared/ui/icon/icon';
import { NotificationBellState } from './notification-bell-state';

/**
 * The bell in the header: opens the Notifications page and shows how many are unread (nothing at 0, "99+" over 99). The number is read
 * when the shell starts and when the tab comes back to the front; there is no timer. Shown to everyone who has notifications.
 */
@Component({
  selector: 'app-notification-bell',
  imports: [RouterLink, RouterLinkActive, Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (bell.applies()) {
      <a
        class="bell"
        routerLink="/notifications"
        routerLinkActive="bell--active"
        ariaCurrentWhenActive="page"
        [attr.aria-label]="name()"
      >
        <app-icon name="bell" [size]="22" />
        @if (bell.badge(); as badge) {
          <span class="bell__badge" aria-hidden="true">{{ badge }}</span>
        }
      </a>
    }
  `,
  styles: `
    :host {
      display: contents;
    }

    .bell {
      position: relative;
      display: inline-flex;
      align-items: center;
      justify-content: center;
      flex: none;
      width: var(--touch-target);
      height: var(--touch-target);
      border-radius: var(--radius-md);
      color: var(--color-text);
    }

    .bell:hover,
    .bell--active {
      background: var(--color-surface-2);
    }

    .bell:focus-visible {
      outline: var(--focus-ring-width) solid var(--color-focus);
      outline-offset: 2px;
    }

    .bell__badge {
      position: absolute;
      top: 4px;
      right: 2px;
      min-width: 20px;
      height: 20px;
      padding: 0 5px;
      border-radius: var(--radius-pill);
      background: var(--color-danger);
      color: var(--color-on-danger);
      font-size: 0.75rem;
      font-weight: 700;
      font-variant-numeric: tabular-nums;
      line-height: 20px;
      text-align: center;
    }
  `,
})
export class NotificationBell {
  protected readonly bell = inject(NotificationBellState);

  /** "Notifications, 3 unread" while something is unread; otherwise just "Notifications". */
  protected readonly name = computed(() => {
    const unread = this.bell.unread();
    return unread > 0 ? `Notifications, ${unread} unread` : 'Notifications';
  });

  constructor() {
    effect(() => {
      if (this.bell.applies()) untracked(() => this.bell.refresh());
    });
    this.bell.refreshWhenVisible(inject(DestroyRef));
  }
}
