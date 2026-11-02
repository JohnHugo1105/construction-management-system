import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { formatDate } from '../../shared/presentation/business-time';
import { formatMoney } from '../../shared/presentation/money';
import { injectMinWidth } from '../costing/ui/container-width';
import { ScrollRegion } from '../costing/ui/scroll-region';
import { curveGeometry, curveSummary } from './schedule-presentation';
import { ScheduleCurve, ScheduleCurvePoint } from './schedule.models';

/**
 * The S-curve drawn in the app as SVG (no chart library): planned, baseline, earned and actual cost over time, with a line for
 * today. Each series has its own line style and legend sample, so colour is never the only way to tell them apart. A sentence
 * says what the chart shows, and the same figures are one tap away as a table, for screen readers and for anyone who wants the
 * numbers. Every figure is the server's; this only draws them.
 */
@Component({
  selector: 'app-s-curve-chart',
  imports: [ScrollRegion],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <p class="curve__summary">{{ summary() }}</p>

    @if (geometry(); as chart) {
      <ul class="curve__legend" aria-hidden="true">
        @for (series of chart.series; track series.key) {
          <li class="curve__key">
            <svg width="28" height="10" viewBox="0 0 28 10">
              <line
                x1="1"
                y1="5"
                x2="27"
                y2="5"
                [class]="'curve__line curve__line--' + series.key"
              />
            </svg>
            {{ series.label }}
          </li>
        }
        @if (chart.todayX !== null) {
          <li class="curve__key">
            <svg width="28" height="10" viewBox="0 0 28 10">
              <line x1="14" y1="0" x2="14" y2="10" class="curve__today" />
            </svg>
            Today
          </li>
        }
      </ul>

      <svg
        class="curve__chart"
        [attr.viewBox]="'0 0 ' + chart.width + ' ' + chart.height"
        role="img"
        [attr.aria-label]="'S-curve chart. ' + summary()"
        preserveAspectRatio="xMidYMid meet"
      >
        @for (tick of chart.yTicks; track tick.at) {
          <line
            class="curve__grid"
            [attr.x1]="chart.plot.left"
            [attr.x2]="chart.plot.right"
            [attr.y1]="tick.at"
            [attr.y2]="tick.at"
          />
          <text
            class="curve__label"
            [attr.x]="chart.plot.left - 6"
            [attr.y]="tick.at + 4"
            text-anchor="end"
          >
            {{ tick.label }}
          </text>
        }
        @for (tick of chart.xTicks; track $index) {
          <text
            class="curve__label"
            [attr.x]="tick.at"
            [attr.y]="chart.plot.bottom + 20"
            [attr.text-anchor]="$first ? 'start' : $last ? 'end' : 'middle'"
          >
            {{ tick.label }}
          </text>
        }
        @if (chart.todayX !== null) {
          <line
            class="curve__today"
            [attr.x1]="chart.todayX"
            [attr.x2]="chart.todayX"
            [attr.y1]="chart.plot.top"
            [attr.y2]="chart.plot.bottom"
          />
        }
        @for (series of chart.series; track series.key) {
          <polyline
            [class]="'curve__line curve__line--' + series.key"
            [attr.points]="series.points"
          />
        }
      </svg>
    }

    @if (curve().points.length > 0) {
      <details class="curve__table">
        <summary>Show the figures as a table</summary>
        <app-scroll-region label="S-curve figures">
          <table class="data-table">
            <caption class="visually-hidden">
              Planned, earned and actual cost by week
            </caption>
            <thead>
              <tr>
                <th scope="col">Date</th>
                <th scope="col" class="num">Planned</th>
                @if (curve().hasBaseline) {
                  <th scope="col" class="num">Baseline</th>
                }
                <th scope="col" class="num">Earned</th>
                <th scope="col" class="num">Actual cost</th>
              </tr>
            </thead>
            <tbody>
              @for (point of curve().points; track point.date) {
                <tr>
                  <th scope="row">{{ dateOf(point) }}</th>
                  <td class="num">{{ money(point.plannedMinor) }}</td>
                  @if (curve().hasBaseline) {
                    <td class="num">{{ money(point.baselinePlannedMinor) }}</td>
                  }
                  <td class="num">{{ money(point.earnedMinor) }}</td>
                  <td class="num">{{ money(point.actualMinor) }}</td>
                </tr>
              }
            </tbody>
          </table>
        </app-scroll-region>
      </details>
    }
  `,
  styles: `
    :host {
      display: grid;
      gap: var(--space-3);
      min-width: 0;
    }

    .curve__summary {
      margin: 0;
    }

    .curve__legend {
      display: flex;
      flex-wrap: wrap;
      gap: var(--space-2) var(--space-4);
      margin: 0;
      padding: 0;
      list-style: none;
      font-size: var(--text-sm);
    }

    .curve__key {
      display: inline-flex;
      align-items: center;
      gap: var(--space-2);
    }

    .curve__chart {
      display: block;
      width: 100%;
      max-width: 56rem;
      height: auto;
    }

    .curve__grid {
      stroke: var(--color-border);
      stroke-width: 1;
    }

    .curve__label {
      fill: var(--color-text-muted);
      font-size: 11px;
    }

    .curve__today {
      stroke: var(--color-text);
      stroke-width: 1.5;
      stroke-dasharray: 2 3;
    }

    .curve__line {
      fill: none;
      stroke-width: 2.5;
      stroke-linejoin: round;
      stroke-linecap: round;
    }

    /* Each series differs in line style as well as colour. */
    .curve__line--planned {
      stroke: var(--color-brand);
    }

    .curve__line--baseline {
      stroke: var(--color-text-muted);
      stroke-dasharray: 8 5;
    }

    .curve__line--earned {
      stroke: var(--color-success);
      stroke-width: 4;
    }

    .curve__line--actual {
      stroke: var(--color-warning);
      stroke-dasharray: 1 5;
      stroke-width: 3.5;
    }

    .curve__table summary {
      display: flex;
      align-items: center;
      min-height: var(--touch-target);
      color: var(--color-brand-text);
      font-weight: 600;
      cursor: pointer;
    }

    .curve__table[open] summary {
      margin-bottom: var(--space-2);
    }
  `,
})
export class SCurveChart {
  readonly curve = input.required<ScheduleCurve>();
  readonly currency = input.required<string>();

  /** A phone-sized drawing on a narrow column, so its words stay readable; a wider one where there is room. */
  private readonly wide = injectMinWidth(36);

  protected readonly geometry = computed(() =>
    this.wide()
      ? curveGeometry(this.curve(), this.currency(), 640, 280)
      : curveGeometry(this.curve(), this.currency(), 360, 240),
  );

  protected readonly summary = computed(() => curveSummary(this.curve(), this.currency()));

  protected money(minor: number | null): string {
    return minor === null ? 'Not yet' : formatMoney(minor, this.currency());
  }

  protected dateOf(point: ScheduleCurvePoint): string {
    return point.isToday ? `${formatDate(point.date)} (today)` : formatDate(point.date);
  }
}
