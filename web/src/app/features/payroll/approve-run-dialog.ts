import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { ApiError, asApiError } from '../../core/api/api-error';
import { SessionState } from '../../core/session/session-state';
import { formatMoney } from '../../shared/presentation/money';
import { ApiErrorAlert } from '../../shared/ui/api-error-alert/api-error-alert';
import { ConfirmDialog } from '../../shared/ui/dialog/confirm-dialog';
import { PayrollApi } from './payroll-api';
import { postingsOf } from './payroll-rules';
import { PayrollRun } from './payroll.models';

/**
 * Asks before approving a payroll, and says what approving does: it locks the attendance and posts the labor cost to each
 * project, with the amount for each, taken from the run the API returned. A refusal (already approved, changed by someone else,
 * a project archived since) is shown here in the API's words with a way to reload, and the dialog stays open.
 */
@Component({
  selector: 'app-approve-run-dialog',
  imports: [ConfirmDialog, ApiErrorAlert],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-confirm-dialog
      title="Approve this payroll?"
      message="Approving locks this attendance and posts the labor cost to:"
      confirmLabel="Approve payroll"
      [busy]="busy()"
      (confirmed)="approve()"
      (cancelled)="dismissed.emit()"
    >
      <ul class="postings" aria-label="Labor cost to be posted">
        @for (posting of postings(); track posting.key) {
          <li class="postings__row">
            <span class="postings__place">{{ posting.label }}</span>
            <span class="money">{{ posting.amount }}</span>
          </li>
        }
        <li class="postings__row postings__row--total">
          <span>Total</span>
          <span class="money">{{ total() }}</span>
        </li>
      </ul>
      <p class="text-muted text-small postings__note">
        Nobody can change this attendance until the payroll is voided.
      </p>

      @if (error(); as problem) {
        <app-api-error-alert
          [error]="problem"
          [allowRetry]="problem.status === 409"
          reloadLabel="Reload payroll"
          (reload)="reloaded.emit()"
        />
      }
    </app-confirm-dialog>
  `,
  styles: `
    .postings {
      display: grid;
      gap: var(--space-2);
      margin: 0 0 var(--space-3);
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

    .postings__row--total {
      border-bottom: 0;
      font-weight: 650;
    }

    .postings__note {
      margin-bottom: var(--space-3);
    }
  `,
})
export class ApproveRunDialog {
  readonly run = input.required<PayrollRun>();

  /** The run the API returned once it is approved. */
  readonly approved = output<PayrollRun>();
  readonly dismissed = output<void>();
  /** The person chose to reload after a refusal. */
  readonly reloaded = output<void>();

  private readonly api = inject(PayrollApi);
  private readonly session = inject(SessionState);

  protected readonly busy = signal(false);
  protected readonly error = signal<ApiError | null>(null);

  protected readonly postings = computed(() =>
    postingsOf(this.run()).map((posting) => ({
      key: posting.key,
      label: posting.label,
      amount: formatMoney(posting.amountMinor, this.session.currencyCode()),
    })),
  );
  protected readonly total = computed(() =>
    formatMoney(this.run().totalGrossMinor, this.session.currencyCode()),
  );

  protected async approve(): Promise<void> {
    if (this.busy()) return;
    this.busy.set(true);
    this.error.set(null);
    try {
      const run = this.run();
      this.approved.emit(await firstValueFrom(this.api.approve(run.id, run.version)));
    } catch (failure) {
      this.error.set(asApiError(failure));
    } finally {
      this.busy.set(false);
    }
  }
}
