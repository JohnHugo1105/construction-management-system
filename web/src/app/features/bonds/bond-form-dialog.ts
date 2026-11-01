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
import { rxResource, toSignal } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { ApiError, asApiError } from '../../core/api/api-error';
import { injectFocusFirstInvalid } from '../../shared/forms/server-errors';
import { ApiErrorAlert } from '../../shared/ui/api-error-alert/api-error-alert';
import { Button } from '../../shared/ui/button/button';
import { Dialog } from '../../shared/ui/dialog/dialog';
import { Control } from '../../shared/ui/form-field/control';
import { FieldGroup } from '../../shared/ui/form-field/field-group';
import { FormField } from '../../shared/ui/form-field/form-field';
import { MoneyInput } from '../../shared/ui/inputs/money-input';
import { ClientRequestId } from '../costing/client-request-id';
import { describeFormFailure } from '../workforce/form-failure';
import { BondsApi } from './bonds-api';
import { DIRECTION_CHOICES, KIND_CHOICES } from './bonds-presentation';
import { Bond, BondDirection, BondKind } from './bonds.models';

/**
 * Adds a bond to a project, or changes the details of an Active one. Adding asks for the project (unless it is already chosen), who gave
 * the bond to whom, the kind, the bank or surety, the bond number, the amount and the two dates; a bond received from a firm can name
 * the firm. Editing keeps the project and direction as recorded: a bond entered the wrong way round is voided and entered again.
 * A bond is only a record: saving it posts no cost, bill or payment.
 */
