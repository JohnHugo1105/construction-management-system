import { Injectable, computed, inject, linkedSignal, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { firstValueFrom } from 'rxjs';
import { ApiError, asApiError } from '../../core/api/api-error';
import { SessionState } from '../../core/session/session-state';
import { injectListQuery, listField } from '../../shared/list-query/list-query';
import { todayIn } from '../../shared/presentation/business-time';
import { ProjectContext } from '../projects/project-context';
import {
  DraftRow,
  FALLBACK_HOURS_PER_DAY,
  RowProblems,
  checkRows,
  draftRowsFor,
  isFutureDate,
  mapBatchErrors,
  planBatch,
  rowsToSave,
} from './attendance-sheet';
import { newRequestId } from './request-id';
import { WorkforceApi } from './workforce-api';
import { AttendanceEntry, AttendanceSheet } from './workforce.models';

/** A saved entry that is being changed in place. */
export interface EntryEdit {
  readonly entry: AttendanceEntry;
  readonly regular: number | null;
  readonly overtime: number | null;
  readonly night: number | null;
  readonly remarks: string;
  readonly problems: RowProblems;
  readonly saving: boolean;
  /** A refusal that is not about one box (the entry was locked, or someone else changed it). */
  readonly error: ApiError | null;
}

/**
 * One project's daily attendance sheet. The day comes from the address (`?date=`) and defaults to today in the company time zone;
 * a future day is refused here and never asked of the server. The crew posted that day who have no entry yet become editable
 * lines that start as a full day, and "Save the day" sends them in one all-or-nothing request carrying one request id for the
 * whole sheet session: kept when the save fails (so a retry cannot record the day twice), replaced once it succeeds.
 *
 * Provide it on the Team tab: it reads the project from ProjectContext.
 */
@Injectable()
export class AttendanceSheetState {
  private readonly api = inject(WorkforceApi);
  private readonly session = inject(SessionState);
  private readonly context = inject(ProjectContext);

  readonly query = injectListQuery({ date: listField.date() });

  readonly today = computed(() => todayIn(this.session.timeZoneId()));
  readonly date = computed(() => this.query.values().date || this.today());
  readonly isToday = computed(() => this.date() === this.today());
  /** The chosen day is after today: nothing is loaded and nothing can be recorded. */
  readonly futureDate = computed(() => isFutureDate(this.date(), this.today()));

  private readonly hoursResource = rxResource({ stream: () => this.api.hoursPerDay() });
  /** The company's paid hours in a day: what a new line starts at, and the most regular hours a line may hold. */
  readonly hoursPerDay = computed(() =>
    this.hoursResource.hasValue() ? this.hoursResource.value() : FALLBACK_HOURS_PER_DAY,
  );

  private readonly sheetResource = rxResource({
    // The sheet waits for the company's day so every line starts at the right hours, and never asks for a future day.
    params: () => {
      const projectId = this.context.projectId();
      return projectId && !this.hoursResource.isLoading() && !this.futureDate()
        ? { projectId, date: this.date() }
        : undefined;
    },
    stream: ({ params }) => this.api.sheet(params.projectId, params.date),
  });

  readonly sheet = computed(() =>
    this.sheetResource.hasValue() ? this.sheetResource.value() : undefined,
  );
  readonly loading = computed(() => this.sheetResource.isLoading());
  readonly loadError = computed(() => {
    const failure = this.sheetResource.error();
    return failure ? asApiError(failure) : null;
  });

  /** Workers the person unticked, remembered so they stay unticked when the sheet is loaded again after a save. */
  private readonly away = new Set<string>();

  /** The lines still to be recorded, rebuilt whenever a sheet arrives. */
  readonly rows = linkedSignal<AttendanceSheet | undefined, readonly DraftRow[]>({
    source: () => this.sheet(),
    computation: (sheet) =>
      sheet
        ? draftRowsFor(sheet.crewWithoutEntry, this.hoursPerDay()).map((row) =>
            this.away.has(row.workerId) ? { ...row, worked: false } : row,
          )
        : [],
  });

  /** The sub-project the whole sheet is recorded against; empty means the whole project. */
  readonly subProjectId = signal('');

  /** Something has been changed on the lines that is not saved yet. */
  readonly edited = linkedSignal<AttendanceSheet | undefined, boolean>({
    source: () => this.sheet(),
    computation: () => false,
  });

  readonly toRecord = computed(() => rowsToSave(this.rows()));
  readonly notWorking = computed(() => this.rows().length - this.toRecord().length);

  readonly saving = signal(false);
  readonly problems = signal<ReadonlyMap<string, RowProblems>>(new Map());
  readonly general = signal<readonly string[]>([]);
  readonly error = signal<ApiError | null>(null);

  readonly editing = signal<EntryEdit | null>(null);

  /** The id that makes saving this sheet safe to repeat. A new one is made after a save and when a different day is opened. */
  readonly requestId = signal(newRequestId());

  setDate(date: string): void {
    if (date === '' || date === this.date()) return;
    this.away.clear();
    this.requestId.set(newRequestId());
    this.clearProblems();
    this.editing.set(null);
    this.query.set({ date: date === this.today() ? '' : date });
  }

  reload(): void {
    this.sheetResource.reload();
  }

  updateRow(workerId: string, patch: Partial<DraftRow>): void {
    this.rows.update((rows) =>
      rows.map((row) => (row.workerId === workerId ? { ...row, ...patch } : row)),
    );
    this.edited.set(true);
    if (patch.worked !== undefined) {
      if (patch.worked) this.away.delete(workerId);
      else this.away.add(workerId);
    }
    // A message about a line goes away as soon as the line is touched.
    if (this.problems().has(workerId)) {
      const next = new Map(this.problems());
      next.delete(workerId);
      this.problems.set(next);
    }
  }

  /**
   * Saves the ticked lines as one day. Returns true when the day was recorded. What is wrong is shown on the lines (or above the
   * sheet when it concerns the whole day); nothing is sent while a box is blank.
   */
  async saveDay(): Promise<boolean> {
    if (this.saving()) return false;
    this.clearProblems();

    const rows = this.rows();
    if (rowsToSave(rows).length === 0) {
      this.general.set(['Tick at least one worker to record, or leave this day as it is.']);
      return false;
    }
    const local = checkRows(rows, this.hoursPerDay());
    if (local.size > 0) {
      this.problems.set(local);
      return false;
    }

    const plan = planBatch(
      this.context.projectId() ?? '',
      this.subProjectId() || null,
      this.date(),
      rows,
      this.requestId(),
    );
    this.saving.set(true);
    try {
      await firstValueFrom(this.api.recordDay(plan.request));
      this.requestId.set(newRequestId());
      this.sheetResource.reload();
      return true;
    } catch (failure) {
      const problem = asApiError(failure);
      if (problem.isValidation && Object.keys(problem.errors).length > 0) {
        const mapped = mapBatchErrors(problem.errors, plan.order);
        this.problems.set(mapped.rows);
        this.general.set(mapped.general);
      } else {
        this.error.set(problem);
      }
      return false;
    } finally {
      this.saving.set(false);
    }
  }

  startEdit(entry: AttendanceEntry): void {
    this.editing.set({
      entry,
      regular: entry.regularHours,
      overtime: entry.overtimeHours,
      night: entry.nightHours ?? 0,
      remarks: entry.remarks ?? '',
      problems: {},
      saving: false,
      error: null,
    });
  }

  updateEdit(patch: Partial<Pick<EntryEdit, 'regular' | 'overtime' | 'night' | 'remarks'>>): void {
    this.editing.update((edit) => (edit ? { ...edit, ...patch, problems: {}, error: null } : edit));
  }

  cancelEdit(): void {
    this.editing.set(null);
  }

  /** Saves the hours of one saved entry. Returns true when it was changed. */
  async saveEdit(): Promise<boolean> {
    const edit = this.editing();
    if (!edit || edit.saving) return false;

    const found: { regular?: string; overtime?: string } = {};
    if (edit.regular === null) found.regular = 'Enter the regular hours, or 0 if there are none.';
    else if (edit.regular > this.hoursPerDay())
      found.regular = `Regular hours are at most ${this.hoursPerDay()} a day. Enter the rest as overtime.`;
    if (edit.overtime === null)
      found.overtime = 'Enter the overtime hours, or 0 if there are none.';
    if (found.regular || found.overtime) {
      this.editing.set({ ...edit, problems: found });
      return false;
    }

    this.editing.set({ ...edit, saving: true, problems: {}, error: null });
    try {
      await firstValueFrom(
        this.api.reviseAttendance(edit.entry.id, {
          subProjectId: edit.entry.subProjectId,
          regularHours: edit.regular ?? 0,
          overtimeHours: edit.overtime ?? 0,
          nightHours: edit.night ?? 0,
          remarks: edit.remarks.trim() || null,
          version: edit.entry.version,
        }),
      );
      this.editing.set(null);
      this.sheetResource.reload();
      return true;
    } catch (failure) {
      const problem = asApiError(failure);
      const fields = {
        regular: problem.firstError('regularHours') ?? undefined,
        overtime: problem.firstError('overtimeHours') ?? undefined,
        night: problem.firstError('nightHours') ?? undefined,
        remarks: problem.firstError('remarks') ?? undefined,
      };
      const aboutAField = Object.values(fields).some(Boolean);
      this.editing.set({
        ...edit,
        saving: false,
        problems: fields,
        error: aboutAField ? null : problem,
      });
      return false;
    }
  }

  private clearProblems(): void {
    this.problems.set(new Map());
    this.general.set([]);
    this.error.set(null);
  }
}
