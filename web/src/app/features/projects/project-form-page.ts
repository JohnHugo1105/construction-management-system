import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  computed,
  effect,
  inject,
  input,
  signal,
  untracked,
  viewChild,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import {
  AbstractControl,
  FormControl,
  FormGroup,
  ReactiveFormsModule,
  ValidationErrors,
  Validators,
} from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { ApiError, asApiError } from '../../core/api/api-error';
import { ToastService } from '../../core/notifications/toast-service';
import { SessionState } from '../../core/session/session-state';
import { applyServerErrors, injectFocusFirstInvalid } from '../../shared/forms/server-errors';
import { todayIn } from '../../shared/presentation/business-time';
import { Alert } from '../../shared/ui/alert/alert';
import { ApiErrorAlert } from '../../shared/ui/api-error-alert/api-error-alert';
import { Button } from '../../shared/ui/button/button';
import { Control } from '../../shared/ui/form-field/control';
import { FormField } from '../../shared/ui/form-field/form-field';
import { MoneyInput } from '../../shared/ui/inputs/money-input';
import { PageHeader } from '../../shared/ui/page-header/page-header';
import { Skeleton } from '../../shared/ui/skeleton/skeleton';
import { ProjectsApi } from './projects-api';
import { Project, ProjectDetails } from './projects.models';

/** The target end date cannot be before the start date. Checked as soon as either date changes. */
function targetNotBeforeStart(control: AbstractControl): ValidationErrors | null {
  const start = control.parent?.get('startDate')?.value;
  const end = control.value;
  if (
    typeof start === 'string' &&
    typeof end === 'string' &&
    start !== '' &&
    end !== '' &&
    end < start
  ) {
    return { message: "The target end date can't be before the start date." };
  }
  return null;
}

/** The form's field names differ from the API's for the price: the API calls it `contractPriceMinor`. */
const SERVER_FIELD_NAMES = { contractPriceMinor: 'contractPrice' } as const;

/**
 * Creates a project (`/projects/new`) or edits one (`/projects/:id/edit`). Every field is checked as the person leaves it;
 * what the server still refuses is shown under the right field. An edit sends the version it opened: if someone else changed
 * the project meanwhile, the person is told and can reload.
 */
@Component({
  selector: 'app-project-form-page',
  imports: [
    ReactiveFormsModule,
    RouterLink,
    PageHeader,
    FormField,
    Control,
    MoneyInput,
    Button,
    Alert,
    ApiErrorAlert,
    Skeleton,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './project-form-page.html',
})
export class ProjectFormPage {
  /** Bound from the route: set when editing, absent when creating. */
  readonly id = input<string | undefined>(undefined);

  private readonly api = inject(ProjectsApi);
  private readonly session = inject(SessionState);
  private readonly router = inject(Router);
  private readonly toast = inject(ToastService);

  protected readonly form = new FormGroup({
    name: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(120)],
    }),
    client: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(120)] }),
    location: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(200)] }),
    contractPrice: new FormControl<number | null>(null, { validators: [Validators.min(0)] }),
    startDate: new FormControl(todayIn(this.session.timeZoneId()), {
      nonNullable: true,
      validators: [Validators.required],
    }),
    targetEndDate: new FormControl('', { nonNullable: true, validators: [targetNotBeforeStart] }),
  });

  protected readonly isEdit = computed(() => this.id() !== undefined);
  protected readonly loadingProject = signal(false);
  protected readonly saving = signal(false);
  protected readonly error = signal<ApiError | null>(null);
  protected readonly extraMessages = signal<readonly string[]>([]);
  protected readonly showSummary = signal(false);

  /** The project being edited, as last loaded. Its version goes back with the save. */
  protected readonly loaded = signal<Project | null>(null);

  protected readonly backLink = computed(() =>
    this.id() ? ['/projects', this.id()] : ['/projects'],
  );
  protected readonly backLabel = computed(() => (this.id() ? 'Back to project' : 'All projects'));

  private readonly formElement = viewChild<ElementRef<HTMLFormElement>>('formElement');
  private readonly focusInvalid = injectFocusFirstInvalid();

  constructor() {
    // The end date is compared with the start date, so check it again when the start date changes.
    this.form.controls.startDate.valueChanges
      .pipe(takeUntilDestroyed(inject(DestroyRef)))
      .subscribe(() =>
        this.form.controls.targetEndDate.updateValueAndValidity({ emitEvent: false }),
      );

    effect(() => {
      const id = this.id();
      if (id) untracked(() => void this.load(id));
    });
  }

  /** Fetches the project again and replaces what is in the form (after a conflict, or on first load). */
  protected async load(id: string): Promise<void> {
    this.loadingProject.set(true);
    this.error.set(null);
    try {
      const project = await firstValueFrom(this.api.get(id));
      this.loaded.set(project);
      this.form.reset({
        name: project.name,
        client: project.client ?? '',
        location: project.location ?? '',
        contractPrice: project.contractPriceMinor,
        startDate: project.startDate,
        targetEndDate: project.targetEndDate ?? '',
      });
    } catch (failure) {
      this.error.set(asApiError(failure));
    } finally {
      this.loadingProject.set(false);
    }
  }

  protected reload(): void {
    const id = this.id();
    if (id) void this.load(id);
  }

  protected async submit(): Promise<void> {
    if (this.saving()) return;
    this.form.controls.targetEndDate.updateValueAndValidity({ emitEvent: false });
    this.form.markAllAsTouched();
    if (this.form.invalid) {
      this.showSummary.set(true);
      this.focusInvalid(this.formElement()?.nativeElement);
      return;
    }

    this.saving.set(true);
    this.showSummary.set(false);
    this.error.set(null);
    this.extraMessages.set([]);
    const details = this.details();
    const id = this.id();
    try {
      const saved = id
        ? await firstValueFrom(this.api.update(id, details, this.loaded()?.version ?? 0))
        : await firstValueFrom(this.api.create(details));
      this.toast.success(id ? 'Project saved.' : 'Project created.');
      await this.router.navigate(['/projects', saved.id]);
    } catch (failure) {
      const problem = asApiError(failure);
      this.error.set(problem);
      this.extraMessages.set(applyServerErrors(this.form, problem, SERVER_FIELD_NAMES));
      this.focusInvalid(this.formElement()?.nativeElement);
    } finally {
      this.saving.set(false);
    }
  }

  private details(): ProjectDetails {
    const value = this.form.getRawValue();
    return {
      name: value.name.trim(),
      client: value.client.trim() || null,
      location: value.location.trim() || null,
      contractPriceMinor: value.contractPrice,
      startDate: value.startDate,
      targetEndDate: value.targetEndDate || null,
    };
  }
}
