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
import { rxResource, toSignal } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { debounceTime, firstValueFrom } from 'rxjs';
import { ApiError, asApiError } from '../../core/api/api-error';
import { SessionState } from '../../core/session/session-state';
import { injectFocusFirstInvalid } from '../../shared/forms/server-errors';
import { todayIn } from '../../shared/presentation/business-time';
import { formatMoney } from '../../shared/presentation/money';
import { ApiErrorAlert } from '../../shared/ui/api-error-alert/api-error-alert';
import { Button } from '../../shared/ui/button/button';
import { Dialog } from '../../shared/ui/dialog/dialog';
import { Control } from '../../shared/ui/form-field/control';
import { FormField } from '../../shared/ui/form-field/form-field';
import { MoneyInput } from '../../shared/ui/inputs/money-input';
import { ClientRequestId } from '../costing/client-request-id';
import { describeFormFailure } from '../workforce/form-failure';
import { VAT_WORDS } from './subcontract-presentation';
import { SubcontractsApi } from './subcontracts-api';
import { ClaimPreview, Subcontract, SubcontractClaim } from './subcontracts.models';

/** The breakdown the server returned, as label and amount pairs, in the order a payment certificate reads. */
export function previewLines(preview: ClaimPreview, currency: string, vatWords: string) {
  const money = (minor: number) => formatMoney(minor, currency);
  return [
    { label: `This claim (${vatWords})`, amount: money(preview.thisClaimBaseMinor) },
    { label: 'VAT', amount: money(preview.vatMinor) },
    { label: 'Total with VAT', amount: money(preview.grossMinor) },
    { label: 'Retention held', amount: `- ${money(preview.retentionMinor)}` },
    { label: 'Withholding tax', amount: `- ${money(preview.withholdingMinor)}` },
    { label: 'To pay on this claim', amount: money(preview.netPayableMinor), total: true },
  ];
}

/**
 * Drafts a progress claim, or edits the open draft. The person enters the value of all work done to date; the server works out
 * this claim, VAT, retention, withholding and what is payable, and that breakdown is shown before saving. A draft changes no cost:
 * certifying it does.
 */
