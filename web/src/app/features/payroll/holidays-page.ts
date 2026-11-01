import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { firstValueFrom } from 'rxjs';
import { ApiError, asApiError } from '../../core/api/api-error';
import { ToastService } from '../../core/notifications/toast-service';
import { Permission } from '../../core/session/permissions';
import { SessionState } from '../../core/session/session-state';
import { injectListQuery, listField } from '../../shared/list-query/list-query';
import { formatDate, todayIn } from '../../shared/presentation/business-time';
import { ApiErrorAlert } from '../../shared/ui/api-error-alert/api-error-alert';
import { Button } from '../../shared/ui/button/button';
import { Dialog } from '../../shared/ui/dialog/dialog';
import { EmptyState } from '../../shared/ui/empty-state/empty-state';
import { Icon } from '../../shared/ui/icon/icon';
import { PageHeader } from '../../shared/ui/page-header/page-header';
import { Skeleton } from '../../shared/ui/skeleton/skeleton';
import { StatusChip } from '../../shared/ui/status-chip/status-chip';
import { HolidayDialog } from './holiday-dialog';
import { PayRulesApi } from './pay-rules-api';
import { CompanyHoliday } from './payroll.models';
import { HOLIDAY_TYPE_LABEL } from './premium-rules';

/** The year in the address (`?year=2027`), or the company's current year. */
export function holidayYear(raw: string, today: string): number {
  return /^\d{4}$/.test(raw) ? Number(raw) : Number(today.slice(0, 4));
}

/**
 * The company's holiday calendar for one year (`/payroll/holidays?year=`). Nothing is built in: people who keep the pay rules
 * (payrollrules.manage) add the year's proclaimed holidays, and everyone who reads payroll sees them. A holiday an approved payroll
 * covers shows why it can't be changed instead of offering the change.
 */
