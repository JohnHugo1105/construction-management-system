import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  OnInit,
  inject,
  input,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { ApiError, asApiError } from '../../core/api/api-error';
import { applyServerErrors, injectFocusFirstInvalid } from '../../shared/forms/server-errors';
import { ApiErrorAlert } from '../../shared/ui/api-error-alert/api-error-alert';
import { Button } from '../../shared/ui/button/button';
import { Dialog } from '../../shared/ui/dialog/dialog';
import { Control } from '../../shared/ui/form-field/control';
import { FieldGroup } from '../../shared/ui/form-field/field-group';
import { FormField } from '../../shared/ui/form-field/form-field';
import { TimeClockApi } from './timeclock-api';
import { clockTime, directionWords, newId } from './timeclock-presentation';
import { ImportResult, PunchDirection, TimeClockPunch } from './timeclock.models';

/**
 * Adds a scan a worker missed (a badge left at home, a machine that was down), with the reason. The time is the company's clock; the date starts
 * on the day being reviewed and can be the next morning for a night shift.
 */
@Component({
  selector: 'app-add-scan-dialog',
  imports: [ReactiveFormsModule, Dialog, Button, FormField, FieldGroup, Control, ApiErrorAlert],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-dialog title="Add a missing scan" [locked]="saving()" (closed)="dismissed.emit()">
      <div class="stack">
        <p>
          For {{ workerName() }}. The scan is kept with your reason; it counts once the day is
          confirmed.
        </p>
        @if (error(); as problem) {
          <app-api-error-alert
            [error]="problem"
            [extra]="extra()"
            reloadLabel="Reload the day"
            (reload)="reloaded.emit()"
          />
        }
        <form id="add-scan-form" class="form" [formGroup]="form" (ngSubmit)="submit()" #formElement>
          <app-field-group
            label="Which scan is missing?"
            [control]="form.controls.direction"
            requiredMessage="Choose time in or time out."
          >
            <div class="choice-list">
              <label class="choice">
                <input type="radio" formControlName="direction" value="In" />
                <span class="choice__text"><span class="choice__label">Time in</span></span>
              </label>
              <label class="choice">
                <input type="radio" formControlName="direction" value="Out" />
                <span class="choice__text"><span class="choice__label">Time out</span></span>
              </label>
            </div>
          </app-field-group>
          <div class="form__row">
            <app-form-field
              label="Date"
              [control]="form.controls.date"
              requiredMessage="Choose the date of the scan."
            >
              <input appControl type="date" formControlName="date" />
            </app-form-field>
            <app-form-field
              label="Time"
              [control]="form.controls.time"
              requiredMessage="Enter the time of the scan."
            >
              <input appControl type="time" formControlName="time" />
            </app-form-field>
          </div>
          <app-form-field
            label="Why is it being added?"
            [control]="form.controls.reason"
            requiredMessage="Say why the scan is being added."
          >
            <textarea appControl formControlName="reason" rows="2" maxlength="300"></textarea>
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
        <button appButton variant="primary" type="submit" form="add-scan-form" [busy]="saving()">
          Add scan
        </button>
      </div>
    </app-dialog>
  `,
})
export class AddScanDialog implements OnInit {
  readonly projectId = input.required<string>();
  readonly workerId = input.required<string>();
  readonly workerName = input.required<string>();
  readonly workDate = input.required<string>();
  /** The direction the reviewer most likely needs (the one missing). */
  readonly suggested = input<PunchDirection | null>(null);

  readonly added = output<TimeClockPunch>();
  readonly dismissed = output<void>();
  readonly reloaded = output<void>();

  private readonly api = inject(TimeClockApi);
  protected readonly dialog = viewChild.required(Dialog);
  private readonly formElement = viewChild<ElementRef<HTMLFormElement>>('formElement');
  private readonly focusInvalid = injectFocusFirstInvalid();
  private readonly requestId = newId();

  protected readonly form = new FormGroup({
    direction: new FormControl<PunchDirection | null>(null, { validators: [Validators.required] }),
    date: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    time: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    reason: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(300)],
    }),
  });

  protected readonly saving = signal(false);
  protected readonly error = signal<ApiError | null>(null);
  protected readonly extra = signal<readonly string[]>([]);

  ngOnInit(): void {
    this.form.patchValue({ date: this.workDate(), direction: this.suggested() });
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
      const punch = await firstValueFrom(
        this.api.addManual({
          projectId: this.projectId(),
          workerId: this.workerId(),
          at: `${value.date}T${value.time}`,
          direction: value.direction!,
          reason: value.reason.trim(),
          clientRequestId: this.requestId,
        }),
      );
      this.added.emit(punch);
    } catch (failure) {
      const problem = asApiError(failure);
      this.error.set(problem);
      this.extra.set(applyServerErrors(this.form, problem, { at: 'time' }));
      this.focusInvalid(this.formElement()?.nativeElement);
    } finally {
      this.saving.set(false);
    }
  }
}

/** Removes a wrong scan, with the reason. The scan stays on record, marked removed, and no longer counts. */
@Component({
  selector: 'app-remove-scan-dialog',
  imports: [ReactiveFormsModule, Dialog, Button, FormField, Control, ApiErrorAlert],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-dialog title="Remove this scan?" [locked]="saving()" (closed)="dismissed.emit()">
      <div class="stack">
        <p>
          {{ describe() }} of {{ workerName() }} will no longer count. It stays on record with your
          reason.
        </p>
        @if (error(); as problem) {
          <app-api-error-alert
            [error]="problem"
            [extra]="extra()"
            reloadLabel="Reload the day"
            (reload)="reloaded.emit()"
          />
        }
        <form
          id="remove-scan-form"
          class="form"
          [formGroup]="form"
          (ngSubmit)="submit()"
          #formElement
        >
          <app-form-field
            label="Why is it being removed?"
            [control]="form.controls.reason"
            requiredMessage="Say why the scan is being removed."
          >
            <textarea appControl formControlName="reason" rows="2" maxlength="300"></textarea>
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
          Keep scan
        </button>
        <button appButton variant="danger" type="submit" form="remove-scan-form" [busy]="saving()">
          Remove scan
        </button>
      </div>
    </app-dialog>
  `,
})
export class RemoveScanDialog {
  readonly punch = input.required<TimeClockPunch>();
  readonly workerName = input.required<string>();

