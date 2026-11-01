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
import { Observable, firstValueFrom } from 'rxjs';
import { ApiError, asApiError } from '../../core/api/api-error';
import { SessionState } from '../../core/session/session-state';
import { injectFocusFirstInvalid } from '../../shared/forms/server-errors';
import { todayIn } from '../../shared/presentation/business-time';
import { ApiErrorAlert } from '../../shared/ui/api-error-alert/api-error-alert';
import { Button } from '../../shared/ui/button/button';
import { ConfirmDialog } from '../../shared/ui/dialog/confirm-dialog';
import { Dialog } from '../../shared/ui/dialog/dialog';
import { Control } from '../../shared/ui/form-field/control';
import { FormField } from '../../shared/ui/form-field/form-field';
import { MoneyInput } from '../../shared/ui/inputs/money-input';
import { describeFormFailure } from '../workforce/form-failure';
import { Subcontract } from './subcontracts.models';

/**
 * Asks before a step that needs no input (award, certify, complete), says what it will do, then runs it. A refusal is shown here
 * in the server's words with a way to reload, and the dialog stays open.
 */
@Component({
  selector: 'app-confirm-action-dialog',
  imports: [ConfirmDialog, ApiErrorAlert],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-confirm-dialog
      [title]="title()"
      [message]="message()"
      [confirmLabel]="confirmLabel()"
      [busy]="busy()"
      (confirmed)="confirm()"
      (cancelled)="dismissed.emit()"
    >
      <ng-content />
      @if (error(); as problem) {
        <app-api-error-alert
          [error]="problem"
          [allowRetry]="problem.status === 409"
          reloadLabel="Reload subcontract"
          (reload)="reloaded.emit()"
        />
      }
    </app-confirm-dialog>
  `,
})
export class ConfirmActionDialog {
  readonly title = input.required<string>();
  readonly message = input.required<string>();
  readonly confirmLabel = input.required<string>();
  /** The request to run; it answers with the whole subcontract. */
  readonly run = input.required<() => Observable<Subcontract>>();

  readonly done = output<Subcontract>();
  readonly dismissed = output<void>();
  readonly reloaded = output<void>();

  protected readonly busy = signal(false);
  protected readonly error = signal<ApiError | null>(null);

  protected async confirm(): Promise<void> {
    if (this.busy()) return;
    this.busy.set(true);
    this.error.set(null);
    try {
      this.done.emit(await firstValueFrom(this.run()()));
    } catch (failure) {
      this.error.set(asApiError(failure));
    } finally {
      this.busy.set(false);
    }
  }
}

/** A step that needs a reason (cancel, discard or void a claim, void a payment). The reason stays on the record. */
@Component({
  selector: 'app-reason-dialog',
  imports: [ReactiveFormsModule, Dialog, Button, FormField, Control, ApiErrorAlert],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-dialog [title]="title()" [locked]="saving()" (closed)="dismissed.emit()">
      <div class="stack">
        <p>{{ message() }}</p>
        @if (error(); as problem) {
          <app-api-error-alert
            [error]="problem"
            [extra]="extra()"
            [allowRetry]="problem.status === 409"
            reloadLabel="Reload subcontract"
            (reload)="reloaded.emit()"
          />
        }
        <form id="reason-form" class="form" [formGroup]="form" (ngSubmit)="submit()" #formElement>
          <app-form-field [label]="reasonLabel()" [control]="form.controls.reason" requiredMessage="Give a reason.">
            <textarea appControl formControlName="reason" rows="3" maxlength="300"></textarea>
          </app-form-field>
        </form>
      </div>
      <div dialogActions class="dialog-actions">
        <button appButton type="button" [attr.disabled]="saving() ? '' : null" (click)="dialog().dismiss()">
          {{ keepLabel() }}
        </button>
        <button appButton variant="danger" type="submit" form="reason-form" [busy]="saving()">{{ confirmLabel() }}</button>
      </div>
    </app-dialog>
  `,
})
export class ReasonDialog {
  readonly title = input.required<string>();
  readonly message = input.required<string>();
  readonly reasonLabel = input('Why?');
  readonly confirmLabel = input.required<string>();
  readonly keepLabel = input('Keep it');
  readonly run = input.required<(reason: string) => Observable<Subcontract>>();

