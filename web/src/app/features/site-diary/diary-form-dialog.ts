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
import {
  AbstractControl,
  FormControl,
  FormGroup,
  ReactiveFormsModule,
  ValidationErrors,
  Validators,
} from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { ApiError, asApiError } from '../../core/api/api-error';
import { SessionState } from '../../core/session/session-state';
import { injectFocusFirstInvalid } from '../../shared/forms/server-errors';
import { todayIn } from '../../shared/presentation/business-time';
import { ApiErrorAlert } from '../../shared/ui/api-error-alert/api-error-alert';
import { Button } from '../../shared/ui/button/button';
import { Dialog } from '../../shared/ui/dialog/dialog';
import { Control } from '../../shared/ui/form-field/control';
import { FieldGroup } from '../../shared/ui/form-field/field-group';
import { FormField } from '../../shared/ui/form-field/form-field';
import { ClientRequestId } from '../costing/client-request-id';
import { describeFormFailure } from '../workforce/form-failure';
import { DiaryFilledInPanels } from './diary-filled-in';
import { SiteDiaryApi } from './site-diary-api';
import { WEATHER_CHOICES, reportDayLabel } from './site-diary-presentation';
import { DiaryDetailsInput, DiaryEntry, DiaryWeather } from './site-diary.models';

const DATE_FORMAT = /^\d{4}-\d{2}-\d{2}$/;

/** A whole number of workers from 0 to 2,000, in the words the person needs. An empty field is the required rule's to report. */
function workerCount(control: AbstractControl): ValidationErrors | null {
  const value: unknown = control.value;
  if (value === null || value === '') return null;
  return typeof value === 'number' && Number.isInteger(value) && value >= 0 && value <= 2000
    ? null
    : { message: 'Enter how many subcontractor workers were on site, from 0 to 2,000.' };
}

/**
 * Writes a day's report, or corrects one. A new report asks for the day (today unless chosen otherwise), the weather as chips, what
 * work was done and how many subcontractor workers were on site; issues, visitors and safety notes are one step away. Beside the form
 * it shows, read-only, who was on site and what was delivered that day. A refusal shows in the server's words above the form and the
 * person's text stays where it is.
 */
