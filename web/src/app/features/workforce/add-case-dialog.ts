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
import { SessionState } from '../../core/session/session-state';
import { injectFocusFirstInvalid } from '../../shared/forms/server-errors';
import { todayIn } from '../../shared/presentation/business-time';
import { ApiErrorAlert } from '../../shared/ui/api-error-alert/api-error-alert';
import { Button } from '../../shared/ui/button/button';
import { Dialog } from '../../shared/ui/dialog/dialog';
import { Control } from '../../shared/ui/form-field/control';
import { FormField } from '../../shared/ui/form-field/form-field';
import { describeFormFailure } from './form-failure';
import { injectProjectChoices } from './project-choices';
import { WorkforceApi } from './workforce-api';
import { WorkerDetail } from './workforce.models';

/** Adds an incident or disciplinary note to a worker's record. Cases are added and never edited or removed. */
@Component({
  selector: 'app-add-case-dialog',
  imports: [ReactiveFormsModule, Dialog, Button, FormField, Control, ApiErrorAlert],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-dialog title="Add a case" [locked]="saving()" (closed)="dismissed.emit()">
      <div class="stack">
        <p>
          A case is a note about an incident for {{ worker().fullName }}. Once added it can't be
          edited or removed.
        </p>

        @if (error(); as problem) {
          <app-api-error-alert
            [error]="problem"
            [extra]="extra()"
            reloadLabel="Reload worker"
            (reload)="reloaded.emit()"
          />
        }

        <form id="case-form" class="form" [formGroup]="form" (ngSubmit)="submit()" #formElement>
          <app-form-field
            label="What happened"
            hint="A short title, for example Late three days in a row."
            [control]="form.controls.title"
            requiredMessage="Say what happened."
          >
            <input appControl formControlName="title" autocomplete="off" maxlength="200" />
          </app-form-field>
          <app-form-field label="More detail" [optional]="true" [control]="form.controls.remarks">
            <textarea appControl formControlName="remarks" rows="3" maxlength="1000"></textarea>
          </app-form-field>
          <app-form-field
            label="Date it happened"
            [control]="form.controls.caseDate"
            requiredMessage="Choose the date it happened."
          >
            <input
              appControl
              type="date"
              formControlName="caseDate"
              [min]="worker().dateHired"
              [max]="today"
            />
          </app-form-field>
          <app-form-field label="Project" [optional]="true" [control]="form.controls.projectId">
            <select appControl formControlName="projectId">
              <option value="">Not about one project</option>
              @for (project of choices.projects(); track project.id) {
                <option [value]="project.id">{{ project.name }}</option>
              }
            </select>
          </app-form-field>
        </form>
      </div>

      <div dialogActions class="dialog-actions">
        <button appButton type="button" [attr.disabled]="saving() ? '' : null" (click)="cancel()">
          Cancel
        </button>
        <button appButton variant="primary" type="submit" form="case-form" [busy]="saving()">
          Add case
        </button>
      </div>
    </app-dialog>
  `,
})
export class AddCaseDialog {
  readonly worker = input.required<WorkerDetail>();

  readonly added = output<WorkerDetail>();
  readonly dismissed = output<void>();
  readonly reloaded = output<void>();

  private readonly api = inject(WorkforceApi);
  private readonly session = inject(SessionState);
  private readonly dialog = viewChild.required(Dialog);
  private readonly formElement = viewChild<ElementRef<HTMLFormElement>>('formElement');
  private readonly focusInvalid = injectFocusFirstInvalid();
  protected readonly choices = injectProjectChoices();

  protected readonly today = todayIn(this.session.timeZoneId());

  protected readonly form = new FormGroup({
    title: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(200)],
    }),
    remarks: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(1000)] }),
    caseDate: new FormControl(this.today, { nonNullable: true, validators: [Validators.required] }),
    projectId: new FormControl('', { nonNullable: true }),
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
        this.api.addCase(this.worker().id, {
          title: value.title.trim(),
          remarks: value.remarks.trim() || null,
          caseDate: value.caseDate,
          projectId: value.projectId || null,
          version: this.worker().version,
        }),
      );
      this.added.emit(updated);
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
