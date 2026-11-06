import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { Skeleton } from '../../shared/ui/skeleton/skeleton';
import { dayWord, quantityText, workersWords } from './site-diary-presentation';
import { DiaryFilledIn } from './site-diary.models';

/**
 * The two read-only panels beside a report: who was on site that day (from the attendance already recorded) and what was delivered
 * (from the deliveries already received). Nothing in them is typed here and neither carries money. The names of the workers appear
 * only when the server sent them, which it does for people who may see the worker list.
 *
 *     <app-diary-filled-in [filledIn]="data" [date]="'2026-11-02'" [today]="today" />
 */
@Component({
  selector: 'app-diary-filled-in',
  imports: [Skeleton],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="panel" aria-labelledby="diary-on-site">
      <h3 id="diary-on-site" class="panel__title">On site {{ word() }}</h3>
      @if (filledIn(); as data) {
        @if (data.onSite.count > 0) {
          <p>{{ workers(data.onSite.count) }} with attendance recorded.</p>
          @if (data.onSite.names && data.onSite.names.length > 0) {
            <p class="text-small text-muted">{{ data.onSite.names.join(', ') }}</p>
          }
        } @else {
          <p class="text-muted">No attendance has been recorded for {{ dayName() }} yet.</p>
        }
      } @else {
        <app-skeleton [lines]="1" />
      }
    </section>

    <section class="panel" aria-labelledby="diary-delivered">
      <h3 id="diary-delivered" class="panel__title">Delivered {{ word() }}</h3>
      @if (filledIn(); as data) {
        @if (data.deliveries.length > 0) {
          <ul class="deliveries">
            @for (item of data.deliveries; track $index) {
              <li>
                <span class="deliveries__item">{{ item.description }}</span>
                <span class="deliveries__qty">{{ quantity(item.quantity) }} {{ item.unit }}</span>
                <span class="deliveries__from text-small text-muted">{{ item.supplierName }}</span>
              </li>
            }
          </ul>
        } @else {
          <p class="text-muted">No deliveries were received {{ dayName() }}.</p>
        }
      } @else {
        <app-skeleton [lines]="1" />
      }
    </section>

    <p class="text-small text-muted">
      These come from the attendance and deliveries already recorded. There is nothing to type here.
    </p>
  `,
  styles: `
    :host {
      display: grid;
      gap: var(--space-3);
    }

    .panel {
      display: grid;
      gap: var(--space-2);
      padding: var(--space-3) var(--space-4);
      border: 1px solid var(--color-border);
      border-radius: var(--radius-md);
      background: var(--color-surface-2);
    }

    .panel__title {
      font-size: var(--text-base);
    }

    .deliveries {
      display: grid;
      gap: var(--space-2);
      margin: 0;
      padding: 0;
      list-style: none;
    }

    .deliveries li {
      display: grid;
      grid-template-columns: 1fr auto;
      gap: 0 var(--space-3);
    }

    .deliveries__item {
      min-width: 0;
      overflow-wrap: anywhere;
    }

    .deliveries__qty {
      font-weight: 600;
      white-space: nowrap;
    }

    .deliveries__from {
      grid-column: 1 / -1;
    }
  `,
})
export class DiaryFilledInPanels {
  /** What was read for the day; undefined while it loads. */
  readonly filledIn = input<DiaryFilledIn | undefined>(undefined);
  /** The report's day, "2026-11-02". */
  readonly date = input.required<string>();
  /** Today where the company works, "2026-11-02". */
  readonly today = input.required<string>();

  protected readonly word = computed(() => dayWord(this.date(), this.today()));
  protected readonly dayName = computed(() => (this.word() === 'today' ? 'today' : 'that day'));
  protected readonly workers = workersWords;
  protected readonly quantity = quantityText;
}
