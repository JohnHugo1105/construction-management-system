import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  OnInit,
  ElementRef,
  computed,
  inject,
  input,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Subscription, debounceTime, firstValueFrom, merge } from 'rxjs';
import { ApiError, asApiError } from '../../core/api/api-error';
import { Permission } from '../../core/session/permissions';
import { SessionState } from '../../core/session/session-state';
import { injectFocusFirstInvalid } from '../../shared/forms/server-errors';
import { formatDate, todayIn } from '../../shared/presentation/business-time';
import { formatMoney } from '../../shared/presentation/money';
import { formatBps } from '../../shared/presentation/percent';
import { ApiErrorAlert } from '../../shared/ui/api-error-alert/api-error-alert';
import { Button } from '../../shared/ui/button/button';
import { Dialog } from '../../shared/ui/dialog/dialog';
import { Control } from '../../shared/ui/form-field/control';
import { FormField } from '../../shared/ui/form-field/form-field';
import { MoneyInput } from '../../shared/ui/inputs/money-input';
import { ClientRequestId } from '../costing/client-request-id';
import { describeFormFailure } from '../workforce/form-failure';
import { BillingApi } from './billing-api';
import { percentCompleteFrom } from './billing-rules';
import { Bill, BillKind, BillPreview } from './billing.models';

/** What the form needs to know about the project. Every figure is the server's. */
export interface BillFormContext {
  readonly projectId: string;
  readonly contractPriceMinor: number | null;
  readonly billedToDateMinor: number;
  readonly retentionHeldMinor: number;
}

export interface BreakdownRow {
  readonly label: string;
  readonly amount: string;
  readonly total?: boolean;
}

/** The server's breakdown of a progress bill, as rows to print. Nothing is added or subtracted here. */
export function breakdownRows(figures: BillPreview | Bill, currency: string): BreakdownRow[] {
  const money = (minor: number): string => formatMoney(minor, currency);
  const rows: BreakdownRow[] = [
    { label: 'This bill (VAT included)', amount: money(figures.grossMinor) },
    { label: `VAT ${formatBps(figures.vatRateBps)}, included`, amount: money(figures.vatMinor) },
  ];
  if (figures.retentionBps > 0)
    rows.push({
      label: `Retention ${formatBps(figures.retentionBps)}, held by the client`,
      amount: `-${money(figures.retentionMinor)}`,
    });
  if (figures.withholdingBps > 0)
    rows.push({
      label: `Withholding tax ${formatBps(figures.withholdingBps)} of the amount before VAT`,
      amount: `-${money(figures.withholdingMinor)}`,
    });
  rows.push({ label: 'The client pays', amount: money(figures.netReceivableMinor), total: true });
  return rows;
}

/**
 * Prepares a bill, or changes a draft. A progress bill asks for the value of all work completed to date (VAT included, like the
 * contract price) and shows the server's breakdown before saving; a retention release asks for the amount to give back. When the
 * person can read progress, the project's percent complete is offered as a hint (that feature may not answer; then there is no
 * hint). Saving makes a draft: issuing it is a separate step for an approver.
 */