  readonly done = output<Subcontract>();
  readonly dismissed = output<void>();
  readonly reloaded = output<void>();

  protected readonly dialog = viewChild.required(Dialog);
  private readonly formElement = viewChild<ElementRef<HTMLFormElement>>('formElement');
  private readonly focusInvalid = injectFocusFirstInvalid();

  protected readonly form = new FormGroup({
    reason: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(300)] }),
  });
  protected readonly saving = signal(false);
  protected readonly error = signal<ApiError | null>(null);
  protected readonly extra = signal<readonly string[]>([]);

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
      this.done.emit(await firstValueFrom(this.run()(this.form.getRawValue().reason.trim())));
    } catch (failure) {
      const outcome = describeFormFailure(this.form, asApiError(failure));
      this.error.set(outcome.alert);
      this.extra.set(outcome.extra);
    } finally {
      this.saving.set(false);
    }
  }
}

/** Records a variation: a change to the contract sum, more work (positive) or less (negative). */
@Component({
  selector: 'app-variation-dialog',
  imports: [ReactiveFormsModule, Dialog, Button, FormField, Control, MoneyInput, ApiErrorAlert],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-dialog title="Record a variation" [locked]="saving()" (closed)="dismissed.emit()">
      <div class="stack">
        @if (error(); as problem) {
          <app-api-error-alert
            [error]="problem"
            [extra]="extra()"
            [allowRetry]="problem.status === 409"
            reloadLabel="Reload subcontract"
            (reload)="reloaded.emit()"
          />
        }
        <form id="variation-form" class="form" [formGroup]="form" (ngSubmit)="submit()" #formElement>
          <app-form-field label="What changes" [control]="form.controls.description" requiredMessage="Say what the variation changes.">
            <input appControl formControlName="description" maxlength="200" autocomplete="off" />
          </app-form-field>
          <app-form-field
            label="Amount"
            hint="On the same VAT basis as the contract sum. Start with a minus sign for work taken off."
            [control]="form.controls.amountMinor"
            requiredMessage="Enter the amount."
          >
            <app-money-input formControlName="amountMinor" [allowNegative]="true" />
          </app-form-field>
          <app-form-field label="Date" [control]="form.controls.variationDate" requiredMessage="Choose the date.">
            <input appControl type="date" formControlName="variationDate" [max]="today" />
          </app-form-field>
        </form>
      </div>
      <div dialogActions class="dialog-actions">
        <button appButton type="button" [attr.disabled]="saving() ? '' : null" (click)="dialog().dismiss()">Cancel</button>
        <button appButton variant="primary" type="submit" form="variation-form" [busy]="saving()">Record variation</button>
      </div>
    </app-dialog>
  `,
})
export class VariationDialog {
  readonly run = input.required<
    (variation: { description: string; amountMinor: number | null; variationDate: string }) => Observable<Subcontract>
  >();
  readonly done = output<Subcontract>();
  readonly dismissed = output<void>();
  readonly reloaded = output<void>();

  protected readonly dialog = viewChild.required(Dialog);
  private readonly formElement = viewChild<ElementRef<HTMLFormElement>>('formElement');
  private readonly focusInvalid = injectFocusFirstInvalid();
  protected readonly today = todayIn(inject(SessionState).timeZoneId());

  protected readonly form = new FormGroup({
    description: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(200)] }),
    amountMinor: new FormControl<number | null>(null, { validators: [Validators.required] }),
    variationDate: new FormControl(this.today, { nonNullable: true, validators: [Validators.required] }),
  });
  protected readonly saving = signal(false);
  protected readonly error = signal<ApiError | null>(null);
  protected readonly extra = signal<readonly string[]>([]);

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
      const value = this.form.getRawValue();
      this.done.emit(await firstValueFrom(this.run()({ ...value, description: value.description.trim() })));
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
