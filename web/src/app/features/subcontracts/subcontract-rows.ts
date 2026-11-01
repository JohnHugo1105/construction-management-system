import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { SessionState } from '../../core/session/session-state';
import { formatMoney } from '../../shared/presentation/money';
import { formatBps } from '../../shared/presentation/percent';
import { StatusChip } from '../../shared/ui/status-chip/status-chip';
import { Viewport } from '../../shared/ui/viewport/viewport';
import { SUBCONTRACT_STATUS, VAT_WORDS } from './subcontract-presentation';
import { SubcontractListItem } from './subcontracts.models';

/**
 * Subcontract rows: a table on wide screens and cards on phones. Each says who does the work, its state, how much of the revised sum
 * is certified, and what is still owed (only when something is). Every figure is the API's.
 */
@Component({
  selector: 'app-subcontract-rows',
  imports: [RouterLink, StatusChip],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (viewport.isTablet()) {
      <div class="table-wrap" role="region" aria-label="Subcontracts" tabindex="0">
        <table class="data-table">
          <thead>
            <tr>
              <th scope="col">Subcontract</th>
              <th scope="col">Status</th>
              <th scope="col" class="num">Revised sum</th>
              <th scope="col" class="num">Certified</th>
              <th scope="col" class="num">Owed</th>
            </tr>
          </thead>
          <tbody>
            @for (row of rows(); track row.id) {
              <tr>
                <th scope="row">
                  <a class="row-link" [routerLink]="['/subcontracts', row.id]">{{ row.heading }}</a>
                  <span class="cell-sub">{{ row.sub }}</span>
                </th>
                <td>
                  <app-status-chip [label]="row.status.label" [tone]="row.status.tone" [icon]="row.status.icon" />
                </td>
                <td class="num">
                  {{ row.revised }}
                  <span class="cell-sub">{{ row.vat }}</span>
                </td>
                <td class="num">
                  {{ row.certified }}
                  @if (row.percent) {
                    <span class="cell-sub">{{ row.percent }}</span>
                  }
                </td>
                <td class="num">{{ row.owed || '-' }}</td>
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
              <a class="row-card__title row-link row-link--stretch" [routerLink]="['/subcontracts', row.id]">{{
                row.heading
              }}</a>
              <app-status-chip [label]="row.status.label" [tone]="row.status.tone" [icon]="row.status.icon" />
            </div>
            <p class="text-small text-muted">{{ row.sub }}</p>
            <p class="text-small">
              Certified <span class="money">{{ row.certified }}</span> of
              <span class="money">{{ row.revised }}</span> {{ row.vat }}
              @if (row.percent) {
                ({{ row.percent }})
              }
            </p>
            @if (row.owed) {
              <p class="text-small">
                Owed <span class="money">{{ row.owed }}</span>
              </p>
            }
          </li>
        }
      </ul>
    }
  `,
})
export class SubcontractRows {
  readonly items = input.required<readonly SubcontractListItem[]>();
  /** Name the project on each row (the section list); a project's own tab leaves it out. */
  readonly showProject = input(true);

  protected readonly viewport = inject(Viewport);
  private readonly session = inject(SessionState);

  protected readonly rows = computed(() => {
    const currency = this.session.currencyCode();
    return this.items().map((item) => {
      const place = item.subProjectName ? `${item.projectName}, ${item.subProjectName}` : item.projectName;
      return {
        id: item.id,
        heading: `${item.number} ${item.title}`,
        sub: this.showProject() ? `${item.subcontractorName} · ${place}` : item.subcontractorName,
        status: SUBCONTRACT_STATUS[item.status],
        revised: formatMoney(item.revisedSumMinor, currency),
        vat: VAT_WORDS[item.vatTreatment],
        certified: formatMoney(item.certifiedWorkMinor, currency),
        percent: item.certifiedWorkMinor > 0 && item.percentCertifiedBps !== null ? formatBps(item.percentCertifiedBps) : '',
        owed: item.outstandingMinor > 0 ? formatMoney(item.outstandingMinor, currency) : '',
      };
    });
  });
}
