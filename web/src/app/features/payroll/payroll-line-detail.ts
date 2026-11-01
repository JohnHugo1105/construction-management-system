import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { Button } from '../../shared/ui/button/button';
import { PayrollDeduction, PayrollLine } from './payroll.models';
import { BreakdownRow } from './premium-rules';

/** One worker's line of a payroll with every figure and phrase already worked out for display. */
export interface LineView {
  readonly line: PayrollLine;
  readonly id: string;
  readonly name: string;
  readonly sub: string;
  readonly hours: string;
  readonly gross: string;
  readonly deductions: string;
  readonly net: string;
  /** How the gross is made up (regular, overtime and premium, night, holiday pay), parts that are zero left out. */
  readonly breakdown: readonly BreakdownRow[];
  readonly deductionRows: readonly {
    readonly id: string;
    readonly description: string;
    readonly amount: string;
    /** "SSS, calculated", "PhilHealth, adjusted"; null for a deduction typed by hand. */
    readonly source: string | null;
    readonly reason: string | null;
    readonly deduction: PayrollDeduction;
  }[];
  /** What the employer adds to the worker's contributions; null when nothing. */
  readonly employerShares: string | null;
  /** A calculated amount was cut so the deductions don't pass the pay. */
  readonly limited: boolean;
  readonly shares: readonly {
    readonly key: string;
    readonly place: string;
    readonly hours: string;
    readonly amount: string;
  }[];
}

/** What is behind a worker's pay: how it is made up, what is deducted, and which projects it is charged to. */
@Component({
  selector: 'app-payroll-line-detail',
  imports: [Button],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="detail">
      <section [attr.aria-label]="'How the pay of ' + item().name + ' is made up'">
        <h3 class="detail__title">Pay</h3>
        <dl class="detail__pairs">
          @for (part of item().breakdown; track part.label) {
            <div>
              <dt>
                {{ part.label }}
                @if (part.note) {
                  <span class="detail__sub text-small">{{ part.note }}</span>
                }
              </dt>
              <dd class="money">{{ part.amount }}</dd>
            </div>
          }
        </dl>
      </section>

      <section [attr.aria-label]="'Deductions of ' + item().name">
        <h3 class="detail__title">Deductions</h3>
        @if (item().deductionRows.length === 0) {
          <p class="text-muted text-small">None.</p>
        } @else {
          <ul class="detail__list">
            @for (row of item().deductionRows; track row.id) {
              <li>
                <span class="detail__place">
                  {{ row.description }}
                  @if (row.source) {
                    <span class="text-muted text-small">{{ row.source }}</span>
                  }
                  @if (row.reason) {
                    <span class="text-muted text-small">Reason: {{ row.reason }}</span>
                  }
                  @if (canAdjust() && row.source) {
                    <button
                      appButton
                      variant="ghost"
                      type="button"
                      class="detail__adjust"
                      (click)="adjust.emit(row.deduction)"
                    >
                      Adjust<span class="visually-hidden"> {{ row.description }}</span>
                    </button>
                  }
                </span>
                <span class="money">{{ row.amount }}</span>
              </li>
            }
          </ul>
        }
        @if (item().limited) {
          <p class="text-small detail__note">Deductions were limited to the pay.</p>
        }
        @if (item().employerShares; as employer) {
          <p class="text-muted text-small detail__note">
            Employer adds <span class="money">{{ employer }}</span> (not taken from pay).
          </p>
        }
      </section>

      <section [attr.aria-label]="'Projects charged for ' + item().name">
        <h3 class="detail__title">Charged to</h3>
        <ul class="detail__list">
          @for (share of item().shares; track share.key) {
            <li>
              <span class="detail__place">
                {{ share.place }}
                <span class="text-muted text-small">{{ share.hours }}</span>
              </span>
              <span class="money">{{ share.amount }}</span>
            </li>
          }
        </ul>
      </section>
    </div>
  `,
  styles: `
    :host {
      display: block;
    }

    .detail {
      display: grid;
      gap: var(--space-4);
      padding: var(--space-3) 0 var(--space-1);
    }

    @media (min-width: 48rem) {
      .detail {
        grid-template-columns: repeat(3, minmax(0, 1fr));
        gap: var(--space-6);
        padding: var(--space-2) var(--space-2);
      }
    }

    .detail__title {
      margin-bottom: var(--space-2);
      color: var(--color-text-muted);
      font-size: var(--text-sm);
      font-weight: 650;
    }

    .detail__pairs {
      display: grid;
      gap: var(--space-2);
    }

    .detail__pairs > div,
    .detail__list li {
      display: flex;
      align-items: baseline;
      justify-content: space-between;
      gap: var(--space-3);
    }

    .detail__pairs dt {
      display: grid;
      color: var(--color-text-muted);
    }

    .detail__sub {
      overflow-wrap: anywhere;
    }

    .detail__pairs dd {
      font-weight: 600;
    }

    .detail__list {
      display: grid;
      gap: var(--space-2);
      margin: 0;
      padding: 0;
      list-style: none;
    }

    .detail__place {
      display: grid;
      justify-items: start;
      min-width: 0;
      overflow-wrap: anywhere;
    }

    .detail__note {
      margin-top: var(--space-2);
    }
  `,
})
export class PayrollLineDetail {
  readonly item = input.required<LineView>();
  /** Offer "Adjust" on calculated contributions (a draft, for someone who prepares payroll). */
  readonly canAdjust = input(false);
  readonly adjust = output<PayrollDeduction>();
}
