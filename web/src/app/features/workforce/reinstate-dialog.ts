import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  computed,
  inject,
  input,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { ApiError, asApiError } from '../../core/api/api-error';
import { SessionState } from '../../core/session/session-state';
import { injectFocusFirstInvalid } from '../../shared/forms/server-errors';
import { formatDate, todayIn } from '../../shared/presentation/business-time';
import { ApiErrorAlert } from '../../shared/ui/api-error-alert/api-error-alert';
import { Button } from '../../shared/ui/button/button';
import { Dialog } from '../../shared/ui/dialog/dialog';
import { Control } from '../../shared/ui/form-field/control';
import { FormField } from '../../shared/ui/form-field/form-field';
import { describeFormFailure } from './form-failure';
import { WORKER_STATUS } from './worker-status';
import { WorkforceApi } from './workforce-api';
import { WorkerDetail } from './workforce.models';

/** Brings back a worker who left (resigned, retired or terminated). The only way back is through a reason on record. */
@Component({
  selector: 'app-reinstate-dialog',
  imports: [ReactiveFormsModule, Dialog, Button, FormField, Control, ApiErrorAlert],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-dialog title="Reinstate this worker?" [locked]="saving()" (closed)="dismissed.emit()">
      <div class="stack">
        <p>
          {{ worker().fullName }} {{ leftSentence() }} Reinstating makes them active again. You can
          then post them to a project.
        </p>

        @if (error(); as problem) {
          <app-api-error-alert
            [error]="problem"
            [extra]="extra()"
            reloadLabel="Reload worker"
            (reload)="reloaded.emit()"
          />
        }

        <form
          id="reinstate-form"
          class="form"
          [formGroup]="form"
          (ngSubmit)="submit()"
          #formElement
        >
          <app-form-field
            label="Effective date"
            hint="After the day they left, and not in the future."
            [control]="form.controls.effectiveDate"
            requiredMessage="Choose the date they come back."
          >
            <input
              appControl
              type="date"
              formControlName="effectiveDate"
              [min]="worker().statusChangedOn ?? worker().dateHired"
              [max]="today"
            />
          </app-form-field>
          <app-form-field
            label="Reason"
            [control]="form.controls.reason"
            requiredMessage="Say why the worker is being reinstated."
          >
            <textarea appControl formControlName="reason" rows="3" maxlength="300"></textarea>
          </app-form-field>
        </form>
      </div>

      <div dialogActions class="dialog-actions">
        <button appButton type="button" [attr.disabled]="saving() ? '' : null" (click)="cancel()">
          Cancel
        </button>
        <button appButton variant="primary" type="submit" form="reinstate-form" [busy]="saving()">
          Reinstate worker
        </button>
      </div>
    </app-dialog>
  `,
})
export class ReinstateDialog {
  readonly worker = input.required<WorkerDetail>();

  readonly reinstated = output<WorkerDetail>();
  readonly dismissed = output<void>();
  readonly reloaded = output<void>();

  private readonly api = inject(WorkforceApi);
  private readonly session = inject(SessionState);
  private readonly dialog = viewChild.required(Dialog);
  private readonly formElement = viewChild<ElementRef<HTMLFormElement>>('formElement');
  private readonly focusInvalid = injectFocusFirstInvalid();

  protected readonly today = todayIn(this.session.timeZoneId());

  protected readonly form = new FormGroup({
    effectiveDate: new FormControl(this.today, {
      nonNullable: true,
      validators: [Validators.required],
    }),
    reason: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(300)],
    }),
  });

  protected readonly leftSentence = computed(() => {
    const worker = this.worker();
    const state = WORKER_STATUS[worker.status].label.toLowerCase();
    return worker.statusChangedOn
      ? `${state} on ${formatDate(worker.statusChangedOn)}.`
      : `has ${state}.`;
  });

  protected readonly saving = signal(false);
  protected readonly error = signal<ApiError | null>(null);
  protected readonly extra = signal<readonly string[]>([]);

  protected cancel(): void {
    this.dialog().dismiss();
  }

  protected async submit(): Promise<void> {
    if (this.saving()) return;
    this.form.markAllAsTouched();
    if (this.form.invalid) {
      this.focusInvalid(this.formElement()?.nativeElement);
      return;
    }

    const value = this.form.getRawValue();
    this.saving.set(true);
    this.error.set(null);
    this.extra.set([]);
    try {
      const updated = await firstValueFrom(
        this.api.reinstate(this.worker().id, {
          effectiveDate: value.effectiveDate,
          reason: value.reason.trim(),
          version: this.worker().version,
        }),
      );
      this.reinstated.emit(updated);
    } catch (failure) {
      const outcome = describeFormFailure(this.form, asApiError(failure));
      this.error.set(outcome.alert);
      this.extra.set(outcome.extra);
      this.focusInvalid(this.formElement()?.nativeElement);
    } finally {
      this.saving.set(false);
    }
  }
}
