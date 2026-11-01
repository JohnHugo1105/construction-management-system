import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  computed,
  effect,
  inject,
  input,
  signal,
  untracked,
  viewChild,
} from '@angular/core';
import { rxResource, toSignal } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { ApiError, asApiError } from '../../core/api/api-error';
import { ToastService } from '../../core/notifications/toast-service';
import { injectFocusFirstInvalid } from '../../shared/forms/server-errors';
import { ApiErrorAlert } from '../../shared/ui/api-error-alert/api-error-alert';
import { Button } from '../../shared/ui/button/button';
import { EmptyState } from '../../shared/ui/empty-state/empty-state';
import { Control } from '../../shared/ui/form-field/control';
import { FieldGroup } from '../../shared/ui/form-field/field-group';
import { FormField } from '../../shared/ui/form-field/form-field';
import { MoneyInput } from '../../shared/ui/inputs/money-input';
import { PercentInput } from '../../shared/ui/inputs/percent-input';
import { PageHeader } from '../../shared/ui/page-header/page-header';
import { Skeleton } from '../../shared/ui/skeleton/skeleton';
import { ClientRequestId } from '../costing/client-request-id';
import { VatTreatment } from '../costing/costing.models';
import { describeFormFailure } from '../workforce/form-failure';
import { VAT_CHOICES } from './subcontract-presentation';
import { SubcontractsApi } from './subcontracts-api';
import { Subcontract, SubcontractTerms } from './subcontracts.models';

/**
 * Drafts a subcontract, or edits a draft: the firm, the project, the scope, the contract sum and whether it includes VAT, and the
 * retention and withholding rates (10% and 0% unless changed). Nothing is committed until an approver awards it.
 */