  readonly removed = output<void>();
  readonly dismissed = output<void>();
  readonly reloaded = output<void>();

  private readonly api = inject(TimeClockApi);
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

  protected describe(): string {
    return `The ${directionWords(this.punch().direction).toLowerCase()} scan at ${clockTime(this.punch().localTime)}`;
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
    try {
      await firstValueFrom(
        this.api.voidPunch(
          this.punch().id,
          this.form.getRawValue().reason.trim(),
          this.punch().version,
        ),
      );
      this.removed.emit();
    } catch (failure) {
      const problem = asApiError(failure);
      this.error.set(problem);
      this.extra.set(applyServerErrors(this.form, problem));
    } finally {
      this.saving.set(false);
    }
  }
}

/**
 * Imports a time clock machine's exported file (CSV: code, time, and optionally In or Out) for one project. The file is read here and its text
 * sent; the server keeps only the scans and answers every row.
 */
@Component({
  selector: 'app-import-scans-dialog',
  imports: [Dialog, Button, ApiErrorAlert],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-dialog title="Import a machine's file" [locked]="saving()" (closed)="dismissed.emit()">
      <div class="stack">
        @if (result(); as done) {
          <p role="status">{{ summary(done) }}</p>
          @if (done.rejectedRows.length > 0) {
            <ul class="import-rejected">
              @for (row of done.rejectedRows; track row.row) {
                <li>Row {{ row.row }}: {{ row.reason }}</li>
              }
            </ul>
          }
        } @else {
          <p>
            For {{ projectName() }}. Each row needs a badge code and a time, and may say In or Out:
            <code>ABCD-EFGH-JKMN,2026-11-02 07:02,In</code>. Times without a time zone are the
            company's. The file isn't kept.
          </p>
          @if (error(); as problem) {
            <app-api-error-alert
              [error]="problem"
              [extra]="extra()"
              reloadLabel="Reload the day"
              (reload)="reloaded.emit()"
            />
          }
          <label class="field">
            <span class="field__label">File (CSV)</span>
            <input
              #file
              class="input"
              type="file"
              accept=".csv,text/csv,text/plain"
              (change)="chosen(file.files)"
            />
          </label>
        }
      </div>
      <div dialogActions class="dialog-actions">
        @if (result()) {
          <button appButton variant="primary" type="button" (click)="finished.emit()">Done</button>
        } @else {
          <button
            appButton
            type="button"
            [attr.disabled]="saving() ? '' : null"
            (click)="dialog().dismiss()"
          >
            Cancel
          </button>
          <button appButton variant="primary" type="button" [busy]="saving()" (click)="submit()">
            Import file
          </button>
        }
      </div>
    </app-dialog>
  `,
  styles: `
    .import-rejected {
      display: grid;
      gap: var(--space-1);
      max-height: 12rem;
      overflow-y: auto;
      padding-left: var(--space-4);
    }
  `,
})
export class ImportScansDialog {
  readonly projectId = input.required<string>();
  readonly projectName = input.required<string>();

  readonly finished = output<void>();
  readonly dismissed = output<void>();
  readonly reloaded = output<void>();

  private readonly api = inject(TimeClockApi);
  protected readonly dialog = viewChild.required(Dialog);

  protected readonly file = signal<File | null>(null);
  protected readonly saving = signal(false);
  protected readonly error = signal<ApiError | null>(null);
  protected readonly extra = signal<readonly string[]>([]);
  protected readonly result = signal<ImportResult | null>(null);

  protected chosen(files: FileList | null): void {
    this.file.set(files?.item(0) ?? null);
    this.error.set(null);
  }

  protected summary(done: ImportResult): string {
    return `Read ${done.rows} ${done.rows === 1 ? 'row' : 'rows'}: ${done.accepted} added, ${done.alreadyRecorded} already there, ${done.rejected} refused.`;
  }

  protected async submit(): Promise<void> {
    const file = this.file();
    if (this.saving()) return;
    if (!file) {
      this.error.set(
        new ApiError({
          status: 422,
          title: 'Choose a file',
          detail: "Choose the machine's exported file first.",
        }),
      );
      return;
    }
    this.saving.set(true);
    this.error.set(null);
    try {
      const text = await file.text();
      this.result.set(await firstValueFrom(this.api.importFile(this.projectId(), text)));
    } catch (failure) {
      const problem = asApiError(failure);
      this.error.set(problem);
      this.extra.set(Object.values(problem.errors).flat());
    } finally {
      this.saving.set(false);
    }
  }
}
