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
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ApiError, asApiError } from '../../core/api/api-error';
import { SessionState } from '../../core/session/session-state';
import { applyServerErrors, injectFocusFirstInvalid } from '../../shared/forms/server-errors';
import { todayIn } from '../../shared/presentation/business-time';
import { formatMoney } from '../../shared/presentation/money';
import { ApiErrorAlert } from '../../shared/ui/api-error-alert/api-error-alert';
import { Button } from '../../shared/ui/button/button';
import { Dialog } from '../../shared/ui/dialog/dialog';
import { Control } from '../../shared/ui/form-field/control';
import { FieldGroup } from '../../shared/ui/form-field/field-group';
import { FormField } from '../../shared/ui/form-field/form-field';
import { Icon } from '../../shared/ui/icon/icon';
import { ClientRequestId } from '../costing/client-request-id';
import { ScheduleState } from './schedule-state';
import { ScheduleActivity, ScheduleBudgetLine } from './schedule.models';

/** Up to this many lines to choose from, every division starts open; above it, only divisions with a chosen line do. */
const OPEN_ALL_UP_TO = 25;

interface DivisionChoices {
  readonly key: string;
  readonly name: string;
  readonly lines: readonly ScheduleBudgetLine[];
}

/**
 * Adds a schedule activity or changes one: its name, planned dates, and the budget lines whose work it covers, picked by
 * division. A line already in another activity is left out of the picker (a line belongs to one activity only), and the
 * dialog says how many. Lines are optional: an activity without them is kept but counts for nothing in the plan until they
 * are linked. The server checks everything and its messages land under the field they are about.
 */
