import { DestroyRef, ElementRef, Signal, afterNextRender, inject, signal } from '@angular/core';

/**
 * Whether the element this is called from (a component's host) is at least `minRem` wide, kept up to date as it is resized.
 * A table needs more room than a phone-sized or tablet-sized column gives it even when the screen is wide (the side rail
 * takes a quarter of a laptop screen), so a list asks how much room it really has before it chooses a table over cards.
 *
 * It assumes there is room until it has measured, and wherever nothing can be measured (a test, a hidden tab), so the wide
 * layout is the one drawn first.
 */
export function injectMinWidth(minRem: number): Signal<boolean> {
  const host = inject<ElementRef<HTMLElement>>(ElementRef).nativeElement;
  const destroyRef = inject(DestroyRef);
  const enough = signal(true);

  afterNextRender(() => {
    const rootSize = parseFloat(getComputedStyle(document.documentElement).fontSize) || 16;
    const limit = minRem * rootSize;
    const measure = () => {
      const width = host.clientWidth;
      if (width > 0) enough.set(width >= limit);
    };
    measure();
    if (typeof ResizeObserver === 'undefined') return;
    const observer = new ResizeObserver(measure);
    observer.observe(host);
    destroyRef.onDestroy(() => observer.disconnect());
  });

  return enough.asReadonly();
}
