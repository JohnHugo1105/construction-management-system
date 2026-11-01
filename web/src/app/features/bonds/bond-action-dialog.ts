import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  computed,
  OnInit,
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
import { addDays, formatDate, todayIn } from '../../shared/presentation/business-time';
import { ApiErrorAlert } from '../../shared/ui/api-error-alert/api-error-alert';
import { Button } from '../../shared/ui/button/button';
import { Dialog } from '../../shared/ui/dialog/dialog';
import { Control } from '../../shared/ui/form-field/control';
import { FormField } from '../../shared/ui/form-field/form-field';
import { describeFormFailure } from '../workforce/form-failure';
import { BondsApi } from './bonds-api';
import { bondTitle } from './bonds-presentation';
import { Bond } from './bonds.models';

export type BondStep = 'extend' | 'release' | 'call' | 'void';

interface StepWords {
  readonly title: string;
  readonly intro: string;
  readonly confirm: string;
  readonly dateLabel: string | null;
  readonly dateRequired: string;
  readonly noteLabel: string;
  readonly noteRequired: string | null;
  readonly noteHint: string | null;
}

/**
 * The one-field steps of a bond: extend the valid-until date, record that it was released or called, or void it with a reason. Each
 * asks for the date (when it has one) and a note, sends the version the person opened, and shows a refusal in the server's words with a
 * way to reload. Releasing, calling and voiding are final, and the dialog says so before it asks.
 */
