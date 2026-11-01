import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { Health } from '../costing.models';
import { barWidthPercent } from '../figures';
import { healthOf } from '../health';

/**
 * A thin bar showing how much of a budget is used. It only repeats what the percentage beside it says (the bar is hidden from
 * screen readers), and its colour repeats the health chip, so nothing depends on seeing it. Money spent where nothing was
 * budgeted fills the whole bar.
 */
@Component({
  selector: 'app-used-bar',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <span class="bar" aria-hidden="true">
      <span
        class="bar__fill"
        [class]="'bar__fill bar__fill--' + tone()"
        [style.width.%]="width()"
      ></span>
    </span>
  `,
  styles: `
    :host {
      display: block;
      min-width: 3rem;
    }

    .bar {
      display: block;
      height: 6px;
      border: 1px solid var(--color-border-strong);
      border-radius: var(--radius-pill);
      background: var(--color-surface-2);
      overflow: clip;
    }

    .bar__fill {
      display: block;
      height: 100%;
      background: var(--color-text-muted);
    }

    .bar__fill--OnTrack {
      background: var(--health-on-track);
    }

    .bar__fill--NearLimit {
      background: var(--health-near-limit);
    }

    .bar__fill--OverBudget {
      background: var(--health-over-budget);
    }

    .bar__fill--Unbudgeted {
      background: var(--health-unbudgeted);
    }
  `,
})
export class UsedBar {
  readonly usedBps = input<number | null>(null);
  readonly health = input<Health | string>('NoActivity');

  protected readonly tone = computed(() => healthOf(this.health()));
  protected readonly width = computed(() =>
    this.tone() === 'Unbudgeted' ? 100 : barWidthPercent(this.usedBps()),
  );
}
