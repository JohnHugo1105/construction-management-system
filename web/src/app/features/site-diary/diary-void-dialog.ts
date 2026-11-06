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
import { ApiErrorAlert } from '../../shared/ui/api-error-alert/api-error-alert';
import { Button } from '../../shared/ui/button/button';
import { Dialog } from '../../shared/ui/dialog/dialog';
import { Control } from '../../shared/ui/form-field/control';
import { FormField } from '../../shared/ui/form-field/form-field';
import { describeFormFailure } from '../workforce/form-failure';
import { SiteDiaryApi } from './site-diary-api';
import { reportDayLabel } from './site-diary-presentation';
import { DiaryEntry } from './site-diary.models';

/**
 * Voids a report that was written by mistake. A reason is required. The report stays on record with the reason, leaves the list and
 * frees its day, so a new report can be written for it.
 */
@Component({
  selector: 'app-diary-void-dialog',
  imports: [ReactiveFormsModule, Dialog, Button, FormField, Control, ApiErrorAlert],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-dialog title="Void this report" [locked]="saving()" (closed)="dismissed.emit()">
      <div class="stack">
        <p>
          Void the report for {{ day() }}? It stays on record with your reason, and you can then write a new
          report for that day.
        </p>
        @if (error(); as problem) {
          <app-api-error-alert
            [error]="problem"
            [extra]="extra()"
            [allowRetry]="problem.status === 409"
            reloadLabel="Reload reports"
            (reload)="reloaded.emit()"
          />
        }
        <form id="diary-void-form" class="form" [formGroup]="form" (ngSubmit)="submit()" #formElement>
          <app-form-field
            label="Why is it being voided?"
            [control]="form.controls.reason"
            requiredMessage="Say why this report is being voided."
          >
            <textarea
              appControl
              formControlName="reason"
              rows="3"
              maxlength="300"
              data-autofocus
            ></textarea>
          </app-form-field>
        </form>
      </div>
      <div dialogActions class="dialog-actions">
        <button
          appButton
          type="button"
          [attr.disabled]="saving() ? '' : null"
          (click)="dialog().dismiss()"
        >
          Cancel
        </button>
        <button appButton variant="danger" type="submit" form="diary-void-form" [busy]="saving()">
          Void report
        </button>
      </div>
    </app-dialog>
  `,
})
export class DiaryVoidDialog {
  readonly projectId = input.required<string>();
  readonly entry = input.required<DiaryEntry>();

  readonly voided = output<DiaryEntry>();
  readonly dismissed = output<void>();
  readonly reloaded = output<void>();

  private readonly api = inject(SiteDiaryApi);
  protected readonly dialog = viewChild.required(Dialog);
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
    return reportDayLabel(this.entry().reportDate);
  }

  protected async submit(): Promise<void> {
    if (this.saving()) return;
    this.form.markAllAsTouched();
    if (this.form.invalid) {
      this.focusInvalid(this.formElement()?.nativeElement);
      return;
    }
    const entry = this.entry();
    this.saving.set(true);
    this.error.set(null);
    this.extra.set([]);
    try {
      const voided = await firstValueFrom(
        this.api.voidReport(
          this.projectId(),
          entry.reportDate,
          this.form.getRawValue().reason.trim(),
          entry.version,
        ),
      );
      this.voided.emit(voided);
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
