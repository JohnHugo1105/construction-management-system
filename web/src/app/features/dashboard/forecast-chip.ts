import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { SessionState } from '../../core/session/session-state';
import { formatMoney } from '../../shared/presentation/money';
import { Icon } from '../../shared/ui/icon/icon';

/**
 * "Forecast ₱11,040,912.96 over": the project is spent within its budget so far, but the work done says it will finish over it. A link to
 * that project's Progress tab, where the same figure is explained. It has an icon and words, never colour alone, and a 44 px target.
 * The caller shows it only when the forecast is over budget, so there is never an empty chip.
 */
@Component({
  selector: 'app-forecast-chip',
  imports: [RouterLink, Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <a
      class="forecast-chip"
      [routerLink]="['/projects', projectId(), 'progress']"
      [attr.aria-label]="name()"
    >
      <span class="chip chip--warning">
        <app-icon name="warning" [size]="14" />
        {{ label() }}
      </span>
    </a>
  `,
  styles: `
    :host {
      display: inline-block;
    }

    .forecast-chip {
      display: inline-flex;
      align-items: center;
      min-height: var(--touch-target);
      text-decoration: none;
    }

    .forecast-chip:hover .chip {
      text-decoration: underline;
    }

    /* The chip sits under the project's name, where the committed lines already make the column wide, so it never widens the table. On a narrow
       card the words may wrap instead of running off the edge. */
    .chip {
      max-width: 100%;
      white-space: normal;
      text-align: left;
      line-height: 1.25;
    }
  `,
})
export class ForecastChip {
  readonly projectId = input.required<string>();
  /** How far over the budget the forecast is, in minor units (above zero). */
  readonly overMinor = input.required<number>();

  private readonly session = inject(SessionState);

  protected readonly label = computed(
    () => `Forecast ${formatMoney(this.overMinor(), this.session.currencyCode())} over`,
  );

  /** The visible words come first so a person who says them aloud reaches the link, then what it is and where it goes. */
  protected readonly name = computed(
    () => `${this.label()}. Forecast to finish over budget: see progress`,
  );
}
