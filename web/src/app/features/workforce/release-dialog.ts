import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
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
import { injectFocusFirstInvalid } from '../../shared/forms/server-errors';
import { formatDate, todayIn } from '../../shared/presentation/business-time';
import { ApiErrorAlert } from '../../shared/ui/api-error-alert/api-error-alert';
import { Button } from '../../shared/ui/button/button';
import { Dialog } from '../../shared/ui/dialog/dialog';
import { Control } from '../../shared/ui/form-field/control';
import { FormField } from '../../shared/ui/form-field/form-field';
import { describeFormFailure } from './form-failure';
import { WorkforceApi } from './workforce-api';
import { WorkerDetail } from './workforce.models';

/** Ends a worker's current posting without a new one. The date must be after the day the posting started. */
@Component({
  selector: 'app-release-dialog',
  imports: [ReactiveFormsModule, Dialog, Button, FormField, Control, ApiErrorAlert],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-dialog title="Release from the project?" [locked]="saving()" (closed)="dismissed.emit()">
      <div class="stack">
        <p>
          {{ worker().fullName }} will no longer be posted to {{ projectName() }}. Attendance
          already recorded stays as it is. You can post them to a project again at any time.
        </p>

        @if (error(); as problem) {
          <app-api-error-alert
            [error]="problem"
            [extra]="extra()"
            reloadLabel="Reload worker"
            (reload)="reloaded.emit()"
          />
        }

        <form id="release-form" class="form" [formGroup]="form" (ngSubmit)="submit()" #formElement>
          <app-form-field
            label="Last day on the project"
            [hint]="hint()"
            [control]="form.controls.endDate"
            requiredMessage="Choose the date the posting ends."
          >
            <input appControl type="date" formControlName="endDate" />
          </app-form-field>
        </form>
      </div>

      <div dialogActions class="dialog-actions">
        <button appButton type="button" [attr.disabled]="saving() ? '' : null" (click)="cancel()">
          Cancel
        </button>
        <button appButton variant="danger" type="submit" form="release-form" [busy]="saving()">
          Release worker
        </button>
      </div>
    </app-dialog>
  `,
})
export class ReleaseDialog {
  readonly worker = input.required<WorkerDetail>();

  readonly released = output<WorkerDetail>();
  readonly dismissed = output<void>();
  readonly reloaded = output<void>();

  private readonly api = inject(WorkforceApi);
  private readonly session = inject(SessionState);
  private readonly dialog = viewChild.required(Dialog);
  private readonly formElement = viewChild<ElementRef<HTMLFormElement>>('formElement');
  private readonly focusInvalid = injectFocusFirstInvalid();

  protected readonly form = new FormGroup({
    endDate: new FormControl(todayIn(this.session.timeZoneId()), {
      nonNullable: true,
      validators: [Validators.required],
    }),
  });

  protected readonly projectName = computed(
    () => this.worker().currentAssignment?.projectName ?? 'their project',
  );
  protected readonly hint = computed(() => {
    const started = this.worker().currentAssignment?.startDate;
    return started ? `It must be after ${formatDate(started)}, when the posting started.` : '';
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

    this.saving.set(true);
    this.error.set(null);
    this.extra.set([]);
    try {
      const updated = await firstValueFrom(
        this.api.release(this.worker().id, {
          endDate: this.form.getRawValue().endDate,
          version: this.worker().version,
        }),
      );
      this.released.emit(updated);
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
