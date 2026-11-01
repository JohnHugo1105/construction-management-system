import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { Button } from '../button/button';
import { Icon } from '../icon/icon';

/**
 * Previous and next for a list the server pages. It shows nothing while everything fits on one page. It only reports the
 * wanted page; the page owns the query (see ListQuery) and loads the rows.
 */
@Component({
  selector: 'app-pager',
  imports: [Button, Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (pageCount() > 1) {
      <nav class="pager" aria-label="Pages">
        <p class="pager__summary">
          Showing {{ first() }} to {{ last() }} of {{ total() }} {{ noun() }}
        </p>
        <div class="pager__buttons">
          <button
            appButton
            type="button"
            [attr.disabled]="page() <= 1 ? '' : null"
            (click)="go(page() - 1)"
          >
            <app-icon name="chevron-left" [size]="18" />
            Previous
          </button>
          <span class="pager__position" aria-current="page"
            >Page {{ page() }} of {{ pageCount() }}</span
          >
          <button
            appButton
            type="button"
            [attr.disabled]="page() >= pageCount() ? '' : null"
            (click)="go(page() + 1)"
          >
            Next
            <app-icon name="chevron-right" [size]="18" />
          </button>
        </div>
      </nav>
    }
  `,
  styles: `
    .pager {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      justify-content: space-between;
      gap: var(--space-3);
      margin-top: var(--space-4);
    }

    .pager__summary {
      font-size: var(--text-sm);
      color: var(--color-text-muted);
    }

    .pager__buttons {
      display: flex;
      align-items: center;
      gap: var(--space-3);
    }

    .pager__position {
      font-size: var(--text-sm);
      white-space: nowrap;
    }

    @media (max-width: 47.99rem) {
      .pager {
        justify-content: center;
      }

      .pager__summary {
        flex-basis: 100%;
        text-align: center;
      }
    }
  `,
})
export class Pager {
  readonly page = input.required<number>();
  readonly pageSize = input.required<number>();
  readonly total = input.required<number>();
  /** What the rows are, for the summary line ("projects", "entries"). */
  readonly noun = input('results');

  readonly pageChange = output<number>();

  protected readonly pageCount = computed(() =>
    Math.max(1, Math.ceil(this.total() / this.pageSize())),
  );
  protected readonly first = computed(() =>
    this.total() === 0 ? 0 : (this.page() - 1) * this.pageSize() + 1,
  );
  protected readonly last = computed(() => Math.min(this.total(), this.page() * this.pageSize()));

  protected go(target: number): void {
    if (target >= 1 && target <= this.pageCount() && target !== this.page())
      this.pageChange.emit(target);
  }
}
