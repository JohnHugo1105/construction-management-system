import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  input,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Observable, firstValueFrom } from 'rxjs';
import { ApiError, asApiError } from '../../core/api/api-error';
import { injectFocusFirstInvalid } from '../../shared/forms/server-errors';
import { ApiErrorAlert } from '../../shared/ui/api-error-alert/api-error-alert';
import { Button } from '../../shared/ui/button/button';
import { Dialog } from '../../shared/ui/dialog/dialog';
import { Control } from '../../shared/ui/form-field/control';
import { FormField } from '../../shared/ui/form-field/form-field';
import { describeFormFailure } from '../workforce/form-failure';
import { Bill } from './billing.models';

let nextFormId = 0;

/**
 * Asks why before something is voided or discarded, then runs the action. A bill or a payment is never deleted: the reason stays
 * on record. The parent says what happens in `message` and passes the request to make with the reason.
 */
@Component({
  selector: 'app-reason-dialog',
  imports: [ReactiveFormsModule, Dialog, Button, FormField, Control, ApiErrorAlert],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-dialog [title]="title()" [locked]="saving()" (closed)="dismissed.emit()">
      <div class="stack">
        <p>{{ message() }}</p>

        @if (error(); as problem) {
          <app-api-error-alert
            [error]="problem"
            [extra]="extra()"
            [allowRetry]="problem.status === 409"
            reloadLabel="Reload bill"
            (reload)="reloaded.emit()"
          />
        }

        <form [id]="formId" class="form" [formGroup]="form" (ngSubmit)="submit()" #formElement>
          <app-form-field
            [label]="label()"
            [control]="form.controls.reason"
            requiredMessage="Say why, so the record explains itself later."
          >
            <textarea appControl formControlName="reason" rows="3" maxlength="300"></textarea>
          </app-form-field>
        </form>
      </div>

      <div dialogActions class="dialog-actions">
        <button appButton type="button" [attr.disabled]="saving() ? '' : null" (click)="cancel()">
          {{ keepLabel() }}
        </button>
        <button appButton variant="danger" type="submit" [attr.form]="formId" [busy]="saving()">
          {{ confirmLabel() }}
        </button>
      </div>
    </app-dialog>
  `,
})
export class ReasonDialog {
  readonly title = input.required<string>();
  readonly message = input.required<string>();
  readonly label = input('Why?');
  readonly confirmLabel = input.required<string>();
  readonly keepLabel = input('Keep it');
  /** The request to make with the reason the person typed. */
  readonly action = input.required<(reason: string) => Observable<Bill>>();

  readonly done = output<Bill>();
  readonly dismissed = output<void>();
  readonly reloaded = output<void>();

  private readonly dialog = viewChild.required(Dialog);
  private readonly formElement = viewChild<ElementRef<HTMLFormElement>>('formElement');
  private readonly focusInvalid = injectFocusFirstInvalid();

  protected readonly formId = `reason-form-${++nextFormId}`;
  protected readonly form = new FormGroup({
    reason: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(300)],
    }),
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
    this.saving.set(true);
    this.error.set(null);
    this.extra.set([]);
    try {
      this.done.emit(await firstValueFrom(this.action()(this.form.getRawValue().reason.trim())));
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
