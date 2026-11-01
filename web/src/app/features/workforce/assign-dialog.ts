import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  computed,
  inject,
  input,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { firstValueFrom, startWith } from 'rxjs';
import { ApiError, asApiError } from '../../core/api/api-error';
import { SessionState } from '../../core/session/session-state';
import { applyServerErrors, injectFocusFirstInvalid } from '../../shared/forms/server-errors';
import { todayIn } from '../../shared/presentation/business-time';
import { Alert } from '../../shared/ui/alert/alert';
import { ApiErrorAlert } from '../../shared/ui/api-error-alert/api-error-alert';
import { Button } from '../../shared/ui/button/button';
import { Dialog } from '../../shared/ui/dialog/dialog';
import { Control } from '../../shared/ui/form-field/control';
import { FormField } from '../../shared/ui/form-field/form-field';
import { injectProjectChoices } from './project-choices';
import { WorkforceApi } from './workforce-api';
import { WorkerDetail } from './workforce.models';

/** The worker (or workers) to post. A single worker also carries the version the person opened. */
export interface AssignTarget {
  readonly id: string;
  readonly version: number;
}

/**
 * Posts one worker, or a whole selected crew, to a project from a date. The project is chosen, never pre-selected, so a
 * leftover default cannot post people to the wrong place. A crew is posted all or nothing: when the API refuses, it lists each
 * worker it could not post and why, and this dialog stays open so the person can fix the selection and try again.
 */
