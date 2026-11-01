import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  inject,
  input,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { ApiError, asApiError } from '../../core/api/api-error';
import { injectFocusFirstInvalid } from '../../shared/forms/server-errors';
import { formatDate } from '../../shared/presentation/business-time';
import { ApiErrorAlert } from '../../shared/ui/api-error-alert/api-error-alert';
import { Button } from '../../shared/ui/button/button';
import { Dialog } from '../../shared/ui/dialog/dialog';
import { Control } from '../../shared/ui/form-field/control';
import { FormField } from '../../shared/ui/form-field/form-field';
import { describeFormFailure } from './form-failure';
import { WorkforceApi } from './workforce-api';
import { AttendanceEntry } from './workforce.models';

/**
 * Removes one attendance entry, with a reason. The entry stays on record and is not paid; the worker reappears on the sheet so the
 * day can be recorded again. An entry a payroll run has taken is refused by the API, and its message is shown here.
 */
@Component({
  selector: 'app-void-attendance-dialog',
  imports: [ReactiveFormsModule, Dialog, Button, FormField, Control, ApiErrorAlert],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-dialog title="Remove this entry?" [locked]="saving()" (closed)="dismissed.emit()">
      <div class="stack">
        <p>
          {{ entry().workerName }}'s hours for {{ day() }} will be removed. They stay on record, are
          not paid, and the day can be recorded again.
        </p>

        @if (error(); as problem) {
          <app-api-error-alert
            [error]="problem"
            [extra]="extra()"
            reloadLabel="Reload the day"
            (reload)="reloaded.emit()"
          />
        }

        <form
          id="void-attendance-form"
          class="form"
          [formGroup]="form"
          (ngSubmit)="submit()"
          #formElement
        >
          <app-form-field
            label="Why is it being removed?"
            [control]="form.controls.reason"
            requiredMessage="Say why the entry is being removed."
          >
            <textarea appControl formControlName="reason" rows="3" maxlength="300"></textarea>
          </app-form-field>
        </form>
      </div>

      <div dialogActions class="dialog-actions">
        <button appButton type="button" [attr.disabled]="saving() ? '' : null" (click)="cancel()">
          Keep entry
        </button>
        <button
          appButton
          variant="danger"
          type="submit"
          form="void-attendance-form"
          [busy]="saving()"
        >
          Remove entry
        </button>
      </div>
    </app-dialog>
  `,
})
export class VoidAttendanceDialog {
  readonly entry = input.required<AttendanceEntry>();

  readonly voided = output<void>();
  readonly dismissed = output<void>();
  readonly reloaded = output<void>();

  private readonly api = inject(WorkforceApi);
  private readonly dialog = viewChild.required(Dialog);
  private readonly formElement = viewChild<ElementRef<HTMLFormElement>>('formElement');
  private readonly focusInvalid = injectFocusFirstInvalid();

  protected readonly form = new FormGroup({
    reason: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(300)],
    }),
  });

  protected readonly saving = signal(false);
  protected readonly error = signal<ApiError | null>(null);
  protected readonly extra = signal<readonly string[]>([]);

  protected day(): string {
    return formatDate(this.entry().workDate);
  }

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

    this.saving.set(true);
    this.error.set(null);
    this.extra.set([]);
    try {
      await firstValueFrom(
        this.api.voidAttendance(this.entry().id, {
          reason: this.form.getRawValue().reason.trim(),
          version: this.entry().version,
        }),
      );
      this.voided.emit();
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
