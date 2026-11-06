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
import { injectFocusFirstInvalid } from '../../shared/forms/server-errors';
import { ApiErrorAlert } from '../../shared/ui/api-error-alert/api-error-alert';
import { Button } from '../../shared/ui/button/button';
import { Dialog } from '../../shared/ui/dialog/dialog';
import { Control } from '../../shared/ui/form-field/control';
import { FormField } from '../../shared/ui/form-field/form-field';
import { describeFormFailure } from '../workforce/form-failure';
import { CollaborationApi } from './collaboration-api';
import { WorkTask } from './collaboration.models';
import { PersonPicker } from './person-picker';

export type TaskStep = 'reassign' | 'cancel';

/**
 * The two task steps that ask something first: give the task to someone else (they are told in the app), or call it off with an optional
 * reason. Each sends the version the person opened; if someone changed the task meanwhile, it says so and offers to reload.
 */
@Component({
  selector: 'app-task-step-dialog',
  imports: [ReactiveFormsModule, Dialog, Button, FormField, Control, ApiErrorAlert, PersonPicker],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-dialog [title]="words().title" [locked]="saving()" (closed)="dismissed.emit()">
      <div class="stack">
        <p>{{ words().intro }}</p>
        @if (error(); as problem) {
          <app-api-error-alert
            [error]="problem"
            [extra]="extra()"
            [allowRetry]="problem.status === 409"
            reloadLabel="Reload task"
            (reload)="reloaded.emit()"
          />
        }
        <form
          id="task-step-form"
          class="form"
          [formGroup]="form"
          (ngSubmit)="submit()"
          #formElement
        >
          @if (step() === 'reassign') {
            <app-person-picker
              label="Give it to"
              [control]="form.controls.assigneeUserId"
              [exclude]="task().assigneeUserId"
            />
          } @else {
            <app-form-field
              label="Why is it cancelled?"
              [optional]="true"
              [control]="form.controls.reason"
            >
              <textarea
                appControl
                formControlName="reason"
                rows="3"
                maxlength="300"
                data-autofocus
              ></textarea>
            </app-form-field>
          }
        </form>
      </div>
      <div dialogActions class="dialog-actions">
        <button
          appButton
          type="button"
          [attr.disabled]="saving() ? '' : null"
          (click)="dialog().dismiss()"
        >
          {{ words().back }}
        </button>
        <button
          appButton
          [variant]="step() === 'cancel' ? 'danger' : 'primary'"
          type="submit"
          form="task-step-form"
          [busy]="saving()"
        >
          {{ words().confirm }}
        </button>
      </div>
    </app-dialog>
  `,
})
export class TaskStepDialog implements OnInit {
  readonly task = input.required<WorkTask>();
  readonly step = input.required<TaskStep>();

  readonly done = output<WorkTask>();
  readonly dismissed = output<void>();
  readonly reloaded = output<void>();

  private readonly api = inject(CollaborationApi);
  protected readonly dialog = viewChild.required(Dialog);
  private readonly formElement = viewChild<ElementRef<HTMLFormElement>>('formElement');
  private readonly focusInvalid = injectFocusFirstInvalid();

  protected readonly form = new FormGroup({
    assigneeUserId: new FormControl('', { nonNullable: true }),
    reason: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(300)] }),
  });
  protected readonly saving = signal(false);
  protected readonly error = signal<ApiError | null>(null);
  protected readonly extra = signal<readonly string[]>([]);

  protected readonly words = computed(() =>
    this.step() === 'reassign'
      ? {
          title: 'Give this task to someone else',
          intro: `It is assigned to ${this.task().assigneeName ?? 'a colleague'} now. The person you choose is told in the app.`,
          back: 'Cancel',
          confirm: 'Reassign task',
        }
      : {
          title: 'Cancel this task?',
          intro: 'It stays on record as cancelled. You can reopen it later.',
          back: 'Keep task',
          confirm: 'Cancel task',
        },
  );

  ngOnInit(): void {
    if (this.step() === 'reassign') {
      this.form.controls.assigneeUserId.addValidators(Validators.required);
      this.form.controls.assigneeUserId.updateValueAndValidity();
    }
  }

  protected async submit(): Promise<void> {
    if (this.saving()) return;
    this.form.markAllAsTouched();
    if (this.form.invalid) {
      this.focusInvalid(this.formElement()?.nativeElement);
      return;
    }
    const task = this.task();
    const { assigneeUserId, reason } = this.form.getRawValue();
    const call =
      this.step() === 'reassign'
        ? this.api.reassignTask(task.id, assigneeUserId, task.version)
        : this.api.cancelTask(task.id, reason.trim() || null, task.version);
    this.saving.set(true);
    this.error.set(null);
    this.extra.set([]);
    try {
      this.done.emit(await firstValueFrom(call));
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
