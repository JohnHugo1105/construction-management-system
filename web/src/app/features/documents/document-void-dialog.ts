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
import { DocumentsApi } from './documents-api';
import { DocumentListItem, ProjectDocument } from './documents.models';

/** Voids a document with a reason. It is never deleted: it stays on the list as voided, and its files and links still open. */
@Component({
  selector: 'app-document-void-dialog',
  imports: [ReactiveFormsModule, Dialog, Button, FormField, Control, ApiErrorAlert],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-dialog title="Void this document?" [locked]="saving()" (closed)="dismissed.emit()">
      <div class="stack">
        <p>
          Use this when {{ document().title }} was added by mistake or is no longer in use. It stays on
          the list as voided, with your reason, and its files can still be downloaded.
        </p>
        @if (error(); as problem) {
          <app-api-error-alert
            [error]="problem"
            [extra]="extra()"
            [allowRetry]="problem.status === 409"
            reloadLabel="Reload documents"
            (reload)="reloaded.emit()"
          />
        }
        <form id="document-void-form" class="form" [formGroup]="form" (ngSubmit)="submit()" #formElement>
          <app-form-field
            label="Why is it being voided?"
            [control]="form.controls.reason"
            requiredMessage="Give a reason."
          >
            <textarea appControl formControlName="reason" rows="3" maxlength="300" data-autofocus></textarea>
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
        <button appButton variant="danger" type="submit" form="document-void-form" [busy]="saving()">
          Void document
        </button>
      </div>
    </app-dialog>
  `,
})
export class DocumentVoidDialog {
  readonly document = input.required<DocumentListItem>();

  readonly done = output<ProjectDocument>();
  readonly dismissed = output<void>();
  readonly reloaded = output<void>();

  private readonly api = inject(DocumentsApi);
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

  protected async submit(): Promise<void> {
    if (this.saving()) return;
    this.form.markAllAsTouched();
    const reason = this.form.controls.reason.value.trim();
    if (this.form.invalid || !reason) {
      if (!reason) this.form.controls.reason.setErrors({ required: true });
      this.focusInvalid(this.formElement()?.nativeElement);
      return;
    }
    const document = this.document();
    this.saving.set(true);
    this.error.set(null);
    this.extra.set([]);
    try {
      this.done.emit(await firstValueFrom(this.api.voidDocument(document.id, reason, document.version)));
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
