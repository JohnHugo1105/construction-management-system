import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  afterNextRender,
  inject,
  input,
  signal,
  viewChild,
} from '@angular/core';

/**
 * The labelled, keyboard-focusable box a wide table scrolls sideways in (the shared `.table-wrap`). A short "Scroll sideways"
 * hint shows under it only while the table really is wider than the box, so a table that fits carries no extra words.
 */
@Component({
  selector: 'app-scroll-region',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="table-wrap" role="region" tabindex="0" [attr.aria-label]="label()" #region>
      <ng-content />
    </div>
    @if (overflowing()) {
      <p class="scroll-hint">Scroll sideways to see more columns.</p>
    }
  `,
  styles: `
    :host {
      display: block;
    }

    .scroll-hint {
      margin-top: var(--space-2);
      font-size: var(--text-sm);
      color: var(--color-text-muted);
    }
  `,
})
export class ScrollRegion {
  /** What the region holds, read out when it takes focus ("Budget lines"). */
  readonly label = input.required<string>();

  protected readonly overflowing = signal(false);
  private readonly region = viewChild.required<ElementRef<HTMLElement>>('region');

  constructor() {
    const destroyRef = inject(DestroyRef);
    afterNextRender(() => {
      const element = this.region().nativeElement;
      const measure = () => this.overflowing.set(element.scrollWidth > element.clientWidth + 1);
      measure();
      if (typeof ResizeObserver === 'undefined') return;
      const observer = new ResizeObserver(measure);
      observer.observe(element);
      for (const child of Array.from(element.children)) observer.observe(child);
      destroyRef.onDestroy(() => observer.disconnect());
    });
  }
}