@Component({
  selector: 'app-activity-dialog',
  imports: [
    ReactiveFormsModule,
    Dialog,
    Button,
    FormField,
    FieldGroup,
    Control,
    ApiErrorAlert,
    Icon,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './activity-dialog.html',
  styleUrl: './activity-dialog.scss',
})
export class ActivityDialog implements OnInit {
  /** The activity to change; leave it out to add one. */
  readonly activity = input<ScheduleActivity | null>(null);

  readonly dismissed = output<void>();
  readonly saved = output<void>();

  private readonly state = inject(ScheduleState);
  private readonly session = inject(SessionState);
  private readonly dialog = viewChild.required(Dialog);
  private readonly formElement = viewChild<ElementRef<HTMLFormElement>>('formElement');
  private readonly focusInvalid = injectFocusFirstInvalid();
  private readonly requestId = new ClientRequestId();

  protected readonly title = computed(() => (this.activity() ? 'Change activity' : 'Add activity'));
  protected readonly saveLabel = computed(() =>
    this.activity() ? 'Save activity' : 'Add activity',
  );

  protected readonly form = new FormGroup({
    name: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(120)],
    }),
    plannedStart: new FormControl(todayIn(this.session.timeZoneId()), {
      nonNullable: true,
      validators: [Validators.required],
    }),
    plannedFinish: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
  });

  protected readonly chosen = signal<ReadonlySet<string>>(new Set());
  protected readonly saving = signal(false);
  protected readonly error = signal<ApiError | null>(null);
  protected readonly linesProblem = signal<string | null>(null);
  protected readonly unmatched = signal<readonly string[]>([]);

  /** The lines this activity may link: free ones and its own. */
  private readonly available = computed(() => {
    const own = this.activity()?.id ?? null;
    return (this.state.schedule()?.budgetLines ?? []).filter(
      (line) => line.activityId === null || line.activityId === own,
    );
  });

  protected readonly takenCount = computed(
    () => (this.state.schedule()?.budgetLines.length ?? 0) - this.available().length,
  );

  protected readonly divisions = computed<readonly DivisionChoices[]>(() => {
    const groups = new Map<string, { name: string; lines: ScheduleBudgetLine[] }>();
    for (const line of this.available()) {
      const key = line.division === null ? 'unclassified' : `division-${line.division}`;
      const group = groups.get(key) ?? { name: line.divisionName, lines: [] };
      group.lines.push(line);
      groups.set(key, group);
    }
    return Array.from(groups, ([key, group]) => ({ key, name: group.name, lines: group.lines }));
  });

  private readonly toggled = signal<ReadonlyMap<string, boolean>>(new Map());

  protected isOpen(division: DivisionChoices): boolean {
    const toggled = this.toggled().get(division.key);
    if (toggled !== undefined) return toggled;
    return this.available().length <= OPEN_ALL_UP_TO || this.chosenIn(division) > 0;
  }

  protected toggle(division: DivisionChoices): void {
    this.toggled.set(new Map(this.toggled()).set(division.key, !this.isOpen(division)));
  }

  protected chosenIn(division: DivisionChoices): number {
    const chosen = this.chosen();
    return division.lines.filter((line) => chosen.has(line.id)).length;
  }

  protected lineHint(line: ScheduleBudgetLine): string {
    const budget = `${formatMoney(line.budgetMinor, this.session.currencyCode())} budget`;
    return line.wbsCode ? `${line.wbsCode} · ${budget}` : budget;
  }

  protected readonly chosenLabel = computed(() => {
    const count = this.chosen().size;
    return count === 0 ? 'No lines chosen' : `${count} ${count === 1 ? 'line' : 'lines'} chosen`;
  });

  ngOnInit(): void {
    const existing = this.activity();
    if (!existing) return;
    this.form.reset({
      name: existing.name,
      plannedStart: existing.plannedStart,
      plannedFinish: existing.plannedFinish,
    });
    this.chosen.set(new Set(existing.budgetLineIds));
  }

  protected choose(line: ScheduleBudgetLine, event: Event): void {
    const next = new Set(this.chosen());
    if ((event.target as HTMLInputElement).checked) next.add(line.id);
    else next.delete(line.id);
    this.chosen.set(next);
    this.linesProblem.set(null);
  }

  protected cancel(): void {
    this.dialog().dismiss();
  }

  protected reload(): void {
    this.error.set(null);
    this.state.reload();
  }

  protected async submit(): Promise<void> {
    if (this.saving()) return;
    this.form.markAllAsTouched();
    if (this.form.invalid) {
      this.focusInvalid(this.formElement()?.nativeElement);
      return;
    }

    const { name, plannedStart, plannedFinish } = this.form.getRawValue();
    // Keep the lines in the order they are listed, so a change that only re-ticks the same lines is no change.
    const chosen = this.chosen();
    const budgetLineIds = (this.state.schedule()?.budgetLines ?? [])
      .filter((line) => chosen.has(line.id))
      .map((line) => line.id);
    const details = { name: name.trim(), plannedStart, plannedFinish, budgetLineIds };
    const existing = this.activity();

    this.saving.set(true);
    this.error.set(null);
    this.linesProblem.set(null);
    this.unmatched.set([]);
    try {
      if (existing) await this.state.update(existing, details);
      else await this.state.create(details, this.requestId.current);
      this.requestId.renew();
      this.saved.emit();
    } catch (failure) {
      this.show(asApiError(failure));
    } finally {
      this.saving.set(false);
    }
  }

  /** Puts each server message under its field; one about the lines goes under the picker. Anything else shows at the top. */
  private show(problem: ApiError): void {
    const lineMessages: string[] = [];
    const others: Record<string, string[]> = {};
    for (const [field, messages] of Object.entries(problem.errors)) {
      if (field.startsWith('budgetLineIds')) lineMessages.push(...messages);
      else others[field] = messages;
    }
    if (lineMessages.length > 0) this.linesProblem.set(lineMessages.join(' '));
    const unmatched = applyServerErrors(this.form, { errors: others });
    this.unmatched.set(unmatched);
    if (
      problem.isConflict ||
      problem.isRuleViolation ||
      problem.isServerFailure ||
      unmatched.length > 0 ||
      !problem.isValidation
    )
      this.error.set(problem);
    this.focusInvalid(this.formElement()?.nativeElement);
  }
}