@Component({
  selector: 'app-bond-action-dialog',
  imports: [ReactiveFormsModule, Dialog, Button, FormField, Control, ApiErrorAlert],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-dialog [title]="words().title" [locked]="saving()" (closed)="dismissed.emit()">
      <div class="stack">
        <p>{{ words().intro }}</p>
        @if (error(); as problem) {
          <app-api-error-alert
            [error]="problem"
            [extra]="extra()"
            [allowRetry]="problem.status === 409"
            reloadLabel="Reload bond"
            (reload)="reloaded.emit()"
          />
        }
        <form
          id="bond-step-form"
          class="form"
          [formGroup]="form"
          (ngSubmit)="submit()"
          #formElement
        >
          @if (words().dateLabel; as label) {
            <app-form-field
              [label]="label"
              [hint]="dateHint()"
              [control]="form.controls.date"
              [requiredMessage]="words().dateRequired"
            >
              <input
                appControl
                type="date"
                formControlName="date"
                [min]="minDate()"
                [max]="maxDate()"
                data-autofocus
              />
            </app-form-field>
          }
          <app-form-field
            [label]="words().noteLabel"
            [optional]="words().noteRequired === null"
            [hint]="words().noteHint ?? ''"
            [control]="form.controls.note"
            [requiredMessage]="words().noteRequired ?? undefined"
          >
            <textarea appControl formControlName="note" rows="3" maxlength="500"></textarea>
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
        <button
          appButton
          [variant]="step() === 'void' ? 'danger' : 'primary'"
          type="submit"
          form="bond-step-form"
          [busy]="saving()"
        >
          {{ words().confirm }}
        </button>
      </div>
    </app-dialog>
  `,
})
export class BondActionDialog implements OnInit {
  readonly bond = input.required<Bond>();
  readonly step = input.required<BondStep>();

  readonly done = output<Bond>();
  readonly dismissed = output<void>();
  readonly reloaded = output<void>();

  private readonly api = inject(BondsApi);
  private readonly session = inject(SessionState);
  protected readonly dialog = viewChild.required(Dialog);
  private readonly formElement = viewChild<ElementRef<HTMLFormElement>>('formElement');
  private readonly focusInvalid = injectFocusFirstInvalid();
  private readonly today = todayIn(this.session.timeZoneId());

  protected readonly form = new FormGroup({
    date: new FormControl('', { nonNullable: true }),
    note: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(500)] }),
  });
  protected readonly saving = signal(false);
  protected readonly error = signal<ApiError | null>(null);
  protected readonly extra = signal<readonly string[]>([]);

  ngOnInit(): void {
    const step = this.step();
    const date = this.form.controls.date;
    const note = this.form.controls.note;
    if (step === 'release' || step === 'call') date.setValue(this.today);
    if (step !== 'void') date.addValidators(Validators.required);
    if (step === 'call' || step === 'void') note.addValidators(Validators.required);
    date.updateValueAndValidity();
    note.updateValueAndValidity();
  }

  protected readonly words = computed<StepWords>(() => {
    const bond = this.bond();
    const name = bondTitle(bond);
    switch (this.step()) {
      case 'extend':
        return {
          title: 'Extend this bond',
          intro: `Move the valid-until date of ${name} later, for example when the bank or surety renews it. The change is kept in its history.`,
          confirm: 'Extend bond',
          dateLabel: 'New valid-until date',
          dateRequired: 'Choose the new valid-until date.',
          noteLabel: 'Note',
          noteRequired: null,
          noteHint: 'For example, who renewed it and for how long.',
        };
      case 'release':
        return {
          title: 'Release this bond?',
          intro: `Record that ${name} was returned or is no longer needed. It stays on record as released and can't be changed afterwards.`,
          confirm: 'Release bond',
          dateLabel: 'Date released',
          dateRequired: 'Choose the date it was released.',
          noteLabel: 'Note',
          noteRequired: null,
          noteHint: null,
        };
      case 'call':
        return {
          title: 'Mark this bond as called?',
          intro: `Record that the beneficiary claimed against ${name}. It stays on record as called and can't be changed afterwards.`,
          confirm: 'Mark as called',
          dateLabel: 'Date called',
          dateRequired: 'Choose the date it was called.',
          noteLabel: 'What was claimed',
          noteRequired: 'Say what was claimed.',
          noteHint: null,
        };
      default:
        return {
          title: 'Void this bond?',
          intro: `Use this only if ${name} was entered by mistake. It stays on record as voided, with your reason, and counts nowhere.`,
          confirm: 'Void bond',
          dateLabel: null,
          dateRequired: '',
          noteLabel: 'Why is it being voided?',
          noteRequired: 'Give a reason.',
          noteHint: null,
        };
    }
  });

  /** An extension must go later than today's valid-until date; a release or call is not in the future and not before the bond was issued. */
  protected readonly minDate = computed(() =>
    this.step() === 'extend' ? (addDays(this.bond().validUntil, 1) ?? '') : this.bond().issuedOn,
  );
  protected readonly maxDate = computed(() => (this.step() === 'extend' ? '' : this.today));
  protected readonly dateHint = computed(() =>
    this.step() === 'extend' ? `It is valid until ${formatDate(this.bond().validUntil)} now.` : '',
  );

  protected async submit(): Promise<void> {
    if (this.saving()) return;
    this.form.markAllAsTouched();
    if (this.form.invalid) {
      this.focusInvalid(this.formElement()?.nativeElement);
      return;
    }
    const { date, note } = this.form.getRawValue();
    const text = note.trim();
    const bond = this.bond();
    const call = ((): Observable<Bond> => {
      switch (this.step()) {
        case 'extend':
          return this.api.extend(bond.id, date, text || null, bond.version);
        case 'release':
          return this.api.release(bond.id, date, text || null, bond.version);
        case 'call':
          return this.api.markCalled(bond.id, date, text, bond.version);
        default:
          return this.api.voidBond(bond.id, text, bond.version);
      }
    })();
    this.saving.set(true);
    this.error.set(null);
    this.extra.set([]);
    try {
      this.done.emit(await firstValueFrom(call));
    } catch (failure) {
      const outcome = describeFormFailure(this.form, asApiError(failure), {
        validUntil: 'date',
        releasedOn: 'date',
        calledOn: 'date',
        reason: 'note',
      });
      this.error.set(outcome.alert);
      this.extra.set(outcome.extra);
      this.focusInvalid(this.formElement()?.nativeElement);
    } finally {
      this.saving.set(false);
    }
  }
}
