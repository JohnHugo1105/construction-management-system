import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { formatDate } from '../../shared/presentation/business-time';
import { formatMoney } from '../../shared/presentation/money';
import { StatusChip } from '../../shared/ui/status-chip/status-chip';
import { Viewport } from '../../shared/ui/viewport/viewport';
import { bondLook, bondTitle, directionWords } from './bonds-presentation';
import { BondListItem } from './bonds.models';

/**
 * Bond rows: a table on wide screens and cards on phones. Each says which bond it is, who gave it to whom, the amount, the date it is
 * valid until and a chip for where it stands. Every figure and state is the server's.
 */
@Component({
  selector: 'app-bond-rows',
  imports: [RouterLink, StatusChip],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (viewport.isTablet()) {
      <div class="table-wrap" role="region" aria-label="Bonds" tabindex="0">
        <table class="data-table">
          <thead>
            <tr>
              <th scope="col">Bond</th>
              <th scope="col" class="num">Amount</th>
              <th scope="col">Valid until</th>
              <th scope="col">Status</th>
            </tr>
          </thead>
          <tbody>
            @for (row of rows(); track row.id) {
              <tr>
                <th scope="row">
                  <a class="row-link" [routerLink]="['/bonds', row.id]">{{ row.heading }}</a>
                  <span class="cell-sub">{{ row.sub }}</span>
                </th>
                <td class="num">{{ row.amount }}</td>
                <td>{{ row.validUntil }}</td>
                <td>
                  <app-status-chip
                    [label]="row.look.label"
                    [tone]="row.look.tone"
                    [icon]="row.look.icon"
                  />
                </td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    } @else {
      <ul class="card-list">
        @for (row of rows(); track row.id) {
          <li class="row-card">
            <div class="row-card__head">
              <a
                class="row-card__title row-link row-link--stretch"
                [routerLink]="['/bonds', row.id]"
                >{{ row.heading }}</a
              >
              <app-status-chip
                [label]="row.look.label"
                [tone]="row.look.tone"
                [icon]="row.look.icon"
              />
            </div>
            <p class="text-small text-muted">{{ row.sub }}</p>
            <p class="text-small">
              <span class="money">{{ row.amount }}</span> · valid until {{ row.validUntil }}
            </p>
          </li>
        }
      </ul>
    }
  `,
})
export class BondRows {
  readonly items = input.required<readonly BondListItem[]>();
  /** Name the project on each row (the register); a project's own card leaves it out. */
  readonly showProject = input(true);

  protected readonly viewport = inject(Viewport);

  protected readonly rows = computed(() =>
    this.items().map((item) => {
      const who = directionWords(item);
      return {
        id: item.id,
        heading: `${bondTitle(item)} · ${item.issuerName}`,
        sub: this.showProject() ? `${who} · ${item.projectName}` : who,
        amount: formatMoney(item.amountMinor, item.currencyCode),
        validUntil: formatDate(item.validUntil),
        look: bondLook(item),
      };
    }),
  );
}
