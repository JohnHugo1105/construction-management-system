import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { PayrollLine } from './payroll.models';

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
  readonly regularPay: string;
  readonly overtimePay: string;
  readonly deductionRows: readonly {
    readonly id: string;
    readonly description: string;
    readonly amount: string;
  }[];
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
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="detail">
      <section [attr.aria-label]="'How the pay of ' + item().name + ' is made up'">
        <h3 class="detail__title">Pay</h3>
        <dl class="detail__pairs">
          <div>
            <dt>Regular pay</dt>
            <dd class="money">{{ item().regularPay }}</dd>
          </div>
          <div>
            <dt>Overtime pay</dt>
            <dd class="money">{{ item().overtimePay }}</dd>
          </div>
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
                <span>{{ row.description }}</span>
                <span class="money">{{ row.amount }}</span>
              </li>
            }
          </ul>
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
      color: var(--color-text-muted);
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
      min-width: 0;
      overflow-wrap: anywhere;
    }
  `,
})
export class PayrollLineDetail {
  readonly item = input.required<LineView>();
}