@Component({
  selector: 'app-diary-form-dialog',
  imports: [
    ReactiveFormsModule,
    Dialog,
    Button,
    FormField,
    FieldGroup,
    Control,
    ApiErrorAlert,
    DiaryFilledInPanels,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-dialog
      [title]="entry() ? 'Correct the report' : 'Write a site report'"
      variant="sheet"
      [locked]="saving()"
      (closed)="dismissed.emit()"
    >
      <div class="stack">
        @if (error(); as problem) {
          <app-api-error-alert
            [error]="problem"
            [extra]="extra()"
            [allowRetry]="problem.status === 409"
            reloadLabel="Reload reports"
            (reload)="reloaded.emit()"
          />
        }
        <form id="diary-form" class="form" [formGroup]="form" (ngSubmit)="submit()" #formElement>
          @if (entry(); as current) {
            <p class="day-line">Report for {{ dayLabel(current.reportDate) }}</p>
          } @else {
            <app-form-field
              label="Day of the report"
              [control]="form.controls.reportDate"
              requiredMessage="Choose the day this report is about."
              [hint]="dateHint()"
            >
              <input appControl type="date" formControlName="reportDate" [max]="today()" />
            </app-form-field>
          }

          <app-field-group label="Weather" [control]="form.controls.weather">
            <div class="weather-chips">
              @for (choice of weatherChoices; track choice.value) {
                <button
                  type="button"
                  class="filter-chip"
                  [attr.aria-pressed]="weather() === choice.value"
                  (click)="chooseWeather(choice.value)"
                >
                  {{ choice.label }}
                </button>
              }
            </div>
          </app-field-group>

          <app-form-field
            label="Work done"
            hint="On a day with no work, say why."
            [control]="form.controls.workDone"
            requiredMessage="Say what work was done. On a day with no work, say why."
          >
            <textarea
              appControl
              formControlName="workDone"
              rows="4"
              maxlength="2000"
              data-autofocus
            ></textarea>
          </app-form-field>

          @if (panelDate(); as day) {
            <app-diary-filled-in [filledIn]="filledIn()" [date]="day" [today]="today()" />
          }

          <app-form-field
            label="Subcontractor workers on site"
            hint="Their crews are not on your payroll, so type how many."
            [control]="form.controls.subcontractorWorkers"
            requiredMessage="Enter how many subcontractor workers were on site, or 0."
          >
            <input
              appControl
              type="number"
              inputmode="numeric"
              min="0"
              max="2000"
              formControlName="subcontractorWorkers"
            />
          </app-form-field>

          <details class="more" #more [open]="moreOpen()" (toggle)="moreOpen.set(more.open)">
            <summary>Issues, visitors and safety</summary>
            <div class="stack">
              <app-form-field
                label="Issues and delays"
                [optional]="true"
                [control]="form.controls.issuesAndDelays"
              >
                <textarea
                  appControl
                  formControlName="issuesAndDelays"
                  rows="2"
                  maxlength="1000"
                ></textarea>
              </app-form-field>
              <app-form-field
                label="Visitors and inspections"
                [optional]="true"
                [control]="form.controls.visitorsAndInspections"
              >
                <textarea
                  appControl
                  formControlName="visitorsAndInspections"
                  rows="2"
                  maxlength="500"
                ></textarea>
              </app-form-field>
              <app-form-field
                label="Safety notes"
                [optional]="true"
                [control]="form.controls.safetyNotes"
              >
                <textarea
                  appControl
                  formControlName="safetyNotes"
                  rows="2"
                  maxlength="1000"
                ></textarea>
              </app-form-field>
              <app-form-field
                label="Weather note"
                [optional]="true"
                [control]="form.controls.weatherNote"
              >
                <input appControl formControlName="weatherNote" maxlength="200" autocomplete="off" />
              </app-form-field>
            </div>
          </details>
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
        <button appButton variant="primary" type="submit" form="diary-form" [busy]="saving()">
          {{ entry() ? 'Save changes' : 'Save report' }}
        </button>
      </div>
    </app-dialog>
  `,
  styles: `
    .day-line {
      font-weight: 600;
    }

    .weather-chips {
      display: flex;
      flex-wrap: wrap;
      gap: var(--space-2);
    }

    .more > summary {
      min-height: var(--touch-target);
      display: flex;
      align-items: center;
      font-weight: 600;
      cursor: pointer;
    }

    .more > .stack {
      margin-top: var(--space-3);
    }
  `,
})
export class DiaryFormDialog implements OnInit {
  /** The project the report is for. */
  readonly projectId = input.required<string>();
  /** The report being corrected; null writes a new one. */
  readonly entry = input<DiaryEntry | null>(null);
  /** The day a new report starts on, "2026-11-02" (today when empty). */
  readonly initialDate = input<string | null>(null);

  readonly saved = output<DiaryEntry>();
  readonly dismissed = output<void>();
  /** The person chose to reload after a refusal (someone else changed the report, or the day already has one). */
  readonly reloaded = output<void>();

  private readonly api = inject(SiteDiaryApi);
  private readonly session = inject(SessionState);
  protected readonly dialog = viewChild.required(Dialog);
  private readonly formElement = viewChild<ElementRef<HTMLFormElement>>('formElement');
  private readonly focusInvalid = injectFocusFirstInvalid();
  private readonly requestId = new ClientRequestId();

  protected readonly weatherChoices = WEATHER_CHOICES;
  protected readonly dayLabel = reportDayLabel;
  protected readonly today = computed(() => todayIn(this.session.timeZoneId()));

  protected readonly form = new FormGroup({
    reportDate: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    weather: new FormControl<DiaryWeather>('Fair', { nonNullable: true }),
    weatherNote: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(200)] }),
    workDone: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(2000)],
    }),
    issuesAndDelays: new FormControl('', {
      nonNullable: true,
      validators: [Validators.maxLength(1000)],
    }),
    visitorsAndInspections: new FormControl('', {
      nonNullable: true,
      validators: [Validators.maxLength(500)],
    }),
    safetyNotes: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(1000)] }),
    subcontractorWorkers: new FormControl<number | null>(0, {
      validators: [Validators.required, workerCount],
    }),
  });

  protected readonly weather = toSignal(this.form.controls.weather.valueChanges, {
    initialValue: 'Fair' as DiaryWeather,
  });
  private readonly typedDate = toSignal(this.form.controls.reportDate.valueChanges, {
    initialValue: '',
  });

  /** The day whose attendance and deliveries are shown: the report's own day, or the day typed once it is a real day not in the future. */
  protected readonly panelDate = computed(() => {
    const current = this.entry();
    if (current) return current.reportDate;
    const typed = this.typedDate();
    return DATE_FORMAT.test(typed) && typed <= this.today() ? typed : null;
  });

  private readonly filledInResource = rxResource({
    // A correction already carries the panels; a new report asks for the chosen day's.
    params: () => (this.entry() ? undefined : this.panelDate() ?? undefined),
    stream: ({ params }) => this.api.filledIn(this.projectId(), params),
  });
  protected readonly filledIn = computed(() =>
    this.entry() ?? (this.filledInResource.hasValue() ? this.filledInResource.value() : undefined),
  );

  protected readonly dateHint = computed(() => {
    const typed = this.typedDate();
    return DATE_FORMAT.test(typed) && typed > this.today()
      ? 'A report can only be for today or an earlier day.'
      : '';
  });

  protected readonly moreOpen = signal(false);
  protected readonly saving = signal(false);
  protected readonly error = signal<ApiError | null>(null);
  protected readonly extra = signal<readonly string[]>([]);

  ngOnInit(): void {
    const current = this.entry();
    if (current) {
      this.form.patchValue({
        reportDate: current.reportDate,
        weather: current.weather,
        weatherNote: current.weatherNote ?? '',
        workDone: current.workDone,
        issuesAndDelays: current.issuesAndDelays ?? '',
        visitorsAndInspections: current.visitorsAndInspections ?? '',
        safetyNotes: current.safetyNotes ?? '',
        subcontractorWorkers: current.subcontractorWorkers,
      });
      this.moreOpen.set(
        !!(
          current.issuesAndDelays ||
          current.visitorsAndInspections ||
          current.safetyNotes ||
          current.weatherNote
        ),
      );
    } else {
      this.form.controls.reportDate.setValue(this.initialDate() ?? this.today());
    }
  }

  protected chooseWeather(value: DiaryWeather): void {
    this.form.controls.weather.setValue(value);
    this.form.controls.weather.markAsDirty();
  }

  protected async submit(): Promise<void> {
    if (this.saving()) return;
    this.form.markAllAsTouched();
    if (this.form.invalid) {
      this.moreOpen.set(this.moreOpen() || this.hiddenFieldInvalid());
      this.focusInvalid(this.formElement()?.nativeElement);
      return;
    }
    const value = this.form.getRawValue();
    const details: DiaryDetailsInput = {
      weather: value.weather,
      weatherNote: value.weatherNote.trim() || null,
      workDone: value.workDone.trim(),
      issuesAndDelays: value.issuesAndDelays.trim() || null,
      visitorsAndInspections: value.visitorsAndInspections.trim() || null,
      safetyNotes: value.safetyNotes.trim() || null,
      subcontractorWorkers: value.subcontractorWorkers ?? 0,
    };
    const current = this.entry();
    this.saving.set(true);
    this.error.set(null);
    this.extra.set([]);
    try {
      const saved = current
        ? await firstValueFrom(
            this.api.correct(this.projectId(), current.reportDate, details, current.version),
          )
        : await firstValueFrom(
            this.api.create(this.projectId(), value.reportDate, details, this.requestId.current),
          );
      this.requestId.renew();
      this.saved.emit(saved);
    } catch (failure) {
      const outcome = describeFormFailure(this.form, asApiError(failure));
      this.error.set(outcome.alert);
      this.extra.set(outcome.extra);
      this.moreOpen.set(this.moreOpen() || this.hiddenFieldInvalid());
      this.focusInvalid(this.formElement()?.nativeElement);
    } finally {
      this.saving.set(false);
    }
  }

  /** A problem in a field that sits behind "Issues, visitors and safety" opens that part so the message can be seen. */
  private hiddenFieldInvalid(): boolean {
    const { issuesAndDelays, visitorsAndInspections, safetyNotes, weatherNote } = this.form.controls;
    return [issuesAndDelays, visitorsAndInspections, safetyNotes, weatherNote].some(
      (control) => control.invalid,
    );
  }
}
