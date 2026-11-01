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
import { SessionState } from '../../core/session/session-state';
import { injectFocusFirstInvalid } from '../../shared/forms/server-errors';
import { todayIn } from '../../shared/presentation/business-time';
import { ApiErrorAlert } from '../../shared/ui/api-error-alert/api-error-alert';
import { Button } from '../../shared/ui/button/button';
import { Dialog } from '../../shared/ui/dialog/dialog';
import { Control } from '../../shared/ui/form-field/control';
import { FieldGroup } from '../../shared/ui/form-field/field-group';
import { FormField } from '../../shared/ui/form-field/form-field';
import { describeFormFailure } from './form-failure';
import { WORKER_STATUS, nextStatusChoices, statusNeedsReason } from './worker-status';
import { WorkforceApi } from './workforce-api';
import { WorkerDetail, WorkerStatus } from './workforce.models';

/** A reason is required for Suspended and Terminated, and optional for the other states. */
function reasonRequiredForStatus(control: AbstractControl): ValidationErrors | null {
  const status = control.parent?.get('status')?.value as WorkerStatus | null | undefined;
  return status && statusNeedsReason(status) && String(control.value ?? '').trim() === ''
    ? {
        message:
          status === 'Suspended'
            ? 'Say why the worker is suspended.'
            : 'Say why the worker is terminated.',
      }
    : null;
}

/**
 * Moves a worker to another state. Only the states that make sense from where the worker is are offered, none is pre-selected,
 * and a reason is required for Suspended and Terminated. A worker who has left is not changed here: they are reinstated.
 */
@Component({
  selector: 'app-change-status-dialog',
  imports: [ReactiveFormsModule, Dialog, Button, FieldGroup, FormField, Control, ApiErrorAlert],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-dialog title="Change status" [locked]="saving()" (closed)="dismissed.emit()">
      <div class="stack">
        <p>
          {{ worker().fullName }} is now <strong>{{ currentLabel() }}</strong
          >.
        </p>

        @if (error(); as problem) {
          <app-api-error-alert
            [error]="problem"
            [extra]="extra()"
            reloadLabel="Reload worker"
            (reload)="reloaded.emit()"
          />
        }

        <form id="status-form" class="form" [formGroup]="form" (ngSubmit)="submit()" #formElement>
          <app-field-group
            label="Change to"
            [control]="form.controls.status"
            requiredMessage="Choose the new status."
          >
            <div class="choice-list">
              @for (choice of choices(); track choice.status) {
                <label class="choice">
                  <input type="radio" formControlName="status" [value]="choice.status" />
                  <span class="choice__text">
                    <span class="choice__label">{{ labels[choice.status].label }}</span>
                    <span class="choice__hint">{{ choice.help }}</span>
                  </span>
                </label>
              }
            </div>
          </app-field-group>

          @if (selected()) {
            <app-form-field
              label="Reason"
              [optional]="!reasonRequired()"
              [control]="form.controls.reason"
            >
              <textarea appControl formControlName="reason" rows="3" maxlength="300"></textarea>
            </app-form-field>
          }
          <app-form-field
            label="Effective date"
            hint="From the hire date up to today."
            [control]="form.controls.effectiveDate"
            requiredMessage="Choose the date the change takes effect."
          >
            <input
              appControl
              type="date"
              formControlName="effectiveDate"
              [min]="worker().dateHired"
              [max]="today"
            />
          </app-form-field>
        </form>
      </div>

      <div dialogActions class="dialog-actions">
        <button appButton type="button" [attr.disabled]="saving() ? '' : null" (click)="cancel()">
          Cancel
        </button>
        <button appButton variant="primary" type="submit" form="status-form" [busy]="saving()">
          Change status
        </button>
      </div>
    </app-dialog>
  `,
})
export class ChangeStatusDialog {
  readonly worker = input.required<WorkerDetail>();

  /** The worker the API returned with the new state. */
  readonly changed = output<WorkerDetail>();
  readonly dismissed = output<void>();
  readonly reloaded = output<void>();

  private readonly api = inject(WorkforceApi);
  private readonly session = inject(SessionState);
  private readonly dialog = viewChild.required(Dialog);
  private readonly formElement = viewChild<ElementRef<HTMLFormElement>>('formElement');
  private readonly focusInvalid = injectFocusFirstInvalid();

  protected readonly labels = WORKER_STATUS;
  protected readonly today = todayIn(this.session.timeZoneId());

  protected readonly form = new FormGroup({
    status: new FormControl<WorkerStatus | null>(null, { validators: [Validators.required] }),
    effectiveDate: new FormControl(this.today, {
      nonNullable: true,
      validators: [Validators.required],
    }),
    reason: new FormControl('', {
      nonNullable: true,
      validators: [reasonRequiredForStatus, Validators.maxLength(300)],
    }),
  });

  protected readonly selected = toSignal(
    this.form.controls.status.valueChanges.pipe(startWith(this.form.controls.status.value)),
    { initialValue: null },
  );
  protected readonly reasonRequired = computed(() => {
    const status = this.selected();
    return status !== null && statusNeedsReason(status);
  });

  protected readonly choices = computed(() => nextStatusChoices(this.worker().status));
  protected readonly currentLabel = computed(() => WORKER_STATUS[this.worker().status].label);

  protected readonly saving = signal(false);
  protected readonly error = signal<ApiError | null>(null);
  protected readonly extra = signal<readonly string[]>([]);

  constructor() {
    // The reason is required only for some states, so check it again whenever the choice changes.
    this.form.controls.status.valueChanges
      .pipe(takeUntilDestroyed(inject(DestroyRef)))
      .subscribe(() => this.form.controls.reason.updateValueAndValidity());
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

    const value = this.form.getRawValue();
    this.saving.set(true);
    this.error.set(null);
    this.extra.set([]);
    try {
      const updated = await firstValueFrom(
        this.api.changeStatus(this.worker().id, {
          status: value.status as WorkerStatus,
          effectiveDate: value.effectiveDate,
          reason: value.reason.trim() || null,
          version: this.worker().version,
        }),
      );
      this.changed.emit(updated);
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
