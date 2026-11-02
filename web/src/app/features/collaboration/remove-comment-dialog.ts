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
import { CollaborationApi } from './collaboration-api';
import { DiscussionComment } from './collaboration.models';

/**
 * Removes a comment posted by mistake. A comment is never edited or deleted: it is taken out of the discussion with a reason, and the
 * discussion then shows "Removed by ..." in its place. Only its author or an Administrator is offered this.
 */
@Component({
  selector: 'app-remove-comment-dialog',
  imports: [ReactiveFormsModule, Dialog, Button, FormField, Control, ApiErrorAlert],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-dialog title="Remove this comment?" [locked]="saving()" (closed)="dismissed.emit()">
      <div class="stack">
        <p>
          The comment will no longer be shown. The discussion says who removed it, and the reason
          stays on record. This can't be undone.
        </p>
        @if (error(); as problem) {
          <app-api-error-alert
            [error]="problem"
            [extra]="extra()"
            [allowRetry]="problem.status === 409"
            reloadLabel="Reload discussion"
            (reload)="reloaded.emit()"
          />
        }
        <form
          id="remove-comment-form"
          class="form"
          [formGroup]="form"
          (ngSubmit)="submit()"
          #formElement
        >
          <app-form-field
            label="Why are you removing it?"
            hint="For example, posted on the wrong record."
            [control]="form.controls.reason"
            requiredMessage="Give a reason."
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
          Keep comment
        </button>
        <button
          appButton
          variant="danger"
          type="submit"
          form="remove-comment-form"
          [busy]="saving()"
        >
          Remove comment
        </button>
      </div>
    </app-dialog>
  `,
})
export class RemoveCommentDialog {
  readonly comment = input.required<DiscussionComment>();

  readonly removed = output<DiscussionComment>();
  readonly dismissed = output<void>();
  readonly reloaded = output<void>();

  private readonly api = inject(CollaborationApi);
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
    const reason = this.form.controls.reason;
    reason.setValue(reason.value.trim());
    this.form.markAllAsTouched();
    if (this.form.invalid) {
      this.focusInvalid(this.formElement()?.nativeElement);
      return;
    }
    const comment = this.comment();
    this.saving.set(true);
    this.error.set(null);
    this.extra.set([]);
    try {
      this.removed.emit(
        await firstValueFrom(this.api.removeComment(comment.id, reason.value, comment.version)),
      );
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
