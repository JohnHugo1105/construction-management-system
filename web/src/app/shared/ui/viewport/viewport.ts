import { Injectable, Signal, signal } from '@angular/core';

/**
 * Which layout the screen is wide enough for, as signals. A list renders a table or cards (never both), and the shell renders a
 * navigation rail or a bottom bar (never both), so screen readers meet each landmark once. The breakpoints match the CSS:
 * 768px and 1024px. Where the browser cannot report it (tests), the wide layouts are assumed.
 */
@Injectable({ providedIn: 'root' })
export class Viewport {
  /** Wide enough for a table instead of cards (768px and up). */
  readonly isTablet: Signal<boolean> = this.watch('(min-width: 48rem)');

  /** Wide enough for the left navigation rail instead of the bottom bar (1024px and up). */
  readonly isDesktop: Signal<boolean> = this.watch('(min-width: 64rem)');

  private watch(query: string): Signal<boolean> {
    const media = typeof matchMedia === 'function' ? matchMedia(query) : null;
    const matches = signal(media?.matches ?? true);
    media?.addEventListener('change', (event) => matches.set(event.matches));
    return matches.asReadonly();
  }
}