@Component({
  selector: 'app-bill-form-dialog',
  imports: [ReactiveFormsModule, Dialog, Button, FormField, Control, MoneyInput, ApiErrorAlert],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-dialog [title]="title()" [locked]="saving()" (closed)="dismissed.emit()">
      <div class="stack">
        @if (error(); as problem) {
          <app-api-error-alert
            [error]="problem"
            [extra]="extra()"
            [allowRetry]="problem.status === 409"
            reloadLabel="Reload"
            (reload)="reloaded.emit()"
          />
        }

        <form id="bill-form" class="form" [formGroup]="form" (ngSubmit)="submit()" #formElement>
          @if (isProgress()) {
            <app-form-field
              label="Work completed to date, VAT included"
              [hint]="workHint()"
              [control]="form.controls.amount"
              requiredMessage="Enter the value of all work completed so far."
            >
              <app-money-input formControlName="amount" data-autofocus />
            </app-form-field>
            @if (suggestion(); as text) {
              <p class="suggestion text-small">{{ text }}</p>
            }
          } @else {
            <app-form-field
              label="Retention to release"
              [hint]="releaseHint()"
              [control]="form.controls.amount"
              requiredMessage="Enter the amount of retention to release."
            >
              <app-money-input formControlName="amount" data-autofocus />
            </app-form-field>
          }

          <app-form-field
            label="Billing date"
            hint="Up to today. Payment is due after the project's payment terms."
            [control]="form.controls.billingDate"
            requiredMessage="Choose the billing date."
          >
            <input appControl type="date" formControlName="billingDate" [max]="today" />
          </app-form-field>

          <app-form-field
            label="Description"
            [control]="form.controls.description"
            requiredMessage="Describe the bill, for example the work it covers."
          >
            <input appControl type="text" formControlName="description" maxlength="200" />
          </app-form-field>
        </form>

        @if (isProgress()) {
          <section class="breakdown" aria-live="polite" aria-label="What this bill comes to">
            @if (preview(); as figures) {
              <h3 class="breakdown__title">What this bill comes to</h3>
              <dl class="breakdown__rows">
                @for (row of previewRows(); track row.label) {
                  <div class="breakdown__row" [class.breakdown__row--total]="row.total">
                    <dt>{{ row.label }}</dt>
                    <dd class="money">{{ row.amount }}</dd>
                  </div>
                }
              </dl>
              <p class="text-muted text-small">Due {{ dueDate(figures.dueDate) }}.</p>
            } @else if (previewing()) {
              <p class="text-muted text-small">Working out the bill…</p>
            } @else {
              <p class="text-muted text-small">
                Enter the work completed to date to see what this bill comes to.
              </p>
            }
          </section>
        }
      </div>

      <div dialogActions class="dialog-actions">
        <button appButton type="button" [attr.disabled]="saving() ? '' : null" (click)="cancel()">
          Cancel
        </button>
        <button appButton variant="primary" type="submit" form="bill-form" [busy]="saving()">
          {{ bill() ? 'Save draft' : 'Save as draft' }}
        </button>
      </div>
    </app-dialog>
  `,
  styles: `
    .suggestion {
      margin-top: calc(-1 * var(--space-2));
      color: var(--color-text-muted);
    }

    .breakdown {
      padding: var(--space-3) var(--space-4);
      border: 1px solid var(--color-border);
      border-radius: var(--radius-lg);
      background: var(--color-surface-2);
    }

    .breakdown__title {
      margin-bottom: var(--space-2);
      font-size: var(--text-base);
    }

    .breakdown__rows {
      display: grid;
      gap: var(--space-1);
      margin: 0 0 var(--space-2);
    }

    .breakdown__row {
      display: flex;
      align-items: baseline;
      justify-content: space-between;
      gap: var(--space-3);

      dt {
        min-width: 0;
        font-size: var(--text-sm);
        color: var(--color-text-muted);
      }

      dd {
        margin: 0;
        white-space: nowrap;
      }
    }

    .breakdown__row--total {
      padding-top: var(--space-1);
      border-top: 1px solid var(--color-border);

      dt {
        color: var(--color-text);
        font-weight: 600;
      }

      dd {
        font-weight: 700;
      }
    }
  `,
})
export class BillFormDialog implements OnInit {
  readonly context = input.required<BillFormContext>();
  /** Progress or retention release, for a new bill. */
  readonly kind = input<BillKind>('Progress');
  /** The draft to change; absent for a new bill. */
  readonly bill = input<Bill | null>(null);

  readonly saved = output<Bill>();
  readonly dismissed = output<void>();
  readonly reloaded = output<void>();

  private readonly api = inject(BillingApi);
  private readonly session = inject(SessionState);
  private readonly dialog = viewChild.required(Dialog);
  private readonly formElement = viewChild<ElementRef<HTMLFormElement>>('formElement');
  private readonly focusInvalid = injectFocusFirstInvalid();
  private readonly requestId = new ClientRequestId();
  private previewSubscription: Subscription | null = null;
  private previewRequest = 0;

  protected readonly today = todayIn(this.session.timeZoneId());
  protected readonly form = new FormGroup({
    amount: new FormControl<number | null>(null, {
      validators: [Validators.required, Validators.min(1)],
    }),
    billingDate: new FormControl(this.today, {
      nonNullable: true,
      validators: [Validators.required],
    }),
    description: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(200)],
    }),
  });

  protected readonly saving = signal(false);
  protected readonly error = signal<ApiError | null>(null);
  protected readonly extra = signal<readonly string[]>([]);
  protected readonly preview = signal<BillPreview | null>(null);
  protected readonly previewing = signal(false);
  private readonly percentComplete = signal<number | null>(null);

  protected readonly effectiveKind = computed<BillKind>(() => this.bill()?.kind ?? this.kind());
  protected readonly isProgress = computed(() => this.effectiveKind() === 'Progress');
  protected readonly title = computed(() => {
    if (this.bill()) return 'Change draft bill';
    return this.isProgress() ? 'New progress bill' : 'Release retention';
  });

  private readonly currency = computed(() => this.session.currencyCode());
  protected readonly workHint = computed(() => {
    const context = this.context();
    const money = (minor: number): string => formatMoney(minor, this.currency());
    const price =
      context.contractPriceMinor === null
        ? ''
        : ` of the ${money(context.contractPriceMinor)} contract`;
    return `Already billed: ${money(context.billedToDateMinor)}${price}. This bill is the difference.`;
  });
  protected readonly releaseHint = computed(
    () =>
      `The client still holds ${formatMoney(this.context().retentionHeldMinor, this.currency())} in retention.`,
  );
  protected readonly suggestion = computed(() => {
    const bps = this.percentComplete();
    return bps === null ? null : `Suggested from progress: the work is ${formatBps(bps)} complete.`;
  });
  protected readonly previewRows = computed(() => {
    const figures = this.preview();
    return figures ? breakdownRows(figures, this.currency()) : [];
  });

  constructor() {
    merge(this.form.controls.amount.valueChanges, this.form.controls.billingDate.valueChanges)
      .pipe(debounceTime(350), takeUntilDestroyed())
      .subscribe(() => this.refreshPreview());
    inject(DestroyRef).onDestroy(() => this.previewSubscription?.unsubscribe());
  }

  ngOnInit(): void {
    const bill = this.bill();
    if (bill) {
      this.form.setValue({
        amount: (bill.kind === 'Progress' ? bill.cumulativeWorkMinor : bill.grossMinor) ?? null,
        billingDate: bill.billingDate,
        description: bill.description,
      });
      this.refreshPreview();
    } else {
      this.form.controls.description.setValue(
        this.isProgress() ? 'Progress billing' : 'Release of retention',
      );
    }
    if (this.isProgress() && this.session.can(Permission.progressRead)) void this.loadSuggestion();
  }

  protected cancel(): void {
    this.dialog().dismiss();
  }

  protected dueDate(date: string): string {
    return formatDate(date);
  }

  protected async submit(): Promise<void> {
    if (this.saving()) return;
    this.form.markAllAsTouched();
    if (this.form.invalid) {
      this.focusInvalid(this.formElement()?.nativeElement);
      return;
    }
    const value = this.form.getRawValue();
    const progress = this.isProgress();
    this.saving.set(true);
    this.error.set(null);
    this.extra.set([]);
    try {
      const bill = this.bill();
      const saved = bill
        ? await firstValueFrom(
            this.api.update(bill.id, {
              billingDate: value.billingDate,
              description: value.description.trim(),
              cumulativeWorkMinor: progress ? value.amount : null,
              amountMinor: progress ? null : value.amount,
              version: bill.version,
            }),
          )
        : await firstValueFrom(
            this.api.create(this.context().projectId, {
              kind: this.effectiveKind(),
              billingDate: value.billingDate,
              description: value.description.trim(),
              cumulativeWorkMinor: progress ? value.amount : null,
              amountMinor: progress ? null : value.amount,
              clientRequestId: this.requestId.current,
            }),
          );
      this.requestId.renew();
      this.saved.emit(saved);
    } catch (failure) {
      const outcome = describeFormFailure(this.form, asApiError(failure), {
        cumulativeWorkMinor: 'amount',
        amountMinor: 'amount',
      });
      this.error.set(outcome.alert);
      this.extra.set(outcome.extra);
      this.focusInvalid(this.formElement()?.nativeElement);
    } finally {
      this.saving.set(false);
    }
  }

  /** Asks the server what the bill comes to. Only the latest answer is shown; a refusal shows under the field instead. */
  private refreshPreview(): void {
    if (!this.isProgress()) return;
    const amount = this.form.controls.amount.value;
    const date = this.form.controls.billingDate.value;
    this.previewSubscription?.unsubscribe();
    if (amount === null || amount <= 0 || !date) {
      this.preview.set(null);
      this.previewing.set(false);
      return;
    }
    const request = ++this.previewRequest;
    this.previewing.set(true);
    this.previewSubscription = this.api.preview(this.context().projectId, amount, date).subscribe({
      next: (figures) => {
        if (request !== this.previewRequest) return;
        this.preview.set(figures);
        this.previewing.set(false);
      },
      error: (failure: unknown) => {
        if (request !== this.previewRequest) return;
        this.preview.set(null);
        this.previewing.set(false);
        const problem = asApiError(failure);
        const message = problem.firstError('cumulativeWorkMinor');
        if (message) {
          this.form.controls.amount.setErrors({ server: message });
          this.form.controls.amount.markAsTouched();
        }
      },
    });
  }

  private async loadSuggestion(): Promise<void> {
    try {
      this.percentComplete.set(
        percentCompleteFrom(await firstValueFrom(this.api.progress(this.context().projectId))),
      );
    } catch {
      // Progress is a separate feature that may not be ready or allowed: no hint, nothing else changes.
      this.percentComplete.set(null);
    }
  }
}
