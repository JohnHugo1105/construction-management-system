import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  inject,
  input,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import {
  AbstractControl,
  FormControl,
  FormGroup,
  ReactiveFormsModule,
  ValidationErrors,
  Validators,
} from '@angular/forms';
import { firstValueFrom, startWith } from 'rxjs';
import { ApiError, asApiError } from '../../core/api/api-error';
import { applyServerErrors, injectFocusFirstInvalid } from '../../shared/forms/server-errors';
import { ApiErrorAlert } from '../../shared/ui/api-error-alert/api-error-alert';
import { Button } from '../../shared/ui/button/button';
import { Dialog } from '../../shared/ui/dialog/dialog';
import { Control } from '../../shared/ui/form-field/control';
import { FieldGroup } from '../../shared/ui/form-field/field-group';
import { FormField } from '../../shared/ui/form-field/form-field';
import { ProjectsApi } from './projects-api';
import { ArchiveReason, Project } from './projects.models';

function noteRequiredForOther(control: AbstractControl): ValidationErrors | null {
  const reason = control.parent?.get('reason')?.value;
  return reason === 'Other' && String(control.value ?? '').trim() === ''
    ? { required: true }
    : null;
}

/**
 * Asks why a project is being archived and archives it. "Finished project" needs nothing more; "Other" needs a note. Neither
 * choice is pre-selected, so a leftover default can never archive a project for the wrong reason.
 */
@Component({
  selector: 'app-archive-project-dialog',
  imports: [ReactiveFormsModule, Dialog, Button, FieldGroup, FormField, Control, ApiErrorAlert],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-dialog title="Archive this project?" [locked]="saving()" (closed)="dismissed.emit()">
      <div class="stack">
        <p>
          {{ project().name }} will leave the project list and become read-only. It stays on record,
          and you can restore it at any time.
        </p>

        @if (error(); as problem) {
          <app-api-error-alert
            [error]="problem"
            reloadLabel="Reload project"
            (reload)="reloaded.emit()"
          />
        }

        <form
          id="archive-project-form"
          class="form"
          [formGroup]="form"
          (ngSubmit)="submit()"
          #formElement
        >
          <app-field-group
            label="Why is it being archived?"
            [control]="form.controls.reason"
            requiredMessage="Choose why the project is being archived."
          >
            <div class="choice-list">
              <label class="choice">
                <input type="radio" formControlName="reason" value="Finished" />
                <span class="choice__text"
                  ><span class="choice__label">Finished project</span></span
                >
              </label>
              <label class="choice">
                <input type="radio" formControlName="reason" value="Other" />
                <span class="choice__text"><span class="choice__label">Other</span></span>
              </label>
            </div>
          </app-field-group>

          @if (reason() === 'Other') {
            <app-form-field
              label="Say why"
              [control]="form.controls.note"
              requiredMessage="Say why the project is being archived."
            >
              <textarea appControl formControlName="note" rows="3" maxlength="500"></textarea>
            </app-form-field>
          }
        </form>
      </div>

      <div dialogActions class="dialog-actions">
        <button appButton type="button" [attr.disabled]="saving() ? '' : null" (click)="cancel()">
          Cancel
        </button>
        <button
          appButton
          variant="primary"
          type="submit"
          form="archive-project-form"
          [busy]="saving()"
        >
          Archive project
        </button>
      </div>
    </app-dialog>
  `,
})
export class ArchiveProjectDialog {
  readonly project = input.required<Project>();

  /** The project the API returned once it is archived. */
  readonly archived = output<Project>();
  /** The dialog was dismissed without archiving. */
  readonly dismissed = output<void>();
  /** The person chose to reload after someone else changed the project. */
  readonly reloaded = output<void>();

  private readonly api = inject(ProjectsApi);
  private readonly dialog = viewChild.required(Dialog);
  private readonly formElement = viewChild<ElementRef<HTMLFormElement>>('formElement');
  private readonly focusInvalid = injectFocusFirstInvalid();

  protected readonly form = new FormGroup({
    reason: new FormControl<ArchiveReason | null>(null, { validators: [Validators.required] }),
    note: new FormControl('', {
      nonNullable: true,
      validators: [noteRequiredForOther, Validators.maxLength(500)],
    }),
  });

  protected readonly reason = toSignal(
    this.form.controls.reason.valueChanges.pipe(startWith(this.form.controls.reason.value)),
    {
      initialValue: null,
    },
  );

  protected readonly saving = signal(false);
  protected readonly error = signal<ApiError | null>(null);

  constructor() {
    this.form.controls.reason.valueChanges
      .pipe(takeUntilDestroyed(inject(DestroyRef)))
      .subscribe(() => this.form.controls.note.updateValueAndValidity({ emitEvent: false }));
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

    const { reason, note } = this.form.getRawValue();
    this.saving.set(true);
    this.error.set(null);
    try {
      const updated = await firstValueFrom(
        this.api.archive(this.project().id, {
          reason: reason as ArchiveReason,
          note: reason === 'Other' ? note.trim() : null,
          version: this.project().version,
        }),
      );
      this.archived.emit(updated);
    } catch (failure) {
      const problem = asApiError(failure);
      const unmatched = applyServerErrors(this.form, problem);
      // A field message already sits under its field; anything else (a conflict, a rule) goes above the form.
      if (
        problem.isConflict ||
        problem.isRuleViolation ||
        unmatched.length > 0 ||
        problem.isServerFailure
      )
        this.error.set(problem);
      this.focusInvalid(this.formElement()?.nativeElement);
    } finally {
      this.saving.set(false);
    }
  }
}
