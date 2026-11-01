import { ChangeDetectionStrategy, Component, computed, inject, input, output, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { asApiError } from '../../../core/api/api-error';
import { SessionState } from '../../../core/session/session-state';
import { formatDate, formatDateTime } from '../../../shared/presentation/business-time';
import { formatBps } from '../../../shared/presentation/percent';
import { ApiErrorAlert } from '../../../shared/ui/api-error-alert/api-error-alert';
import { Button } from '../../../shared/ui/button/button';
import { Dialog } from '../../../shared/ui/dialog/dialog';
import { Pager } from '../../../shared/ui/pager/pager';
import { Skeleton } from '../../../shared/ui/skeleton/skeleton';
import { ProgressApi } from '../progress-api';

const PAGE_SIZE = 10;

/**
 * How one budget line's progress changed over time, newest first: the date, the percent, who said so and why. Read-only: progress
 * is never edited, a correction is a newer entry.
 */
@Component({
  selector: 'app-progress-history-dialog',
  imports: [Dialog, Button, Pager, Skeleton, ApiErrorAlert],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-dialog [title]="'History: ' + description()" (closed)="dismissed.emit()">
      <div class="stack">
        @if (error(); as problem) {
          <app-api-error-alert [error]="problem" [allowRetry]="true" reloadLabel="Try again" (reload)="resource.reload()" />
        }
        @if (page(); as result) {
          @if (result.items.length === 0) {
            <p class="text-muted">No progress has been recorded for this line yet.</p>
          } @else {
            <ol class="history">
              @for (item of items(); track item.id) {
                <li class="history__item">
                  <p class="history__main">
                    <strong>{{ item.percent }}</strong>
                    <span> on {{ item.date }}</span>
                  </p>
                  <p class="history__meta text-small text-muted">
                    {{ item.by }} · saved {{ item.saved }}
                  </p>
                  @if (item.note) {
                    <p class="history__note">{{ item.note }}</p>
                  }
                </li>
              }
            </ol>
            <app-pager
              [page]="pageNumber()"
              [pageSize]="pageSize"
              [total]="result.total"
              noun="updates"
              (pageChange)="pageNumber.set($event)"
            />
          }
        } @else if (!error()) {
          <app-skeleton variant="card" [lines]="3" label="Loading the history" />
        }
      </div>
      <div dialogActions class="dialog-actions">
        <button appButton type="button" (click)="dismissed.emit()">Close</button>
      </div>
    </app-dialog>
  `,
  styles: `
    .history {
      display: grid;
      gap: var(--space-3);
      margin: 0;
      padding: 0;
      list-style: none;
    }

    .history__item {
      padding-bottom: var(--space-3);
      border-bottom: 1px solid var(--color-border);
    }

    .history__item:last-child {
      border-bottom: 0;
      padding-bottom: 0;
    }

    .history__main,
    .history__meta,
    .history__note {
      margin: 0;
    }

    .history__note {
      margin-top: var(--space-1);
      overflow-wrap: anywhere;
    }
  `,
})
export class ProgressHistoryDialog {
  readonly lineId = input.required<string>();
  readonly description = input.required<string>();
  readonly dismissed = output<void>();

  private readonly api = inject(ProgressApi);
  private readonly session = inject(SessionState);

  protected readonly pageSize = PAGE_SIZE;
  protected readonly pageNumber = signal(1);

  protected readonly resource = rxResource({
    params: () => ({ id: this.lineId(), page: this.pageNumber() }),
    stream: ({ params }) => this.api.history(params.id, params.page, PAGE_SIZE),
  });

  protected readonly page = computed(() => (this.resource.hasValue() ? this.resource.value() : undefined));
  protected readonly error = computed(() => {
    const failure = this.resource.error();
    return failure ? asApiError(failure) : null;
  });

  protected readonly items = computed(() =>
    (this.page()?.items ?? []).map((item) => ({
      id: item.id,
      percent: formatBps(item.percentBps),
      date: formatDate(item.asOfDate),
      by: item.recordedBy ?? 'Someone',
      saved: formatDateTime(item.recordedAtUtc, this.session.timeZoneId()),
      note: item.note,
    })),
  );
}