@Component({
  selector: 'app-holidays-page',
  imports: [
    PageHeader,
    Button,
    Icon,
    StatusChip,
    EmptyState,
    Skeleton,
    ApiErrorAlert,
    Dialog,
    HolidayDialog,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-page-header title="Holidays" backLabel="Payroll" [backLink]="['/payroll']" />

    <div class="stack">
      <div class="year">
        <button appButton variant="ghost" type="button" (click)="setYear(year() - 1)">
          <app-icon name="chevron-left" [size]="18" /> {{ year() - 1 }}
        </button>
        <h2 class="year__title">{{ year() }}</h2>
        <button appButton variant="ghost" type="button" (click)="setYear(year() + 1)">
          {{ year() + 1 }} <app-icon name="chevron-right" [size]="18" />
        </button>
      </div>

      @if (loadError(); as problem) {
        <app-api-error-alert
          [error]="problem"
          [allowRetry]="true"
          reloadLabel="Try again"
          (reload)="resource.reload()"
        />
      } @else if (resource.isLoading() && !resource.hasValue()) {
        <app-skeleton [lines]="4" label="Loading holidays" />
      } @else if (rows().length > 0) {
        @if (canManage()) {
          <div class="toolbar">
            <button appButton variant="primary" type="button" (click)="adding.set(true)">
              <app-icon name="plus" [size]="18" /> Add holiday
            </button>
          </div>
        }
        <ul class="card-list">
          @for (row of rows(); track row.holiday.id) {
            <li class="row-card">
              <div class="row-card__head">
                <div>
                  <p class="row-card__title">{{ row.holiday.name }}</p>
                  <p class="text-muted text-small">{{ row.date }}</p>
                </div>
                <app-status-chip
                  [label]="row.type"
                  [tone]="row.holiday.type === 'Regular' ? 'warning' : 'info'"
                  icon="calendar"
                />
              </div>
              @if (row.holiday.coveredByApprovedPayroll) {
                <p class="text-muted text-small row-card__note">
                  An approved payroll covers this day. Void it before changing the holiday.
                </p>
              }
              @if (canManage()) {
                <div class="row-card__actions">
                  <button appButton type="button" (click)="editing.set(row.holiday)">
                    {{ row.holiday.coveredByApprovedPayroll ? 'Rename' : 'Change' }}
                  </button>
                  @if (!row.holiday.coveredByApprovedPayroll) {
                    <button
                      appButton
                      variant="ghost"
                      type="button"
                      (click)="removing.set(row.holiday)"
                    >
                      Remove
                    </button>
                  }
                </div>
              }
            </li>
          }
        </ul>
      } @else {
        <app-empty-state
          icon="calendar"
          [title]="'No holidays entered for ' + year()"
          [message]="
            canManage()
              ? 'Add the regular holidays and special days proclaimed for this year, so payroll pays them.'
              : 'Someone who keeps the pay rules can add this year\\'s holidays.'
          "
        >
          @if (canManage()) {
            <button appButton variant="primary" type="button" (click)="adding.set(true)">
              <app-icon name="plus" [size]="18" /> Add holiday
            </button>
          }
        </app-empty-state>
      }
    </div>

    @if (adding()) {
      <app-holiday-dialog
        (saved)="saved($event, 'Holiday added.')"
        (dismissed)="adding.set(false)"
        (reloaded)="reload()"
      />
    }
    @if (editing(); as holiday) {
      <app-holiday-dialog
        [holiday]="holiday"
        (saved)="saved($event, 'Holiday saved.')"
        (dismissed)="editing.set(null)"
        (reloaded)="reload()"
      />
    }
    @if (removing(); as holiday) {
      <app-dialog title="Remove this holiday?" [locked]="busy()" (closed)="removing.set(null)">
        <div class="stack">
          <p>
            {{ holiday.name }}, {{ dateLabel(holiday.date) }}, will be paid as an ordinary day in
            payrolls worked out from now on.
          </p>
          @if (removeError(); as problem) {
            <app-api-error-alert
              [error]="problem"
              [allowRetry]="problem.status === 409"
              reloadLabel="Reload holidays"
              (reload)="reload()"
            />
          }
        </div>
        <div dialogActions class="dialog-actions">
          <button
            appButton
            type="button"
            [attr.disabled]="busy() ? '' : null"
            (click)="removing.set(null)"
          >
            Cancel
          </button>
          <button
            appButton
            variant="danger"
            type="button"
            [busy]="busy()"
            (click)="remove(holiday)"
          >
            Remove holiday
          </button>
        </div>
      </app-dialog>
    }
  `,
  styles: `
    .year {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: var(--space-2);
    }

    .year__title {
      font-size: var(--text-lg);
    }

    .toolbar {
      display: flex;
      justify-content: flex-end;
    }

    .row-card__actions {
      display: flex;
      flex-wrap: wrap;
      gap: var(--space-2);
    }
  `,
})
export class HolidaysPage {
  private readonly api = inject(PayRulesApi);
  private readonly session = inject(SessionState);
  private readonly toast = inject(ToastService);

  protected readonly canManage = this.session.permit(Permission.payrollRulesManage);
  private readonly query = injectListQuery({ year: listField.text() });
  protected readonly year = computed(() =>
    holidayYear(this.query.values().year, todayIn(this.session.timeZoneId())),
  );

  protected readonly resource = rxResource({
    params: () => ({ year: this.year() }),
    stream: ({ params }) => this.api.holidays(params.year),
  });
  protected readonly loadError = computed(() => {
    const failure = this.resource.error();
    return failure ? asApiError(failure) : null;
  });
  protected readonly rows = computed(() =>
    (this.resource.hasValue() ? this.resource.value().holidays : []).map((holiday) => ({
      holiday,
      date: formatDate(holiday.date),
      type: HOLIDAY_TYPE_LABEL[holiday.type],
    })),
  );

  protected readonly adding = signal(false);
  protected readonly editing = signal<CompanyHoliday | null>(null);
  protected readonly removing = signal<CompanyHoliday | null>(null);
  protected readonly busy = signal(false);
  protected readonly removeError = signal<ApiError | null>(null);

  protected setYear(year: number): void {
    this.query.set({ year: String(year) });
  }

  protected dateLabel(date: string): string {
    return formatDate(date);
  }

  protected saved(holiday: CompanyHoliday, message: string): void {
    this.adding.set(false);
    this.editing.set(null);
    this.toast.success(message);
    const year = Number(holiday.date.slice(0, 4));
    if (year !== this.year()) this.setYear(year);
    else this.resource.reload();
  }

  protected async remove(holiday: CompanyHoliday): Promise<void> {
    if (this.busy()) return;
    this.busy.set(true);
    this.removeError.set(null);
    try {
      await firstValueFrom(this.api.removeHoliday(holiday.id, holiday.version));
      this.removing.set(null);
      this.toast.success('Holiday removed.');
      this.resource.reload();
    } catch (failure) {
      this.removeError.set(asApiError(failure));
    } finally {
      this.busy.set(false);
    }
  }

  protected reload(): void {
    this.adding.set(false);
    this.editing.set(null);
    this.removing.set(null);
    this.removeError.set(null);
    this.resource.reload();
  }
}