@Component({
  selector: 'app-assign-dialog',
  imports: [ReactiveFormsModule, Dialog, Button, FormField, Control, Alert, ApiErrorAlert],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-dialog [title]="title()" [locked]="saving()" (closed)="dismissed.emit()">
      <div class="stack">
        <p>{{ intro() }}</p>

        @if (refusals().length > 0) {
          <app-alert tone="danger" [title]="refusalTitle()">
            Nothing was changed.
            <ul class="refusals">
              @for (reason of refusals(); track reason) {
                <li>{{ reason }}</li>
              }
            </ul>
            <span>Take those workers out of your selection, or fix the reason, and try again.</span>
          </app-alert>
        }

        @if (error(); as problem) {
          <app-api-error-alert
            [error]="problem"
            [extra]="extra()"
            reloadLabel="Reload worker"
            (reload)="reloaded.emit()"
          />
        }

        @if (choices.error(); as problem) {
          <app-api-error-alert
            [error]="problem"
            [allowRetry]="true"
            reloadLabel="Try again"
            (reload)="choices.reload()"
          />
        }

        <form id="assign-form" class="form" [formGroup]="form" (ngSubmit)="submit()" #formElement>
          <app-form-field
            label="Project"
            [control]="form.controls.projectId"
            requiredMessage="Choose the project."
          >
            <select appControl formControlName="projectId">
              <option value="">Choose a project</option>
              @for (project of choices.projects(); track project.id) {
                <option [value]="project.id">{{ project.name }}</option>
              }
            </select>
          </app-form-field>

          @if (subProjects().length > 0) {
            <app-form-field
              label="Sub-project"
              [optional]="true"
              [control]="form.controls.subProjectId"
            >
              <select appControl formControlName="subProjectId">
                <option value="">Whole project</option>
                @for (sub of subProjects(); track sub.id) {
                  <option [value]="sub.id">{{ sub.name }}</option>
                }
              </select>
            </app-form-field>
          }

          <app-form-field
            label="Start date"
            hint="Their earlier posting, if any, ends on this date."
            [control]="form.controls.startDate"
            requiredMessage="Choose the date the posting starts."
          >
            <input appControl type="date" formControlName="startDate" />
          </app-form-field>
        </form>
      </div>

      <div dialogActions class="dialog-actions">
        <button appButton type="button" [attr.disabled]="saving() ? '' : null" (click)="cancel()">
          Cancel
        </button>
        <button appButton variant="primary" type="submit" form="assign-form" [busy]="saving()">
          {{ saveLabel() }}
        </button>
      </div>
    </app-dialog>
  `,
  styles: `
    .refusals {
      margin: var(--space-2) 0;
      padding-left: var(--space-5);
    }

    .refusals li + li {
      margin-top: var(--space-1);
    }
  `,
})
export class AssignDialog {
  readonly workers = input.required<readonly AssignTarget[]>();
  /** Who is being posted, in words: "Juan Dela Cruz" or "3 workers". */
  readonly summary = input.required<string>();
  /**
   * True when the people were picked from the list: they are posted through the crew endpoint (all or nothing, no worker
   * versions) even if only one was ticked. False on a worker's own page, where that worker's version goes with the request.
   */
  readonly crew = input(false);

  /** The posting was made. A single worker's updated record comes with it; a crew gives null. */
  readonly assigned = output<WorkerDetail | null>();
  readonly dismissed = output<void>();
  /** The person chose to reload after someone else changed the worker. */
  readonly reloaded = output<void>();

  private readonly api = inject(WorkforceApi);
  private readonly session = inject(SessionState);
  private readonly dialog = viewChild.required(Dialog);
  private readonly formElement = viewChild<ElementRef<HTMLFormElement>>('formElement');
  private readonly focusInvalid = injectFocusFirstInvalid();
  protected readonly choices = injectProjectChoices();

  protected readonly form = new FormGroup({
    projectId: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    subProjectId: new FormControl('', { nonNullable: true }),
    startDate: new FormControl(todayIn(this.session.timeZoneId()), {
      nonNullable: true,
      validators: [Validators.required],
    }),
  });

  private readonly chosenProject = toSignal(
    this.form.controls.projectId.valueChanges.pipe(startWith(this.form.controls.projectId.value)),
    { initialValue: '' },
  );

  protected readonly subProjects = computed(
    () =>
      this.choices.projects().find((project) => project.id === this.chosenProject())?.subProjects ??
      [],
  );

  protected readonly saving = signal(false);
  protected readonly error = signal<ApiError | null>(null);
  protected readonly extra = signal<readonly string[]>([]);
  /** One line for each worker the API refused to post (a crew is all or nothing). */
  protected readonly refusals = signal<readonly string[]>([]);

  private readonly many = computed(() => this.workers().length > 1);
  protected readonly title = computed(() =>
    this.crew() ? `Assign ${this.summary()} to a project` : 'Assign to a project',
  );
  protected readonly intro = computed(() =>
    this.many()
      ? `${this.summary()} will be posted to one project. If any of them can't be posted, nobody is.`
      : `${this.summary()} will be posted to the project you choose.`,
  );
  protected readonly saveLabel = computed(() => (this.many() ? 'Assign workers' : 'Assign worker'));
  protected readonly refusalTitle = computed(() => {
    const count = this.refusals().length;
    return count === 1 ? "1 worker can't be assigned" : `${count} workers can't be assigned`;
  });

  constructor() {
    // Moving to another project forgets the sub-project of the one before it.
    this.form.controls.projectId.valueChanges
      .pipe(takeUntilDestroyed(inject(DestroyRef)))
      .subscribe(() => this.form.controls.subProjectId.setValue(''));
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

    const { projectId, subProjectId, startDate } = this.form.getRawValue();
    const targets = this.workers();
    this.saving.set(true);
    this.error.set(null);
    this.extra.set([]);
    this.refusals.set([]);
    try {
      const sub = subProjectId || null;
      const first = targets[0];
      if (!this.crew() && first) {
        const updated = await firstValueFrom(
          this.api.assign(first.id, {
            projectId,
            subProjectId: sub,
            startDate,
            version: first.version,
          }),
        );
        this.assigned.emit(updated);
      } else {
        await firstValueFrom(
          this.api.assignMany({
            workerIds: targets.map((target) => target.id),
            projectId,
            subProjectId: sub,
            startDate,
          }),
        );
        this.assigned.emit(null);
      }
    } catch (failure) {
      this.showFailure(asApiError(failure));
    } finally {
      this.saving.set(false);
    }
  }

  private showFailure(problem: ApiError): void {
    // The crew refusal: one line for each worker, shown as a list rather than squeezed into a field.
    const { workers, ...others } = problem.errors;
    if (workers && workers.length > 0) this.refusals.set(workers);

    const rest = new ApiError({
      status: problem.status,
      title: problem.title,
      detail: problem.detail,
      code: problem.code,
      errors: others,
      traceId: problem.traceId,
    });
    const unmatched = applyServerErrors(this.form, rest);
    this.extra.set(unmatched);

    // A field message sits under its field, and the crew list explains itself. Anything else needs the alert.
    const explained = (workers?.length ?? 0) > 0 || (rest.isValidation && unmatched.length === 0);
    if (!explained || unmatched.length > 0) this.error.set(problem);
    this.focusInvalid(this.formElement()?.nativeElement);
  }
}
