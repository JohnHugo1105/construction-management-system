import { DOCUMENT, DestroyRef, Injectable, computed, inject, signal } from '@angular/core';
import { Subscription } from 'rxjs';
import { NEEDS_ACTION_PERMISSIONS } from '../../core/session/permissions';
import { SessionState } from '../../core/session/session-state';
import { NeedsActionApi } from './needs-action-api';

/** The badge's text: nothing at 0, the number up to 99, then "99+". */
export function badgeText(total: number): string {
  if (total <= 0) return '';
  return total > 99 ? '99+' : String(total);
}

/** The shortest time between two refreshes caused by the tab coming back to the front. */
const MIN_FOCUS_REFRESH_MS = 30_000;

/**
 * How many things wait on the signed-in person, for the navigation badge. It is loaded once when the shell starts, again when the
 * browser tab becomes visible, and taken from the page whenever the page reads the full queue, so the badge and the page never
 * disagree. There is no timer: nothing polls.
 */
@Injectable({ providedIn: 'root' })
export class NeedsActionState {
  private readonly api = inject(NeedsActionApi);
  private readonly session = inject(SessionState);
  private readonly document = inject(DOCUMENT);

  /** The last number read, and whose it is, so a different person signing in never sees someone else's badge. */
  private readonly latest = signal<{ readonly userId: string; readonly total: number } | null>(
    null,
  );
  private request: Subscription | null = null;
  /** When the number was last asked for or set, so switching tabs back and forth does not ask again each time. */
  private lastReadAtMs = 0;

  /** True when the person holds a permission that can put something on the page. */
  readonly applies = computed(() => this.session.hasAnyPermission(...NEEDS_ACTION_PERMISSIONS));

  /** How many things wait on the signed-in person (0 until known). */
  readonly count = computed(() => {
    const latest = this.latest();
    const userId = this.session.session()?.userId;
    return latest && userId && latest.userId === userId && this.applies() ? latest.total : 0;
  });

  /** "6", "99+", or empty when nothing waits. */
  readonly badge = computed(() => badgeText(this.count()));

  /** Asks the server for the number. A failure keeps the last number: the badge is a hint, the page is the answer. */
  refresh(): void {
    const userId = this.session.session()?.userId;
    if (!userId || !this.applies()) return;
    this.lastReadAtMs = Date.now();
    this.request?.unsubscribe();
    this.request = this.api.count().subscribe({
      next: (result) => this.latest.set({ userId, total: result.total }),
      error: () => undefined,
    });
  }

  /** The page read the full queue: its total is the badge's, so the two always match. */
  setTotal(total: number): void {
    const userId = this.session.session()?.userId;
    if (!userId) return;
    this.lastReadAtMs = Date.now();
    this.request?.unsubscribe();
    this.request = null;
    this.latest.set({ userId, total });
  }

  /** Called once by the shell: refreshes whenever the browser tab becomes visible again, until the shell goes away. */
  refreshWhenVisible(destroyRef: DestroyRef): void {
    const listener = () => {
      // A tab that comes back to the front re-reads the number, but not more often than every 30 seconds: the badge is a hint.
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