@Component({
  selector: 'app-claim-dialog',
  imports: [ReactiveFormsModule, Dialog, Button, FormField, Control, MoneyInput, ApiErrorAlert],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-dialog [title]="claim() ? 'Update the draft claim' : 'New claim'" [locked]="saving()" (closed)="dismissed.emit()">
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
        <form id="claim-form" class="form" [formGroup]="form" (ngSubmit)="submit()" #formElement>
          <app-form-field
            label="Work done to date"
            [hint]="hint()"
            [control]="form.controls.cumulativeWorkMinor"
            requiredMessage="Enter the value of all work done so far."
          >
            <app-money-input formControlName="cumulativeWorkMinor" data-autofocus />
          </app-form-field>

          <section class="preview" aria-live="polite" aria-label="What this claim comes to">
            @if (lines(); as rows) {
              <dl class="preview__list">
                @for (row of rows; track row.label) {
                  <div class="preview__row" [class.preview__row--total]="row.total">
                    <dt>{{ row.label }}</dt>
                    <dd class="money">{{ row.amount }}</dd>
                  </div>
                }
              </dl>
            } @else if (previewMessage()) {
              <p class="text-small preview__message">{{ previewMessage() }}</p>
            } @else {
              <p class="text-small text-muted">Enter the work done to date to see what this claim comes to.</p>
            }
          </section>

          <div class="form__row">
            <app-form-field label="Claim date" [control]="form.controls.claimDate" requiredMessage="Choose the claim date.">
              <input appControl type="date" formControlName="claimDate" [max]="today" />
            </app-form-field>
            <app-form-field label="Work up to" [control]="form.controls.periodEnd" requiredMessage="Choose the last day of work claimed.">
              <input appControl type="date" formControlName="periodEnd" [max]="today" />
            </app-form-field>
          </div>
          <app-form-field label="Notes" [optional]="true" [control]="form.controls.notes">
            <textarea appControl formControlName="notes" rows="2" maxlength="300"></textarea>
          </app-form-field>
        </form>
      </div>
      <div dialogActions class="dialog-actions">
        <button appButton type="button" [attr.disabled]="saving() ? '' : null" (click)="dialog().dismiss()">Cancel</button>
        <button appButton variant="primary" type="submit" form="claim-form" [busy]="saving()">
          {{ claim() ? 'Save claim' : 'Save draft claim' }}
        </button>
      </div>
    </app-dialog>
  `,
  styles: `
    .preview {
      padding: var(--space-3) var(--space-4);
      border-radius: var(--radius-md);
      background: var(--color-surface-2);
    }

    .preview__list {
      display: grid;
      gap: var(--space-1);
      margin: 0;
    }

    .preview__row {
      display: flex;
      justify-content: space-between;
      gap: var(--space-3);

      dt {
        color: var(--color-text-muted);
      }

      dd {
        margin: 0;
      }
    }

    .preview__row--total {
      padding-top: var(--space-2);
      border-top: 1px solid var(--color-border);
      font-weight: 650;

      dt {
        color: var(--color-text);
      }
    }

    .preview__message {
      color: var(--color-danger);
    }
  `,
})
export class ClaimDialog {
  readonly subcontract = input.required<Subcontract>();
  /** The open draft to edit; null for a new claim. */
  readonly claim = input<SubcontractClaim | null>(null);

  readonly saved = output<Subcontract>();
  readonly dismissed = output<void>();
  readonly reloaded = output<void>();

  private readonly api = inject(SubcontractsApi);
  private readonly session = inject(SessionState);
  protected readonly dialog = viewChild.required(Dialog);
  private readonly formElement = viewChild<ElementRef<HTMLFormElement>>('formElement');
  private readonly focusInvalid = injectFocusFirstInvalid();
  private readonly requestId = new ClientRequestId();
  protected readonly today = todayIn(this.session.timeZoneId());

  protected readonly form = new FormGroup({
    cumulativeWorkMinor: new FormControl<number | null>(null, { validators: [Validators.required] }),
    claimDate: new FormControl(this.today, { nonNullable: true, validators: [Validators.required] }),
    periodEnd: new FormControl(this.today, { nonNullable: true, validators: [Validators.required] }),
    notes: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(300)] }),
  });

  private readonly typed = toSignal(this.form.controls.cumulativeWorkMinor.valueChanges.pipe(debounceTime(350)), {
    initialValue: null,
  });

  private readonly preview = rxResource({
    params: () => {
      const value = this.typed();
      return value !== null && value > 0 ? value : undefined;
    },
    stream: ({ params }) => this.api.previewClaim(this.subcontract().id, params),
  });

  protected readonly lines = computed(() =>
    this.preview.hasValue() && !this.preview.error()
      ? previewLines(this.preview.value(), this.session.currencyCode(), VAT_WORDS[this.subcontract().vatTreatment])
      : null,
  );

  /** The server's reason the amount can't be claimed (over the sum, not more than already certified). */
  protected readonly previewMessage = computed(() => {
    const failure = this.preview.error();
    if (!failure) return '';
    const problem = asApiError(failure);
    return problem.errors?.['cumulativeWorkMinor']?.[0] ?? problem.detail ?? '';
  });

  protected readonly hint = computed(() => {
    const subcontract = this.subcontract();
    const currency = this.session.currencyCode();
    const words = VAT_WORDS[subcontract.vatTreatment];
    return subcontract.certifiedWorkMinor > 0
      ? `All work so far, ${words}. ${formatMoney(subcontract.certifiedWorkMinor, currency)} is already certified, out of ${formatMoney(subcontract.revisedSumMinor, currency)}.`
      : `All work so far, ${words}, out of ${formatMoney(subcontract.revisedSumMinor, currency)}.`;
  });

  protected readonly saving = signal(false);
  protected readonly error = signal<ApiError | null>(null);
  protected readonly extra = signal<readonly string[]>([]);

  constructor() {
    queueMicrotask(() => {
      const draft = this.claim();
      if (draft)
        this.form.setValue({
          cumulativeWorkMinor: draft.cumulativeWorkMinor,
          claimDate: draft.claimDate,
          periodEnd: draft.periodEnd,
          notes: draft.notes ?? '',
        });
    });
  }

  protected async submit(): Promise<void> {
    if (this.saving()) return;
    this.form.markAllAsTouched();
    if (this.form.invalid) {
      this.focusInvalid(this.formElement()?.nativeElement);
      return;
    }
    const value = this.form.getRawValue();
    const input = { ...value, notes: value.notes.trim() || null };
    const subcontract = this.subcontract();
    const draft = this.claim();
    this.saving.set(true);
    this.error.set(null);
    this.extra.set([]);
    try {
      const saved = draft
        ? await firstValueFrom(this.api.updateClaim(subcontract.id, draft.id, input, subcontract.version))
        : await firstValueFrom(this.api.draftClaim(subcontract.id, input, subcontract.version, this.requestId.current));
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
