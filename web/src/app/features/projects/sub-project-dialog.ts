import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  OnInit,
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
import {
  applyServerErrors,
  injectFocusFirstInvalid,
  setServerError,
} from '../../shared/forms/server-errors';
import { todayIn } from '../../shared/presentation/business-time';
import { ApiErrorAlert } from '../../shared/ui/api-error-alert/api-error-alert';
import { Button } from '../../shared/ui/button/button';
import { Dialog } from '../../shared/ui/dialog/dialog';
import { Control } from '../../shared/ui/form-field/control';
import { FormField } from '../../shared/ui/form-field/form-field';
import { ProjectContext } from './project-context';
import { ProjectsApi } from './projects-api';
import { SubProject } from './projects.models';

/** Adds a sub-project to the open project, or edits one. A name already used in this project is shown under the name field. */
@Component({
  selector: 'app-sub-project-dialog',
  imports: [ReactiveFormsModule, Dialog, Button, FormField, Control, ApiErrorAlert],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-dialog [title]="title()" [locked]="saving()" (closed)="dismissed.emit()">
      <div class="stack">
        @if (error(); as problem) {
          <app-api-error-alert [error]="problem" reloadLabel="Reload project" (reload)="reload()" />
        }

        <form
          id="sub-project-form"
          class="form"
          [formGroup]="form"
          (ngSubmit)="submit()"
          #formElement
        >
          <app-form-field
            label="Name"
            hint="For example a building or a phase."
            [control]="form.controls.name"
            requiredMessage="Enter the sub-project name."
          >
            <input appControl formControlName="name" autocomplete="off" />
          </app-form-field>
          <app-form-field
            label="Start date"
            [control]="form.controls.startDate"
            requiredMessage="Choose the date it starts."
          >
            <input appControl type="date" formControlName="startDate" />
          </app-form-field>
        </form>
      </div>

      <div dialogActions class="dialog-actions">
        <button appButton type="button" [attr.disabled]="saving() ? '' : null" (click)="cancel()">
          Cancel
        </button>
        <button appButton variant="primary" type="submit" form="sub-project-form" [busy]="saving()">
          {{ saveLabel() }}
        </button>
      </div>
    </app-dialog>
  `,
})
export class SubProjectDialog implements OnInit {
  /** The sub-project to edit; leave it out to add a new one. */
  readonly subProject = input<SubProject | null>(null);

  /** The dialog was dismissed or finished: the page removes it. */
  readonly dismissed = output<void>();
  /** A sub-project was saved (the open project is already updated). */
  readonly saved = output<void>();

  private readonly api = inject(ProjectsApi);
  private readonly context = inject(ProjectContext);
  private readonly session = inject(SessionState);
  private readonly dialog = viewChild.required(Dialog);
  private readonly formElement = viewChild<ElementRef<HTMLFormElement>>('formElement');
  private readonly focusInvalid = injectFocusFirstInvalid();

  protected readonly title = computed(() =>
    this.subProject() ? 'Edit sub-project' : 'Add sub-project',
  );
  protected readonly saveLabel = computed(() =>
    this.subProject() ? 'Save sub-project' : 'Add sub-project',
  );

  protected readonly form = new FormGroup({
    name: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(120)],
    }),
    startDate: new FormControl(todayIn(this.session.timeZoneId()), {
      nonNullable: true,
      validators: [Validators.required],
    }),
  });

  protected readonly saving = signal(false);
  protected readonly error = signal<ApiError | null>(null);

  ngOnInit(): void {
    // Fill the form once the input is known (an edit); an add keeps its defaults.
    const existing = this.subProject();
    if (existing) this.form.reset({ name: existing.name, startDate: existing.startDate });
  }

  protected cancel(): void {
    this.dialog().dismiss();
  }

  protected reload(): void {
    this.error.set(null);
    void this.context.reload();
  }

  protected async submit(): Promise<void> {
    if (this.saving()) return;
    this.form.markAllAsTouched();
    if (this.form.invalid) {
      this.focusInvalid(this.formElement()?.nativeElement);
      return;
    }

    const { name, startDate } = this.form.getRawValue();
    const details = { name: name.trim(), startDate };
    const project = this.context.project();
    const existing = this.subProject();
    this.saving.set(true);
    this.error.set(null);
    try {
      const updated = await firstValueFrom(
        existing
          ? this.api.updateSubProject(project.id, existing.id, details, project.version)
          : this.api.addSubProject(project.id, details, project.version),
      );
      this.context.replace(updated);
      this.saved.emit();
    } catch (failure) {
      const problem = asApiError(failure);
      if (problem.isRuleViolation) {
        // The only business rule here is "this name is already used", so it belongs under the name.
        setServerError(this.form.controls.name, problem.detail);
      } else {
        const unmatched = applyServerErrors(this.form, problem);
        if (
          problem.isConflict ||
          problem.isServerFailure ||
          unmatched.length > 0 ||
          !problem.isValidation
        )
          this.error.set(problem);
      }
      this.focusInvalid(this.formElement()?.nativeElement);
    } finally {
      this.saving.set(false);
    }
  }
}