@Component({
  selector: 'app-subcontract-form-page',
  imports: [
    ReactiveFormsModule,
    RouterLink,
    PageHeader,
    Button,
    FormField,
    FieldGroup,
    Control,
    MoneyInput,
    PercentInput,
    ApiErrorAlert,
    EmptyState,
    Skeleton,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-page-header
      [title]="id() ? 'Edit draft subcontract' : 'New subcontract'"
      [backLabel]="id() ? 'Back to the subcontract' : 'All subcontracts'"
      [backLink]="id() ? ['/subcontracts', id()] : ['/subcontracts']"
    />

    @if (loading()) {
      <app-skeleton [lines]="5" label="Loading the form" />
    } @else if (firms().length === 0) {
      <app-empty-state
        icon="contract"
        title="Add the subcontractor first"
        message="No firm is set up as a subcontractor yet. Add the firm under Suppliers and tick Subcontractor, then come back here."
      >
        <a appButton routerLink="/subcontracts">Back to subcontracts</a>
      </app-empty-state>
    } @else {
      <form class="form form-card" [formGroup]="form" (ngSubmit)="save()" #formElement>
        @if (error(); as problem) {
          <app-api-error-alert
            [error]="problem"
            [extra]="extra()"
            [allowRetry]="problem.status === 409"
            reloadLabel="Reload"
            (reload)="reload()"
          />
        }

        <app-form-field
          label="Subcontractor"
          [control]="form.controls.subcontractorId"
          requiredMessage="Choose the firm doing the work."
        >
          <select appControl formControlName="subcontractorId">
            <option value="" disabled>Choose a firm</option>
            @for (firm of firms(); track firm.id) {
              <option [value]="firm.id">{{ firm.name }}</option>
            }
          </select>
        </app-form-field>

        <div class="form__row">
          <app-form-field
            label="Project"
            [control]="form.controls.projectId"
            requiredMessage="Choose the project."
          >
            <select appControl formControlName="projectId">
              <option value="" disabled>Choose a project</option>
              @for (project of projects(); track project.id) {
                <option [value]="project.id">{{ project.name }}</option>
              }
            </select>
          </app-form-field>
          @if (subProjects().length > 0) {
            <app-form-field
              label="Sub-project"
              [optional]="true"
              [control]="form.controls.subProjectId"
            >
              <select appControl formControlName="subProjectId">
                <option value="">The whole project</option>
                @for (sub of subProjects(); track sub.id) {
                  <option [value]="sub.id">{{ sub.name }}</option>
                }
              </select>
            </app-form-field>
          }
        </div>

        <app-form-field
          label="Scope of work"
          [control]="form.controls.title"
          requiredMessage="Say what work is being given."
        >
          <input
            appControl
            formControlName="title"
            maxlength="200"
            autocomplete="off"
            placeholder="Electrical rough-in, Tower A"
          />
        </app-form-field>

        <app-form-field
          label="Contract sum"
          [control]="form.controls.contractSumMinor"
          requiredMessage="Enter the contract sum."
        >
          <app-money-input formControlName="contractSumMinor" />
        </app-form-field>

        <app-field-group label="The contract sum" [control]="form.controls.vatTreatment">
          <div class="choice-list">
            @for (choice of vatChoices; track choice.value) {
              <label class="choice">
                <input type="radio" formControlName="vatTreatment" [value]="choice.value" />
                <span class="choice__text"
                  ><span class="choice__label">{{ choice.label }}</span></span
                >
              </label>
            }
          </div>
        </app-field-group>

        <app-form-field
          label="Count the cost under"
          [optional]="true"
          hint="The part of the project budget this work is compared with. Leave it empty if it does not fit one."
          [control]="form.controls.wbsCode"
        >
          <select appControl formControlName="wbsCode">
            <option value="">Not placed in the work breakdown</option>
            @for (division of workItems(); track division.number) {
              <optgroup [label]="division.mainScope">
                @for (scope of division.scopes; track scope.code) {
                  <option [value]="scope.code">{{ scope.code }} {{ scope.name }}</option>
                }
              </optgroup>
            }
          </select>
        </app-form-field>

        <div class="form__row">
          <app-form-field
            label="Retention"
            hint="Held back from each claim until the work is complete."
            [control]="form.controls.retentionBps"
            requiredMessage="Enter the retention, 0% if none."
          >
            <app-percent-input formControlName="retentionBps" />
          </app-form-field>
          <app-form-field
            label="Withholding tax"
            hint="Withheld from each payment, on the amount before VAT."
            [control]="form.controls.withholdingBps"
            requiredMessage="Enter the withholding tax, 0% if none."
          >
            <app-percent-input formControlName="withholdingBps" />
          </app-form-field>
        </div>

        <app-form-field label="Notes" [optional]="true" [control]="form.controls.notes">
          <textarea appControl formControlName="notes" rows="3" maxlength="500"></textarea>
        </app-form-field>

        <div class="form__actions form__actions--sticky">
          <a appButton [routerLink]="id() ? ['/subcontracts', id()] : ['/subcontracts']">Cancel</a>
          <button appButton variant="primary" type="submit" [busy]="saving()">
            Save draft subcontract
          </button>
        </div>
      </form>
    }
  `,
})
export class SubcontractFormPage {
  /** Bound from the route when editing (`/subcontracts/:id/edit`). */
  readonly id = input<string>();
  /** Bound from the query string when started from a project (`?projectId=`). */
  readonly projectId = input<string>();

  private readonly api = inject(SubcontractsApi);
  private readonly router = inject(Router);
  private readonly toast = inject(ToastService);
  private readonly formElement = viewChild<ElementRef<HTMLFormElement>>('formElement');
  private readonly focusInvalid = injectFocusFirstInvalid();
  private readonly requestId = new ClientRequestId();

  protected readonly vatChoices = VAT_CHOICES;
  protected readonly form = new FormGroup({
    subcontractorId: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    projectId: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    subProjectId: new FormControl('', { nonNullable: true }),
    title: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(200)],
    }),
    contractSumMinor: new FormControl<number | null>(null, { validators: [Validators.required] }),
    vatTreatment: new FormControl<VatTreatment>('Exclusive', { nonNullable: true }),
    retentionBps: new FormControl<number | null>(1_000, { validators: [Validators.required] }),
    withholdingBps: new FormControl<number | null>(0, { validators: [Validators.required] }),
    wbsCode: new FormControl('', { nonNullable: true }),
    notes: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(500)] }),
  });

  private readonly workItemList = rxResource({ stream: () => this.api.workItems() });
  protected readonly workItems = computed(() =>
    this.workItemList.hasValue() ? this.workItemList.value() : [],
  );
  private readonly firmList = rxResource({ stream: () => this.api.firms() });
  private readonly projectList = rxResource({ stream: () => this.api.projects() });
  protected readonly firms = computed(() =>
    this.firmList.hasValue() ? this.firmList.value() : [],
  );
  protected readonly projects = computed(() =>
    (this.projectList.hasValue() ? this.projectList.value() : []).filter(
      (project) => project.status !== 'Archived',
    ),
  );
  private readonly chosenProject = toSignal(this.form.controls.projectId.valueChanges, {
    initialValue: '',
  });
  protected readonly subProjects = computed(
    () =>
      this.projects()
        .find((project) => project.id === this.chosenProject())
        ?.subProjects.filter((sub) => sub.status === 'Active') ?? [],
  );

  private readonly existing = signal<Subcontract | null>(null);
  private readonly existingLoaded = signal(false);
  protected readonly loading = computed(
    () =>
      this.firmList.isLoading() ||
      this.projectList.isLoading() ||
      (!!this.id() && !this.existingLoaded()),
  );
  protected readonly saving = signal(false);
  protected readonly error = signal<ApiError | null>(null);
  protected readonly extra = signal<readonly string[]>([]);

  constructor() {
    effect(() => {
      const id = this.id();
      const projectId = this.projectId();
      untracked(() => {
        if (id) void this.loadExisting(id);
        else if (projectId) this.form.controls.projectId.setValue(projectId);
      });
    });
  }

  protected reload(): void {
    const id = this.id();
    this.error.set(null);
    if (id) void this.loadExisting(id);
  }

  protected async save(): Promise<void> {
    if (this.saving()) return;
    this.form.markAllAsTouched();
    if (this.form.invalid) {
      this.focusInvalid(this.formElement()?.nativeElement);
      return;
    }
    const value = this.form.getRawValue();
    const terms: SubcontractTerms = {
      subcontractorId: value.subcontractorId,
      subProjectId: value.subProjectId || null,
      title: value.title.trim(),
      notes: value.notes.trim() || null,
      contractSumMinor: value.contractSumMinor,
      vatTreatment: value.vatTreatment,
      retentionBps: value.retentionBps,
      withholdingBps: value.withholdingBps,
      wbsCode: value.wbsCode || null,
    };

    this.saving.set(true);
    this.error.set(null);
    this.extra.set([]);
    try {
      const current = this.existing();
      const saved = current
        ? await firstValueFrom(this.api.update(current.id, terms, current.version))
        : await firstValueFrom(this.api.create(value.projectId, terms, this.requestId.current));
      this.requestId.renew();
      this.toast.success(
        current ? 'Draft subcontract saved.' : `Subcontract ${saved.number} drafted.`,
      );
      await this.router.navigate(['/subcontracts', saved.id]);
    } catch (failure) {
      const outcome = describeFormFailure(this.form, asApiError(failure));
      this.error.set(outcome.alert);
      this.extra.set(outcome.extra);
      this.focusInvalid(this.formElement()?.nativeElement);
    } finally {
      this.saving.set(false);
    }
  }

  private async loadExisting(id: string): Promise<void> {
    try {
      const subcontract = await firstValueFrom(this.api.get(id));
      this.existing.set(subcontract);
      this.form.setValue({
        subcontractorId: subcontract.subcontractorId,
        projectId: subcontract.projectId,
        subProjectId: subcontract.subProjectId ?? '',
        title: subcontract.title,
        contractSumMinor: subcontract.contractSumMinor,
        vatTreatment: subcontract.vatTreatment,
        retentionBps: subcontract.retentionBps,
        withholdingBps: subcontract.withholdingBps,
        wbsCode: subcontract.wbsCode ?? '',
        notes: subcontract.notes ?? '',
      });
      // The project of a subcontract doesn't change; a draft can still move to another of its sub-projects.
      this.form.controls.projectId.disable();
    } catch (failure) {
      this.error.set(asApiError(failure));
    } finally {
      this.existingLoaded.set(true);
    }
  }
}
