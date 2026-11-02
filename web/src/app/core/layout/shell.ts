import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  computed,
  effect,
  inject,
  signal,
  untracked,
  viewChild,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { distinctUntilChanged, filter, map } from 'rxjs';
import { Dialog } from '../../shared/ui/dialog/dialog';
import { Icon } from '../../shared/ui/icon/icon';
import { Viewport } from '../../shared/ui/viewport/viewport';
import { ToastHost } from '../../shared/ui/toast-host/toast-host';
import { NeedsActionState } from '../../features/needs-action/needs-action-state';
import { NotificationBell } from '../../features/collaboration/notification-bell';
import { SessionState } from '../session/session-state';
import { NAV_ITEMS, NavItem, splitForBottomBar, visibleNavItems } from './navigation';
import { PageTitleState } from './page-title-state';
import { UserMenu } from './user-menu';

/**
 * The frame around every signed-in page: a navigation rail on a wide screen or a bottom bar on a phone, a compact header with
 * the page title and the user menu, and the page itself. While a new password is required it stays minimal (no navigation), so
 * the person can only do the one thing asked of them. The "Needs action" entry carries a badge with how many things wait on the
 * person: read once when the shell starts and again when the browser tab becomes visible (no timer).
 */
@Component({
  selector: 'app-shell',
  imports: [
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    Icon,
    Dialog,
    UserMenu,
    ToastHost,
    NotificationBell,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './shell.html',
  styleUrl: './shell.scss',
})
export class Shell {
  protected readonly session = inject(SessionState);
  protected readonly viewport = inject(Viewport);
  protected readonly pageTitle = inject(PageTitleState);
  protected readonly waiting = inject(NeedsActionState);
  private readonly router = inject(Router);

  private readonly main = viewChild.required<ElementRef<HTMLElement>>('main');

  protected readonly minimal = computed(() => this.session.mustChangePassword());
  protected readonly items = computed(() =>
    visibleNavItems(NAV_ITEMS, (...p) => this.session.hasAnyPermission(...p)),
  );
  protected readonly bottomBar = computed(() => splitForBottomBar(this.items()));
  protected readonly moreOpen = signal(false);

  constructor() {
    // The badge's first read, once the navigation shows (not while a new password is required).
    effect(() => {
      if (!this.minimal() && this.waiting.applies()) untracked(() => this.waiting.refresh());
    });
    this.waiting.refreshWhenVisible(inject(DestroyRef));

    // After moving to another page, start the page at its content so keyboard and screen reader users are not left behind.
    this.router.events
      .pipe(
        filter((event): event is NavigationEnd => event instanceof NavigationEnd),
        map((event) => event.urlAfterRedirects.split(/[?#]/)[0]),
        distinctUntilChanged(),
        takeUntilDestroyed(),
      )
      .subscribe(() => this.main().nativeElement.focus({ preventScroll: true }));
  }

  /** "Needs action, 6 waiting" for the entry with the badge while something waits; otherwise its visible words are its name. */
  protected accessibleName(item: NavItem): string | null {
    const count = this.waiting.count();
    return item.showsWaitingCount && count > 0 ? `${item.label}, ${count} waiting` : null;
  }

  protected skipToContent(event: Event): void {
    event.preventDefault();
    this.main().nativeElement.focus();
  }
}
