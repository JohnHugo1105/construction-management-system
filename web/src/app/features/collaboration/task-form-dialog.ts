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
import { rxResource } from '@angular/core/rxjs-interop';
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
import { ClientRequestId } from '../costing/client-request-id';
import { describeFormFailure } from '../workforce/form-failure';
import { CollaborationApi } from './collaboration-api';
import { WorkTask } from './collaboration.models';
import { PersonPicker } from './person-picker';

/**
 * Gives a piece of work to a colleague (or yourself): what to do, who does it, and optionally by when, on which project and with notes.
 * A double tap or a retry never makes two tasks (the same request id goes with every attempt). The person it is for is told in the app.
 */
@Component({
  selector: 'app-task-form-dialog',
  imports: [ReactiveFormsModule, Dialog, Button, FormField, Control, ApiErrorAlert, PersonPicker],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-dialog title="Add task" [locked]="saving()" (closed)="dismissed.emit()">
      <div class="stack">
        @if (error(); as problem) {
          <app-api-error-alert [error]="problem" [extra]="extra()" />
        }
        <form id="task-form" class="form" [formGroup]="form" (ngSubmit)="submit()" #formElement>
          <app-form-field
            label="What needs to be done?"
            [control]="form.controls.title"
            requiredMessage="Say what needs to be done."
          >
            <input
              appControl
              formControlName="title"
              maxlength="120"
              autocomplete="off"
              data-autofocus
            />
          </app-form-field>

          <app-person-picker [control]="form.controls.assigneeUserId" />

          <app-form-field
            label="Due date"
            [optional]="true"
            [control]="form.controls.dueDate"
            hint="Today or later."
          >
            <input appControl type="date" formControlName="dueDate" [min]="today" />
          </app-form-field>

          @if (projects().length > 0) {
            <app-form-field label="Project" [optional]="true" [control]="form.controls.projectId">
              <select appControl formControlName="projectId">
                <option value="">Not on a project</option>
                @for (project of projects(); track project.id) {
                  <option [value]="project.id">{{ project.name }}</option>
                }
              </select>
            </app-form-field>
          }

          <app-form-field label="Notes" [optional]="true" [control]="form.controls.description">
            <textarea appControl formControlName="description" rows="3" maxlength="1000"></textarea>
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
        <button appButton variant="primary" type="submit" form="task-form" [busy]="saving()">
          Save task
        </button>
      </div>
    </app-dialog>
  `,
})
export class TaskFormDialog implements OnInit {
  /** Starts the task on this project (from a project's page); the person can still change it. */
  readonly projectId = input<string | null>(null);

  readonly saved = output<WorkTask>();
  readonly dismissed = output<void>();

  private readonly api = inject(CollaborationApi);
  private readonly session = inject(SessionState);
  protected readonly dialog = viewChild.required(Dialog);
  private readonly formElement = viewChild<ElementRef<HTMLFormElement>>('formElement');
  private readonly focusInvalid = injectFocusFirstInvalid();
  private readonly requestId = new ClientRequestId();

  protected readonly today = todayIn(this.session.timeZoneId());

  protected readonly form = new FormGroup({
    title: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(120)],
    }),
    assigneeUserId: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    dueDate: new FormControl('', { nonNullable: true }),
    projectId: new FormControl('', { nonNullable: true }),
    description: new FormControl('', {
      nonNullable: true,
      validators: [Validators.maxLength(1000)],
    }),
  });
  protected readonly saving = signal(false);
  protected readonly error = signal<ApiError | null>(null);
  protected readonly extra = signal<readonly string[]>([]);

  private readonly projectList = rxResource({ stream: () => this.api.projects() });
  protected readonly projects = computed(() =>
    this.projectList.hasValue() ? this.projectList.value() : [],
  );

  ngOnInit(): void {
    const projectId = this.projectId();
    if (projectId) this.form.controls.projectId.setValue(projectId);
  }

  protected async submit(): Promise<void> {
    if (this.saving()) return;
    const title = this.form.controls.title;
    title.setValue(title.value.trim());
    this.form.markAllAsTouched();
    const due = this.form.controls.dueDate.value;
    if (due && due < this.today)
      this.form.controls.dueDate.setErrors({
        message: "The due date can't be in the past. Choose today or a later day.",
      });
    if (this.form.invalid) {
      this.focusInvalid(this.formElement()?.nativeElement);
      return;
    }
    const value = this.form.getRawValue();
    this.saving.set(true);
    this.error.set(null);
    this.extra.set([]);
    try {
      const task = await firstValueFrom(
        this.api.createTask(
          {
            title: value.title,
            description: value.description.trim() || null,
            dueDate: value.dueDate || null,
            assigneeUserId: value.assigneeUserId,
            projectId: value.projectId || null,
          },
          this.requestId.current,
        ),
      );
      this.requestId.renew();
      this.saved.emit(task);
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
