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
import { formatMoney } from '../../shared/presentation/money';
import { ApiErrorAlert } from '../../shared/ui/api-error-alert/api-error-alert';
import { Button } from '../../shared/ui/button/button';
import { Dialog } from '../../shared/ui/dialog/dialog';
import { Control } from '../../shared/ui/form-field/control';
import { FormField } from '../../shared/ui/form-field/form-field';
import { describeFormFailure } from '../workforce/form-failure';
import { PayrollApi } from './payroll-api';
import { postingsOf } from './payroll-rules';
import { PayrollRun } from './payroll.models';

/**
 * Voids a payroll, with a reason. A draft only releases its attendance; an approved payroll also reverses the labor cost it
 * posted to the projects, and says which. The wording follows what will really happen, because the two are very different.
 */
@Component({
  selector: 'app-void-run-dialog',
  imports: [ReactiveFormsModule, Dialog, Button, FormField, Control, ApiErrorAlert],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-dialog title="Void this payroll?" [locked]="saving()" (closed)="dismissed.emit()">
      <div class="stack">
        @if (wasApproved()) {
          <p>
            Voiding an approved payroll unlocks the attendance and reverses the labor cost posted to
            the projects:
          </p>
          <ul class="postings" aria-label="Labor cost to be reversed">
            @for (posting of postings(); track posting.key) {
              <li class="postings__row">
                <span class="postings__place">{{ posting.label }}</span>
                <span class="money">{{ posting.amount }}</span>
              </li>
            }
          </ul>
        } @else {
          <p>
            Voiding this draft releases its attendance so it can be paid in a new payroll. The draft
            stays on record.
          </p>
        }

        @if (error(); as problem) {
          <app-api-error-alert
            [error]="problem"
            [extra]="extra()"
            [allowRetry]="problem.status === 409"
            reloadLabel="Reload payroll"
            (reload)="reloaded.emit()"
          />
        }

        <form id="void-run-form" class="form" [formGroup]="form" (ngSubmit)="submit()" #formElement>
          <app-form-field
            label="Why is it being voided?"
            [control]="form.controls.reason"
            requiredMessage="Say why the payroll is being voided."
          >
            <textarea appControl formControlName="reason" rows="3" maxlength="300"></textarea>
          </app-form-field>
        </form>
      </div>

      <div dialogActions class="dialog-actions">
        <button appButton type="button" [attr.disabled]="saving() ? '' : null" (click)="cancel()">
          Keep payroll
        </button>
        <button appButton variant="danger" type="submit" form="void-run-form" [busy]="saving()">
          Void payroll
        </button>
      </div>
    </app-dialog>
  `,
  styles: `
    .postings {
      display: grid;
      gap: var(--space-2);
      margin: 0;
      padding: 0;
      list-style: none;
    }

    .postings__row {
      display: flex;
      align-items: baseline;
      justify-content: space-between;
      gap: var(--space-3);
      padding: var(--space-2) 0;
      border-bottom: 1px solid var(--color-border);
    }

    .postings__place {
      min-width: 0;
      overflow-wrap: anywhere;
    }
  `,
})
export class VoidRunDialog {
  readonly run = input.required<PayrollRun>();

  /** The run the API returned once it is voided. */
  readonly voided = output<PayrollRun>();
  readonly dismissed = output<void>();
  readonly reloaded = output<void>();

  private readonly api = inject(PayrollApi);
  private readonly session = inject(SessionState);
  private readonly dialog = viewChild.required(Dialog);
  private readonly formElement = viewChild<ElementRef<HTMLFormElement>>('formElement');
  private readonly focusInvalid = injectFocusFirstInvalid();

  protected readonly form = new FormGroup({
    reason: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(300)],
    }),
  });

  protected readonly wasApproved = computed(() => this.run().status === 'Approved');
  protected readonly postings = computed(() =>
    postingsOf(this.run()).map((posting) => ({
      key: posting.key,
      label: posting.label,
      amount: formatMoney(posting.amountMinor, this.session.currencyCode()),
    })),
  );

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
      const run = this.run();
      const voided = await firstValueFrom(
        this.api.void(run.id, this.form.getRawValue().reason.trim(), run.version),
      );
      this.voided.emit(voided);
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
