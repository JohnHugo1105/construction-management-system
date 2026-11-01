import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { ApiError, asApiError } from '../../core/api/api-error';
import { ApiErrorAlert } from '../../shared/ui/api-error-alert/api-error-alert';
import { Button } from '../../shared/ui/button/button';
import { Dialog } from '../../shared/ui/dialog/dialog';
import { PayrollRun } from './payroll.models';
import { StatutoryApi } from './statutory-api';

/**
 * Works a draft's contributions and tax out again from the tables, the workers' details and the pay runs a month as they are
 * now. Says plainly that adjusted amounts go back to the table's, and that deductions typed by hand stay.
 */
@Component({
  selector: 'app-recalculate-statutory-dialog',
  imports: [Dialog, Button, ApiErrorAlert],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-dialog
      title="Recalculate contributions and tax?"
      [locked]="saving()"
      (closed)="dismissed.emit()"
    >
      <div class="stack">
        <p>
          SSS, PhilHealth, Pag-IBIG and withholding tax are worked out again from the tables and
          worker details as they are now. Deductions typed by hand stay.
        </p>
        @if (adjustedCount() > 0) {
          <p>
            <strong>{{
              adjustedCount() === 1
                ? '1 adjusted amount goes'
                : adjustedCount() + ' adjusted amounts go'
            }}</strong>
            back to what the table gives.
          </p>
        }
        @if (error(); as problem) {
          <app-api-error-alert
            [error]="problem"
            [allowRetry]="problem.status === 409"
            reloadLabel="Reload payroll"
            (reload)="reloaded.emit()"
          />
        }
      </div>
      <div dialogActions class="dialog-actions">
        <button appButton type="button" [attr.disabled]="saving() ? '' : null" (click)="cancel()">
          Cancel
        </button>
        <button appButton variant="primary" type="button" [busy]="saving()" (click)="submit()">
          Recalculate
        </button>
      </div>
    </app-dialog>
  `,
})
export class RecalculateStatutoryDialog {
  readonly run = input.required<PayrollRun>();

  readonly recalculated = output<PayrollRun>();
  readonly dismissed = output<void>();
  readonly reloaded = output<void>();

  private readonly api = inject(StatutoryApi);
  private readonly dialog = viewChild.required(Dialog);

  protected readonly adjustedCount = computed(
    () =>
      this.run()
        .lines.flatMap((line) => line.deductions)
        .filter((item) => item.isAdjusted).length,
  );
  protected readonly saving = signal(false);
  protected readonly error = signal<ApiError | null>(null);

  protected cancel(): void {
    this.dialog().dismiss();
  }

  protected async submit(): Promise<void> {
    if (this.saving()) return;
    this.saving.set(true);
    this.error.set(null);
    try {
      const run = this.run();
      this.recalculated.emit(await firstValueFrom(this.api.recalculate(run.id, run.version)));
    } catch (failure) {
      this.error.set(asApiError(failure));
    } finally {
      this.saving.set(false);
    }
  }
}
