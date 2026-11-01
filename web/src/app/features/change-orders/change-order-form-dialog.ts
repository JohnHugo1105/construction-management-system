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
import { applyServerErrors, injectFocusFirstInvalid } from '../../shared/forms/server-errors';
import { formatMoney } from '../../shared/presentation/money';
import { ApiErrorAlert } from '../../shared/ui/api-error-alert/api-error-alert';
import { Button } from '../../shared/ui/button/button';
import { Dialog } from '../../shared/ui/dialog/dialog';
import { Control } from '../../shared/ui/form-field/control';
import { FieldGroup } from '../../shared/ui/form-field/field-group';
import { FormField } from '../../shared/ui/form-field/form-field';
import { MoneyInput } from '../../shared/ui/inputs/money-input';
import { ClientRequestId } from '../costing/client-request-id';
import { ProjectContext } from '../projects/project-context';
import { REASON_CHOICES } from './change-order-presentation';
import { ChangeOrdersApi } from './change-orders-api';
import { ChangeOrder, ChangeOrderDetails, ChangeOrderReason } from './change-orders.models';

/**
 * Starts a change order, or edits a draft's title, reason, description and change to the contract price. Lines are added on the
 * change order page, one at a time. A new order carries one request id for every attempt, so a double tap makes one draft.
 */
@Component({
  selector: 'app-change-order-form-dialog',
  imports: [
    ReactiveFormsModule,
    Dialog,
    Button,
    FormField,
    FieldGroup,
    Control,
    MoneyInput,
    ApiErrorAlert,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-dialog
      [title]="order() ? 'Edit change order' : 'New change order'"
      [locked]="saving()"
      (closed)="dismissed.emit()"
    >
      <div class="stack">
        @if (error(); as problem) {
          <app-api-error-alert
            [error]="problem"
            [extra]="extra()"
            (reload)="reloadRequested.emit()"
          />
        }
        <form
          id="change-order-form"
          class="form"
          [formGroup]="form"
          (ngSubmit)="submit()"
          #formElement
        >
          <app-form-field
            label="Title"
            hint="What changes, in a few words."
            [control]="form.controls.title"
            requiredMessage="Enter a title for the change."
          >
            <input
              appControl
              formControlName="title"
              maxlength="120"
              autocomplete="off"
              data-autofocus
            />
          </app-form-field>

          <app-field-group
            label="Why is it needed?"
            [control]="form.controls.reason"
            requiredMessage="Choose why the change is needed."
          >
            <div class="choice-list">
              @for (choice of reasons; track choice.value) {
                <label class="choice">
                  <input type="radio" formControlName="reason" [value]="choice.value" />
                  <span class="choice__text"
                    ><span class="choice__label">{{ choice.label }}</span></span
                  >
                </label>
              }
            </div>
          </app-field-group>

          <app-form-field
            label="Description"
            [optional]="true"
            [control]="form.controls.description"
          >
            <textarea appControl formControlName="description" rows="3" maxlength="1000"></textarea>
          </app-form-field>

          @if (hasPrice()) {
            <app-form-field
              label="Change to the contract price"
              [hint]="priceHint()"
              [control]="form.controls.contractPriceDeltaMinor"
            >
              <app-money-input
                formControlName="contractPriceDeltaMinor"
                [allowNegative]="true"
                placeholder="0.00"
              />
            </app-form-field>
          } @else {
            <p class="text-muted text-small">
              This project has no contract price, so this change order changes only the budget.
            </p>
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
          Cancel
        </button>
        <button
          appButton
          variant="primary"
          type="submit"
          form="change-order-form"
          [busy]="saving()"
        >
          {{ order() ? 'Save change order' : 'Start change order' }}
        </button>
      </div>
    </app-dialog>
  `,
})
export class ChangeOrderFormDialog implements OnInit {
  /** The draft to edit, or null to start a new one. */
  readonly order = input<ChangeOrder | null>(null);

  readonly saved = output<ChangeOrder>();
  readonly dismissed = output<void>();
  readonly reloadRequested = output<void>();

  protected readonly dialog = viewChild.required(Dialog);
  private readonly formElement = viewChild<ElementRef<HTMLFormElement>>('formElement');
  private readonly api = inject(ChangeOrdersApi);
  private readonly context = inject(ProjectContext);
  private readonly session = inject(SessionState);
  private readonly focusInvalid = injectFocusFirstInvalid();
  private readonly requestId = new ClientRequestId();

  protected readonly reasons = REASON_CHOICES;
  protected readonly saving = signal(false);
  protected readonly error = signal<ApiError | null>(null);
  protected readonly extra = signal<readonly string[]>([]);
  protected readonly hasPrice = computed(() => this.context.project().contractPriceMinor !== null);
  protected readonly priceHint = computed(() => {
    const price = this.context.project().contractPriceMinor;
    const now =
      price === null ? '' : ` It is ${formatMoney(price, this.session.currencyCode())} now.`;
    return `Leave it at 0.00 when the price stays the same; put a minus sign in front to lower it.${now}`;
  });

  protected readonly form = new FormGroup({
    title: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(120)],
    }),
    reason: new FormControl<ChangeOrderReason | ''>('', {
      nonNullable: true,
      validators: [Validators.required],
    }),
    description: new FormControl('', {
      nonNullable: true,
      validators: [Validators.maxLength(1000)],
    }),
    contractPriceDeltaMinor: new FormControl<number | null>(0),
  });

  ngOnInit(): void {
    const order = this.order();
    if (order)
      this.form.setValue({
        title: order.title,
        reason: order.reason,
        description: order.description ?? '',
        contractPriceDeltaMinor: order.contractPriceDeltaMinor,
      });
  }

  protected async submit(): Promise<void> {
    if (this.saving()) return;
    this.form.markAllAsTouched();
    if (this.form.controls.title.value.trim() === '')
      this.form.controls.title.setErrors({ required: true });
    if (this.form.invalid) {
      this.focusInvalid(this.formElement()?.nativeElement);
      return;
    }
    const value = this.form.getRawValue();
    const details: ChangeOrderDetails = {
      title: value.title.trim(),
      description: value.description.trim() === '' ? null : value.description.trim(),
      reason: value.reason as ChangeOrderReason,
      contractPriceDeltaMinor: this.hasPrice() ? (value.contractPriceDeltaMinor ?? 0) : 0,
    };

    this.saving.set(true);
    this.error.set(null);
    try {
      const order = this.order();
      const result = await firstValueFrom(
        order
          ? this.api.update(order.id, details, order.version)
          : this.api.create(this.context.project().id, details, this.requestId.current),
      );
      this.saved.emit(result);
    } catch (failure) {
      const problem = asApiError(failure);
      const unmatched = applyServerErrors(this.form, problem);
      this.extra.set(unmatched);
      if (!problem.isValidation || unmatched.length > 0) this.error.set(problem);
      this.focusInvalid(this.formElement()?.nativeElement);
    } finally {
      this.saving.set(false);
    }
  }
}