@Component({
  selector: 'app-bond-form-dialog',
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
      [title]="bond() ? 'Change bond details' : 'Add a bond'"
      [locked]="saving()"
      (closed)="dismissed.emit()"
    >
      <div class="stack">
        @if (error(); as problem) {
          <app-api-error-alert
            [error]="problem"
            [extra]="extra()"
            [allowRetry]="problem.status === 409"
            reloadLabel="Reload bond"
            (reload)="reloaded.emit()"
          />
        }
        <form id="bond-form" class="form" [formGroup]="form" (ngSubmit)="submit()" #formElement>
          @if (!bond()) {
            @if (!projectId()) {
              <app-form-field
                label="Project"
                [control]="form.controls.projectId"
                requiredMessage="Choose the project the bond is for."
              >
                <select appControl formControlName="projectId" data-autofocus>
                  <option value="">Choose a project</option>
                  @for (project of projects(); track project.id) {
                    <option [value]="project.id">{{ project.name }}</option>
                  }
                </select>
              </app-form-field>
            }
            <app-field-group label="Whose bond is it?" [control]="form.controls.direction">
              <div class="choice-list">
                @for (choice of directions; track choice.value) {
                  <label class="choice">
                    <input type="radio" formControlName="direction" [value]="choice.value" />
                    <span class="choice__text">
                      <span class="choice__label">{{ choice.label }}</span>
                      <span class="choice__hint">{{ choice.hint }}</span>
                    </span>
                  </label>
                }
              </div>
            </app-field-group>
          }
          <app-form-field
            label="Kind of bond"
            [control]="form.controls.kind"
            requiredMessage="Choose the kind of bond."
          >
            <select appControl formControlName="kind">
              @for (kind of kinds; track kind.value) {
                <option [value]="kind.value">{{ kind.label }}</option>
              }
            </select>
          </app-form-field>
          <app-form-field
            label="Bank or surety"
            [control]="form.controls.issuerName"
            requiredMessage="Enter the bank or surety that issued it."
          >
            <input appControl formControlName="issuerName" maxlength="120" autocomplete="off" />
          </app-form-field>
          <app-form-field
            label="Bond number"
            [control]="form.controls.bondNumber"
            requiredMessage="Enter the bond number."
          >
            <input appControl formControlName="bondNumber" maxlength="60" autocomplete="off" />
          </app-form-field>
          <app-form-field
            label="Amount"
            hint="What the bond guarantees. It is a record only: it is not added to any cost or bill."
            [control]="form.controls.amountMinor"
            requiredMessage="Enter the bond amount."
          >
            <app-money-input formControlName="amountMinor" />
          </app-form-field>
          <div class="form__row">
            <app-form-field
              label="Issued on"
              [control]="form.controls.issuedOn"
              requiredMessage="Choose the date it was issued."
            >
              <input appControl type="date" formControlName="issuedOn" />
            </app-form-field>
            <app-form-field
              label="Valid until"
              [hint]="bond() ? 'To move it later, use Extend bond.' : ''"
              [control]="form.controls.validUntil"
              requiredMessage="Choose the date it is valid until."
            >
              <input appControl type="date" formControlName="validUntil" />
            </app-form-field>
          </div>
          @if (received()) {
            <app-form-field
              label="Given by"
              [optional]="true"
              hint="The subcontractor or supplier who gave you this bond."
              [control]="form.controls.otherPartyName"
            >
              <input
                appControl
                formControlName="otherPartyName"
                maxlength="120"
                autocomplete="off"
              />
            </app-form-field>
          }
          <app-form-field label="Note" [optional]="true" [control]="form.controls.note">
            <textarea appControl formControlName="note" rows="2" maxlength="500"></textarea>
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
        <button appButton variant="primary" type="submit" form="bond-form" [busy]="saving()">
          {{ bond() ? 'Save changes' : 'Add bond' }}
        </button>
      </div>
    </app-dialog>
  `,
})
export class BondFormDialog implements OnInit {
  /** The bond being changed; null adds a new one. */
  readonly bond = input<Bond | null>(null);
  /** The project a new bond is for, when the page already knows it; null shows a project choice. */
  readonly projectId = input<string | null>(null);

  readonly saved = output<Bond>();
  readonly dismissed = output<void>();
  readonly reloaded = output<void>();

  private readonly api = inject(BondsApi);
  protected readonly dialog = viewChild.required(Dialog);
  private readonly formElement = viewChild<ElementRef<HTMLFormElement>>('formElement');
  private readonly focusInvalid = injectFocusFirstInvalid();
  private readonly requestId = new ClientRequestId();

  protected readonly directions = DIRECTION_CHOICES;
  protected readonly kinds = KIND_CHOICES;

  protected readonly form = new FormGroup({
    projectId: new FormControl('', { nonNullable: true }),
    direction: new FormControl<BondDirection>('Posted', { nonNullable: true }),
    kind: new FormControl<BondKind>('Performance', { nonNullable: true }),
    issuerName: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(120)],
    }),
    bondNumber: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(60)],
    }),
    amountMinor: new FormControl<number | null>(null, { validators: [Validators.required] }),
    issuedOn: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    validUntil: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    otherPartyName: new FormControl('', {
      nonNullable: true,
      validators: [Validators.maxLength(120)],
    }),
    note: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(500)] }),
  });

  private readonly direction = toSignal(this.form.controls.direction.valueChanges, {
    initialValue: 'Posted' as BondDirection,
  });
  /** Only a bond received from a firm names the firm. */
  protected readonly received = computed(
    () => (this.bond()?.direction ?? this.direction()) === 'Received',
  );

  private readonly projectList = rxResource({
    params: () => (this.bond() || this.projectId() ? undefined : true),
    stream: () => this.api.projects(false),
  });
  protected readonly projects = computed(() =>
    this.projectList.hasValue() ? this.projectList.value() : [],
  );

  protected readonly saving = signal(false);
  protected readonly error = signal<ApiError | null>(null);
  protected readonly extra = signal<readonly string[]>([]);

  ngOnInit(): void {
    const bond = this.bond();
    if (bond) {
      this.form.patchValue({
        kind: bond.kind,
        issuerName: bond.issuerName,
        bondNumber: bond.bondNumber,
        amountMinor: bond.amountMinor,
        issuedOn: bond.issuedOn,
        validUntil: bond.validUntil,
        otherPartyName: bond.otherPartyName ?? '',
        note: bond.note ?? '',
      });
    } else if (this.projectId()) {
      this.form.controls.projectId.setValue(this.projectId()!);
    } else {
      this.form.controls.projectId.addValidators(Validators.required);
      this.form.controls.projectId.updateValueAndValidity();
    }
  }

  protected async submit(): Promise<void> {
    if (this.saving()) return;
    this.form.markAllAsTouched();
    if (this.form.invalid) {
      this.focusInvalid(this.formElement()?.nativeElement);
      return;
    }
    const value = this.form.getRawValue();
    const details = {
      kind: value.kind,
      issuerName: value.issuerName.trim(),
      bondNumber: value.bondNumber.trim(),
      amountMinor: value.amountMinor,
      issuedOn: value.issuedOn,
      validUntil: value.validUntil,
      otherPartyName: this.received() ? value.otherPartyName.trim() || null : null,
      note: value.note.trim() || null,
    };
    const bond = this.bond();
    this.saving.set(true);
    this.error.set(null);
    this.extra.set([]);
    try {
      const saved = bond
        ? await firstValueFrom(this.api.update(bond.id, details, bond.version))
        : await firstValueFrom(
            this.api.create(
              this.projectId() ?? value.projectId,
              { ...details, direction: value.direction },
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
