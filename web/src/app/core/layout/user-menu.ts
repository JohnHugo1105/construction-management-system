import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { Icon } from '../../shared/ui/icon/icon';
import { Menu } from '../../shared/ui/menu/menu';
import { ToastService } from '../notifications/toast-service';
import { SIGN_IN_URL } from '../session/auth-guards';
import { SessionState } from '../session/session-state';

/** Who is signed in, with their roles and the two things they can do about it: change their password, or sign out. */
@Component({
  selector: 'app-user-menu',
  imports: [Menu, Icon, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-menu align="end" [ariaLabel]="'Account menu for ' + session.displayName()">
      <span menuTrigger class="user-trigger">
        <span class="avatar" aria-hidden="true">{{ initials() }}</span>
        <span class="user-trigger__name">{{ session.displayName() }}</span>
      </span>
      <div class="menu__header">
        <p class="menu__title">{{ session.displayName() }}</p>
        <p class="menu__meta">{{ session.roleLabels().join(', ') }}</p>
        <p class="menu__meta">{{ session.organizationName() }}</p>
      </div>
      @if (!minimal()) {
        <a class="menu__item" routerLink="/change-password">
          <app-icon name="lock" [size]="18" />
          Change password
        </a>
      }
      <button
        type="button"
        class="menu__item"
        [attr.aria-busy]="signingOut() ? 'true' : null"
        (click)="signOut()"
      >
        <app-icon name="logout" [size]="18" />
        Sign out
      </button>
    </app-menu>
  `,
  styles: `
    .user-trigger {
      display: inline-flex;
      align-items: center;
      gap: var(--space-2);
    }

    .avatar {
      display: inline-grid;
      place-items: center;
      width: 28px;
      height: 28px;
      border-radius: 50%;
      background: var(--color-brand);
      color: var(--color-on-brand);
      font-size: var(--text-xs);
      font-weight: 700;
    }

    .user-trigger__name {
      display: none;
      max-width: 14rem;
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }

    @media (min-width: 40rem) {
      .user-trigger__name {
        display: inline;
      }
    }
  `,
})
export class UserMenu {
  protected readonly session = inject(SessionState);
  private readonly router = inject(Router);
  private readonly toast = inject(ToastService);

  /** While true only "Sign out" is offered: the person must choose a new password first. */
  readonly minimal = input(false);

  protected readonly signingOut = signal(false);

  protected readonly initials = computed(() =>
    this.session
      .displayName()
      .split(/\s+/)
      .filter(Boolean)
      .slice(0, 2)
      .map((word) => word[0]!.toUpperCase())
      .join(''),
  );

  protected async signOut(): Promise<void> {
    if (this.signingOut()) return;
    this.signingOut.set(true);
    try {
      await this.session.signOut();
      await this.router.navigateByUrl(SIGN_IN_URL);
    } catch {
      this.toast.error("We couldn't sign you out. Check your connection and try again.");
    } finally {
      this.signingOut.set(false);
    }
  }
}
