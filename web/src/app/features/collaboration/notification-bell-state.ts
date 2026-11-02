import { DOCUMENT, DestroyRef, Injectable, computed, inject, signal } from '@angular/core';
import { Subscription } from 'rxjs';
import { Permission } from '../../core/session/permissions';
import { SessionState } from '../../core/session/session-state';
import { CollaborationApi } from './collaboration-api';
import { unreadBadge } from './collaboration-presentation';

/** The shortest time between two refreshes caused by the tab coming back to the front. */
const MIN_FOCUS_REFRESH_MS = 30_000;

/**
 * How many of the signed-in person's notifications are unread, for the bell in the header. It is read once when the shell starts, again
 * when the browser tab becomes visible, and taken from the Notifications page whenever that page marks something read, so the bell and the
 * page never disagree. There is no timer: nothing polls. The bell says what happened; "Needs your action" says what waits on you.
 */
@Injectable({ providedIn: 'root' })
export class NotificationBellState {
  private readonly api = inject(CollaborationApi);
  private readonly session = inject(SessionState);
  private readonly document = inject(DOCUMENT);

  /** The last number read, and whose it is, so a different person signing in never sees someone else's count. */
  private readonly latest = signal<{ readonly userId: string; readonly unread: number } | null>(
    null,
  );
  private request: Subscription | null = null;
  private lastReadAtMs = 0;

  readonly applies = computed(() => this.session.can(Permission.notificationsRead));

  /** How many are unread (0 until known). */
  readonly unread = computed(() => {
    const latest = this.latest();
    const userId = this.session.session()?.userId;
    return latest && userId && latest.userId === userId && this.applies() ? latest.unread : 0;
  });

  /** "3", "99+", or empty when nothing is unread. */
  readonly badge = computed(() => unreadBadge(this.unread()));

  /** Asks the server for the number. A failure keeps the last number: the bell is a hint, the page is the answer. */
  refresh(): void {
    const userId = this.session.session()?.userId;
    if (!userId || !this.applies()) return;
    this.lastReadAtMs = Date.now();
    this.request?.unsubscribe();
    this.request = this.api.unreadCount().subscribe({
      next: (result) => this.latest.set({ userId, unread: result.unread }),
      error: () => undefined,
    });
  }

  /** The server answered a "mark read" with the new number: it is the bell's, so the two always match. */
  setUnread(unread: number): void {
    const userId = this.session.session()?.userId;
    if (!userId) return;
    this.lastReadAtMs = Date.now();
    this.request?.unsubscribe();
    this.request = null;
    this.latest.set({ userId, unread: Math.max(0, unread) });
  }

  /**
   * Marks one notification read on the way to its record. The server answers with the new count, which the bell takes. A failure changes
   * nothing: the person still reaches the record, and the notification stays unread.
   */
  markRead(id: string): void {
    this.api.markRead(id).subscribe({
      next: (result) => this.setUnread(result.unread),
      error: () => undefined,
    });
  }

  /** Called once by the bell: refreshes whenever the browser tab becomes visible again, until the bell goes away. */
  refreshWhenVisible(destroyRef: DestroyRef): void {
    const listener = () => {
      if (
        this.document.visibilityState === 'visible' &&
        Date.now() - this.lastReadAtMs >= MIN_FOCUS_REFRESH_MS
      )
        this.refresh();
    };
    this.document.addEventListener('visibilitychange', listener);
    destroyRef.onDestroy(() => {
      this.document.removeEventListener('visibilitychange', listener);
      this.request?.unsubscribe();
      this.request = null;
    });
  }
}
