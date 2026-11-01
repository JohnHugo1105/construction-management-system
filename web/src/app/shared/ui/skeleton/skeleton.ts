import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

/** A calm placeholder while content loads: pulsing blocks (no shimmer gradient), announced once as "Loading". */
@Component({
  selector: 'app-skeleton',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="skeleton" role="status">
      <span class="visually-hidden">{{ label() }}</span>
      @for (row of rows(); track row) {
        <div
          class="skeleton__row"
          [class.skeleton__row--card]="variant() === 'card'"
          aria-hidden="true"
        ></div>
      }
    </div>
  `,
  styles: `
    :host {
      display: block;
    }

    .skeleton {
      display: grid;
      gap: var(--space-3);
    }

    .skeleton__row {
      height: 1.25rem;
      border-radius: var(--radius-sm);
      background: var(--color-surface-2);
      animation: skeleton-pulse 1.4s ease-in-out infinite;
    }

    .skeleton__row--card {
      height: 5.5rem;
      border-radius: var(--radius-lg);
    }

    .skeleton__row:nth-child(even) {
      width: 80%;
    }

    @keyframes skeleton-pulse {
      50% {
        opacity: 0.45;
      }
    }
  `,
})
export class Skeleton {
  readonly lines = input(3);
  readonly variant = input<'text' | 'card'>('text');
  readonly label = input('Loading');

  protected readonly rows = computed(() =>
    Array.from({ length: this.lines() }, (_, index) => index),
  );
}
