import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { ApiError, asApiError } from '../../core/api/api-error';
import { ToastService } from '../../core/notifications/toast-service';
import { SessionState } from '../../core/session/session-state';
import { todayIn } from '../../shared/presentation/business-time';
import { ApiErrorAlert } from '../../shared/ui/api-error-alert/api-error-alert';
import { Button } from '../../shared/ui/button/button';
import { FieldGroup } from '../../shared/ui/form-field/field-group';
import { PageHeader } from '../../shared/ui/page-header/page-header';
import { newRequestId } from '../workforce/request-id';
import { PayrollApi } from './payroll-api';

/** The years offered: this year (an early payout is allowed at any time) and last year (paid in the new year). Never a future year. */
export function thirteenthMonthYears(today: string): readonly number[] {
  const year = Number(today.slice(0, 4));
  return [year, year - 1];
}

/**
 * Prepares the 13th-month pay (`/payroll/thirteenth-month`) for a calendar year. The server works it out from the year's approved
 * payrolls and leaves out workers already in a 13th-month payroll for that year; the result is a payroll draft opened on its own
 * page, approved and voided like any other. One request id per visit, so pressing twice makes one payroll.
 */
@Component({
  selector: 'app-thirteenth-month-page',
  imports: [ReactiveFormsModule, RouterLink, PageHeader, FieldGroup, Button, ApiErrorAlert],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-page-header
      title="13th-month pay"
      backLabel="All payroll runs"
      [backLink]="['/payroll']"
    />

    <div class="card stack stack--loose form-card">
      <p>
        Each worker gets a twelfth of the basic pay from the year's approved payrolls: regular hours
        at the plain rate. Overtime, premiums, night differential and holiday pay are not counted.
        No contributions or tax are taken; enter any deduction by hand.
      </p>
      <p class="text-muted text-small">
        Workers already in a 13th-month payroll for the year are left out.
      </p>

      @if (error(); as problem) {
        <app-api-error-alert [error]="problem" />
      }

      <form class="form" [formGroup]="form" (ngSubmit)="submit()">
        <app-field-group
          label="Year"
          [control]="form.controls.year"
          requiredMessage="Choose the year."
        >
          <div class="choice-list">
            @for (choice of years; track choice) {
              <label class="choice">
                <input type="radio" formControlName="year" [value]="choice" />
                <span class="choice__text"
                  ><span class="choice__label">{{ choice }}</span></span
                >
              </label>
            }
          </div>
        </app-field-group>

        <div class="form__actions form__actions--sticky">
          <a appButton routerLink="/payroll">Cancel</a>
          <button appButton variant="primary" type="submit" [busy]="saving()">
            Prepare 13th-month pay
          </button>
        </div>
      </form>
    </div>
  `,
})
export class ThirteenthMonthPage {
  private readonly api = inject(PayrollApi);
  private readonly session = inject(SessionState);
  private readonly router = inject(Router);
  private readonly toast = inject(ToastService);

  protected readonly years = thirteenthMonthYears(todayIn(this.session.timeZoneId()));
  protected readonly form = new FormGroup({
    year: new FormControl<number>(this.years[0] ?? new Date().getFullYear(), {
      nonNullable: true,
      validators: [Validators.required],
    }),
  });

  protected readonly saving = signal(false);
  protected readonly error = signal<ApiError | null>(null);

  /** One id for this visit to the page: a second press returns the same payroll, not another. */
  private readonly requestId = newRequestId();

  protected async submit(): Promise<void> {
    if (this.saving()) return;
    this.saving.set(true);
    this.error.set(null);
    try {
      const run = await firstValueFrom(
        this.api.createThirteenthMonth(this.form.controls.year.value, this.requestId),
      );
      this.toast.success('13th-month pay prepared.');
      await this.router.navigate(['/payroll', run.id]);
    } catch (failure) {
      this.error.set(asApiError(failure));
    } finally {
      this.saving.set(false);
    }
  }
}
