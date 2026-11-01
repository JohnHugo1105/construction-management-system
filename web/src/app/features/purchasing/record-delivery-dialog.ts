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
import { injectFocusFirstInvalid } from '../../shared/forms/server-errors';
import { todayIn } from '../../shared/presentation/business-time';
import { Alert } from '../../shared/ui/alert/alert';
import { ApiErrorAlert } from '../../shared/ui/api-error-alert/api-error-alert';
import { Button } from '../../shared/ui/button/button';
import { Dialog } from '../../shared/ui/dialog/dialog';
import { Control } from '../../shared/ui/form-field/control';
import { FormField } from '../../shared/ui/form-field/form-field';
import { NumberInput } from '../../shared/ui/inputs/number-input';
import { ClientRequestId } from '../costing/client-request-id';
import { describeFormFailure } from '../workforce/form-failure';
import { PurchasingApi } from './purchasing-api';
import { formatQuantity } from './purchasing-presentation';
import { PurchaseOrder } from './purchasing.models';

/**
 * Records what a supplier actually brought against an approved order: the day, the supplier's own delivery receipt number, and how
 * much of each line arrived. Every line that still has something to receive starts at what is left, the usual case of the whole
 * rest arriving at once; the person changes it to what really came and leaves out a line by entering zero. A delivery moves no
 * money, and the server refuses more than is left, so this dialog only asks and shows what the server answers.
 */
@Component({
  selector: 'app-record-delivery-dialog',
  imports: [
    ReactiveFormsModule,
    Dialog,
    Button,
    Alert,
    ApiErrorAlert,
    FormField,
    Control,
    NumberInput,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-dialog title="Record a delivery" [locked]="saving()" (closed)="dismissed.emit()">
      <div class="stack">
        <p class="text-muted">
          What the supplier brought for {{ order().number }}. This does not change what is
          committed; that falls when an invoice is matched.
        </p>
        @if (error(); as problem) {
          <app-api-error-alert
            [error]="problem"
            [extra]="extra()"
            [allowRetry]="problem.isConflict"
            reloadLabel="Reload order"
            (reload)="reloaded.emit()"
          />
        }
        @if (nothingEntered()) {
          <app-alert tone="danger" title="Nothing to record">
            Enter how much arrived for at least one line.
          </app-alert>
        }
        <form id="delivery-form" class="form" [formGroup]="form" (ngSubmit)="submit()" #formElement>
          <app-form-field
            label="Date it arrived"
            [control]="form.controls.receivedDate"
            requiredMessage="Choose the day the delivery arrived."
          >
            <input appControl type="date" formControlName="receivedDate" [max]="today" />
          </app-form-field>
          <app-form-field
            label="Supplier's delivery receipt number"
            hint="The number printed on their delivery receipt. It stops the same delivery being recorded twice."
            [optional]="true"
            [control]="form.controls.deliveryReceiptNumber"
          >
            <input
              appControl
              type="text"
              formControlName="deliveryReceiptNumber"
              maxlength="60"
              autocomplete="off"
            />
          </app-form-field>

          <div class="quantities" formGroupName="quantities">
            @for (line of lines(); track line.id) {
              <app-form-field
                [label]="'Line ' + line.lineNumber + ': ' + line.description"
                [hint]="line.hint"
                [control]="quantity(line.id)"
              >
                <app-number-input
                  [formControlName]="line.id"
                  [decimals]="4"
                  [unit]="line.unit"
                  (input)="nothingEntered.set(false)"
                />
              </app-form-field>
            }
          </div>

          <app-form-field label="Note" [optional]="true" [control]="form.controls.note">
            <textarea appControl formControlName="note" rows="2" maxlength="300"></textarea>
          </app-form-field>
        </form>
      </div>
      <div dialogActions class="dialog-actions">
        <button appButton type="button" [attr.disabled]="saving() ? '' : null" (click)="cancel()">
          Cancel
        </button>
        <button appButton variant="primary" type="submit" form="delivery-form" [busy]="saving()">
          Record delivery
        </button>
      </div>
    </app-dialog>
  `,
  styles: `
    .quantities {
      display: grid;
      gap: var(--space-3);
    }
  `,
})
export class RecordDeliveryDialog implements OnInit {
  readonly order = input.required<PurchaseOrder>();

  /** The delivery was recorded; the order comes back as the server now shows it. */
  readonly saved = output<PurchaseOrder>();
  readonly dismissed = output<void>();
  /** The order changed under the person: the page should refetch it. */
  readonly reloaded = output<void>();

  private readonly api = inject(PurchasingApi);
  private readonly session = inject(SessionState);
  private readonly dialog = viewChild.required(Dialog);
  private readonly formElement = viewChild<ElementRef<HTMLFormElement>>('formElement');
  private readonly focusInvalid = injectFocusFirstInvalid();
  private readonly requestId = new ClientRequestId();

  protected readonly today = todayIn(this.session.timeZoneId());
  protected readonly form = new FormGroup({
    receivedDate: new FormControl(this.today, {
      nonNullable: true,
      validators: [Validators.required],
    }),
    deliveryReceiptNumber: new FormControl('', {
      nonNullable: true,
      validators: [Validators.maxLength(60)],
    }),
    quantities: new FormGroup<Record<string, FormControl<number | null>>>({}),
    note: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(300)] }),
  });
  protected readonly saving = signal(false);
  protected readonly error = signal<ApiError | null>(null);
  protected readonly extra = signal<readonly string[]>([]);
  protected readonly nothingEntered = signal(false);

  /** The lines that still have something to receive, each with a plain note of where it stands. */
  protected readonly lines = computed(() =>
    this.order()
      .lines.filter((line) => line.toReceiveQuantity > 0)
      .map((line) => ({
        id: line.id,
        lineNumber: line.lineNumber,
        description: line.description,
        unit: line.unit,
        hint:
          line.receivedQuantity > 0
            ? `Ordered ${formatQuantity(line.quantity)}, received ${formatQuantity(line.receivedQuantity)}, ${formatQuantity(line.toReceiveQuantity)} left.`
            : `Ordered ${formatQuantity(line.quantity)}.`,
      })),
  );

  ngOnInit(): void {
    for (const line of this.order().lines.filter((item) => item.toReceiveQuantity > 0)) {
      this.form.controls.quantities.addControl(
        line.id,
        new FormControl<number | null>(line.toReceiveQuantity, [Validators.min(0)]),
      );
    }
  }

  protected quantity(lineId: string): FormControl<number | null> {
    return this.form.controls.quantities.controls[lineId]!;
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
    const lines = Object.entries(value.quantities)
      .filter(([, quantity]) => (quantity ?? 0) > 0)
      .map(([purchaseOrderLineId, quantity]) => ({
        purchaseOrderLineId,
        quantity: quantity ?? 0,
      }));
    if (lines.length === 0) {
      this.nothingEntered.set(true);
      return;
    }

    this.saving.set(true);
    this.error.set(null);
    this.extra.set([]);
    this.nothingEntered.set(false);
    try {
      const saved = await firstValueFrom(
        this.api.recordDelivery(
          this.order().id,
          {
            receivedDate: value.receivedDate,
            deliveryReceiptNumber: value.deliveryReceiptNumber.trim() || null,
            note: value.note.trim() || null,
            lines,
          },
          this.requestId.current,
        ),
      );
      this.requestId.renew();
      this.saved.emit(saved);
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
