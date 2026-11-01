import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import {
  AbstractControl,
  FormControl,
  FormGroup,
  ReactiveFormsModule,
  ValidationErrors,
  Validators,
} from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { ApiError, asApiError } from '../../core/api/api-error';
import { ToastService } from '../../core/notifications/toast-service';
import { SessionState } from '../../core/session/session-state';
import { applyServerErrors, injectFocusFirstInvalid } from '../../shared/forms/server-errors';
import { todayIn } from '../../shared/presentation/business-time';
import { Alert } from '../../shared/ui/alert/alert';
import { ApiErrorAlert } from '../../shared/ui/api-error-alert/api-error-alert';
import { Button } from '../../shared/ui/button/button';
import { Control } from '../../shared/ui/form-field/control';
import { FormField } from '../../shared/ui/form-field/form-field';
import { PageHeader } from '../../shared/ui/page-header/page-header';
import { newRequestId } from '../workforce/request-id';
import { PayrollApi } from './payroll-api';
import { checkPeriod, defaultPeriod } from './payroll-rules';

/** Checks the pair of days against each other as soon as either changes (the same rules the server applies). */
function periodDay(which: 'start' | 'end', today: () => string) {
  return (control: AbstractControl): ValidationErrors | null => {
    const group = control.parent;
    if (!group) return null;
    const problems = checkPeriod(
      String(group.get('periodStart')?.value ?? ''),
      String(group.get('periodEnd')?.value ?? ''),
      today(),
    );
    const message = which === 'start' ? problems.start : problems.end;
    return message ? { message } : null;
  };
}

/**
 * Creates a payroll run (`/payroll/new`) for a pay period. The period starts as the 14 days that ended yesterday. Creating
 * takes every recorded attendance in the period that hasn't been paid and locks it to the run. The form carries one request id
 * for the visit, so pressing the button twice makes one run.
 */
@Component({
  selector: 'app-payroll-new-page',
  imports: [
    ReactiveFormsModule,
    RouterLink,
    PageHeader,
    FormField,
    Control,
    Button,
    Alert,
    ApiErrorAlert,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-page-header
      title="New payroll run"
      backLabel="All payroll runs"
      [backLink]="['/payroll']"
    />

    <div class="card stack stack--loose form-card">
      <p>
        Takes all attendance that hasn't been paid and locks it. Locked attendance can't be changed
        until the payroll run is voided.
      </p>

      @if (error(); as problem) {
        <app-api-error-alert [error]="problem" [extra]="extraMessages()" />
      } @else if (showSummary()) {
        <app-alert tone="danger" title="Some details need your attention"
          >Check the highlighted fields and try again.</app-alert
        >
      }

      <form class="form" [formGroup]="form" (ngSubmit)="submit()" #formElement>
        <div class="form__row">
          <app-form-field
            label="First day"
            [control]="form.controls.periodStart"
            requiredMessage="Choose the first day of the pay period."
          >
            <input appControl type="date" formControlName="periodStart" [max]="today" />
          </app-form-field>
          <app-form-field
            label="Last day"
            hint="Up to today. A pay period is at most 31 days."
            [control]="form.controls.periodEnd"
            requiredMessage="Choose the last day of the pay period."
          >
            <input appControl type="date" formControlName="periodEnd" [max]="today" />
          </app-form-field>
        </div>

        <div class="form__actions form__actions--sticky">
          <a appButton routerLink="/payroll">Cancel</a>
          <button appButton variant="primary" type="submit" [busy]="saving()">
            Create payroll run
          </button>
        </div>
      </form>
    </div>
  `,
})
export class PayrollNewPage {
  private readonly api = inject(PayrollApi);
  private readonly session = inject(SessionState);
  private readonly router = inject(Router);
  private readonly toast = inject(ToastService);

  protected readonly today = todayIn(this.session.timeZoneId());

  protected readonly form = new FormGroup({
    periodStart: new FormControl(defaultPeriod(this.today).start, {
      nonNullable: true,
      validators: [Validators.required, periodDay('start', () => this.today)],
    }),
    periodEnd: new FormControl(defaultPeriod(this.today).end, {
      nonNullable: true,
      validators: [Validators.required, periodDay('end', () => this.today)],
    }),
  });

  protected readonly saving = signal(false);
  protected readonly error = signal<ApiError | null>(null);
  protected readonly extraMessages = signal<readonly string[]>([]);
  protected readonly showSummary = signal(false);

  private readonly formElement = viewChild<ElementRef<HTMLFormElement>>('formElement');
  private readonly focusInvalid = injectFocusFirstInvalid();

  /** One id for this visit to the form: a second press makes the same run, not another. */
  private readonly requestId = newRequestId();

  constructor() {
    // Each day is checked against the other, so check both whenever either changes. The change is announced, so a message
    // already on screen follows (a "last day" error goes away as soon as the first day is moved). A guard stops the two from
    // answering each other forever.
    const destroyed = inject(DestroyRef);
    let checking = false;
    const recheck = (other: AbstractControl): void => {
      if (checking) return;
      checking = true;
      other.updateValueAndValidity();
      checking = false;
    };
    this.form.controls.periodStart.valueChanges
      .pipe(takeUntilDestroyed(destroyed))
      .subscribe(() => recheck(this.form.controls.periodEnd));
    this.form.controls.periodEnd.valueChanges
      .pipe(takeUntilDestroyed(destroyed))
      .subscribe(() => recheck(this.form.controls.periodStart));
  }

  protected async submit(): Promise<void> {
    if (this.saving()) return;
    this.form.markAllAsTouched();
    if (this.form.invalid) {
      this.showSummary.set(true);
      this.focusInvalid(this.formElement()?.nativeElement);
      return;
    }

    const { periodStart, periodEnd } = this.form.getRawValue();
    this.saving.set(true);
    this.showSummary.set(false);
    this.error.set(null);
    this.extraMessages.set([]);
    try {
      const run = await firstValueFrom(
        this.api.create({ periodStart, periodEnd, clientRequestId: this.requestId }),
      );
      this.toast.success('Payroll run created.');
      await this.router.navigate(['/payroll', run.id]);
    } catch (failure) {
      const problem = asApiError(failure);
      this.error.set(problem);
      this.extraMessages.set(applyServerErrors(this.form, problem));
      this.focusInvalid(this.formElement()?.nativeElement);
    } finally {
      this.saving.set(false);
    }
  }
}
