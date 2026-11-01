import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  inject,
  input,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { ApiError, asApiError } from '../../core/api/api-error';
import { ApiErrorAlert } from '../../shared/ui/api-error-alert/api-error-alert';
import { Button } from '../../shared/ui/button/button';
import { Dialog } from '../../shared/ui/dialog/dialog';
import { WorkforceApi } from './workforce-api';
import { WorkerDetail, WorkerStatutory } from './workforce.models';

/** A new worker pays all three funds and is taxed. */
export const DEFAULT_STATUTORY: WorkerStatutory = {
  sssMember: true,
  philHealthMember: true,
  pagIbigMember: true,
  taxExempt: false,
};

/** The four choices, worded for a person, in the order payroll takes them. */
export const STATUTORY_CHOICES: readonly {
  readonly key: keyof WorkerStatutory;
  readonly label: string;
  readonly hint: string;
}[] = [
  { key: 'sssMember', label: 'Pays SSS', hint: '' },
  { key: 'philHealthMember', label: 'Pays PhilHealth', hint: '' },
  { key: 'pagIbigMember', label: 'Pays Pag-IBIG', hint: '' },
  {
    key: 'taxExempt',
    label: 'Exempt from withholding tax',
    hint: 'For example a minimum-wage earner.',
  },
];

/** "SSS, PhilHealth and Pag-IBIG; tax withheld", or "No contributions; exempt from tax". */
export function statutoryWords(flags: WorkerStatutory): string {
  const funds = [
    flags.sssMember ? 'SSS' : null,
    flags.philHealthMember ? 'PhilHealth' : null,
    flags.pagIbigMember ? 'Pag-IBIG' : null,
  ].filter((name): name is string => name !== null);
  const paid =
    funds.length === 0
      ? 'No contributions'
      : funds.length === 1
        ? funds[0]
        : `${funds.slice(0, -1).join(', ')} and ${funds[funds.length - 1]}`;
  return `${paid}; ${flags.taxExempt ? 'exempt from tax' : 'tax withheld'}`;
}

/** Changes which contributions a worker pays. Payrolls already prepared keep their amounts until recalculated. */
@Component({
  selector: 'app-worker-statutory-dialog',
  imports: [ReactiveFormsModule, Dialog, Button, ApiErrorAlert],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-dialog title="Contributions and tax" [locked]="saving()" (closed)="dismissed.emit()">
      <div class="stack">
        <p class="text-small text-muted">
          Applies to payrolls prepared or recalculated from now on.
        </p>
        @if (error(); as problem) {
          <app-api-error-alert
            [error]="problem"
            [allowRetry]="problem.status === 409"
            reloadLabel="Reload worker"
            (reload)="reloaded.emit()"
          />
        }
        <form id="statutory-form" [formGroup]="form" (ngSubmit)="submit()">
          <fieldset class="choice-list">
            <legend class="visually-hidden">
              Contributions and tax for {{ worker().fullName }}
            </legend>
            @for (choice of choices; track choice.key) {
              <label class="choice">
                <input type="checkbox" [formControlName]="choice.key" />
                <span class="choice__text">
                  <span class="choice__label">{{ choice.label }}</span>
                  @if (choice.hint) {
                    <span class="choice__hint">{{ choice.hint }}</span>
                  }
                </span>
              </label>
            }
          </fieldset>
        </form>
      </div>
      <div dialogActions class="dialog-actions">
        <button appButton type="button" [attr.disabled]="saving() ? '' : null" (click)="cancel()">
          Cancel
        </button>
        <button appButton variant="primary" type="submit" form="statutory-form" [busy]="saving()">
          Save contributions
        </button>
      </div>
    </app-dialog>
  `,
  styles: `
    fieldset {
      margin: 0;
      padding: 0;
      border: 0;
    }
  `,
})
export class WorkerStatutoryDialog implements OnInit {
  readonly worker = input.required<WorkerDetail>();
  readonly changed = output<WorkerDetail>();
  readonly dismissed = output<void>();
  readonly reloaded = output<void>();

  private readonly api = inject(WorkforceApi);
  private readonly dialog = viewChild.required(Dialog);

  protected readonly choices = STATUTORY_CHOICES;
  protected readonly form = new FormGroup({
    sssMember: new FormControl(true, { nonNullable: true }),
    philHealthMember: new FormControl(true, { nonNullable: true }),
    pagIbigMember: new FormControl(true, { nonNullable: true }),
    taxExempt: new FormControl(false, { nonNullable: true }),
  });
  protected readonly saving = signal(false);
  protected readonly error = signal<ApiError | null>(null);

  ngOnInit(): void {
    this.form.reset(this.worker().statutory ?? DEFAULT_STATUTORY);
  }

  protected cancel(): void {
    this.dialog().dismiss();
  }

  protected async submit(): Promise<void> {
    if (this.saving()) return;
    this.saving.set(true);
    this.error.set(null);
    try {
      const worker = this.worker();
      this.changed.emit(
        await firstValueFrom(
          this.api.setStatutory(worker.id, this.form.getRawValue(), worker.version),
        ),
      );
    } catch (failure) {
      this.error.set(asApiError(failure));
    } finally {
      this.saving.set(false);
    }
  }
}
