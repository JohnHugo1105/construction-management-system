import { Injectable, computed, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { firstValueFrom } from 'rxjs';
import { ApiError, asApiError } from '../../core/api/api-error';
import { ToastService } from '../../core/notifications/toast-service';
import { SessionState } from '../../core/session/session-state';
import { todayIn } from '../../shared/presentation/business-time';
import { ClientRequestId } from '../costing/client-request-id';
import { latestValue } from '../costing/resource-helpers';
import { ProjectContext } from '../projects/project-context';
import {
  LineDraft,
  LineProblems,
  buildRequest,
  draftBps,
  isChanged,
  percentProblem,
  placeServerErrors,
  steppedText,
} from './progress-draft';
import { ProgressApi } from './progress-api';
import { injectProgressView } from './progress-view';
import { Progress, ProgressLine } from './progress.models';

/**
 * The Progress tab's data and its unsaved edits. The report (percent by line, earned value) comes from the server and is shown
 * as it arrives. What a person types stays here as a draft until they press Save progress; one save records every changed line
 * together, all or nothing. Provide it on the tab.
 */
@Injectable()
export class ProgressState {
  private readonly api = inject(ProgressApi);
  private readonly context = inject(ProjectContext);
  private readonly session = inject(SessionState);
  private readonly toast = inject(ToastService);

  /** The report is read only while the progress view shows; coming back from the schedule reads it again (a plan change moves it). */
  private readonly view = injectProgressView();

  private readonly resource = rxResource({
    params: () => (this.view() === 'progress' ? (this.context.projectId() ?? undefined) : undefined),
    stream: ({ params }) => this.api.get(params),
  });

  readonly report = latestValue<Progress>(this.resource);
  readonly loadingFirst = computed(() => this.resource.isLoading() && this.report() === undefined);
  readonly error = computed(() => {
    const failure = this.resource.error();
    return failure ? asApiError(failure) : null;
  });

  /** Every line in the order shown, for building a request and counting changes. */
  readonly lines = computed<readonly ProgressLine[]>(() =>
    (this.report()?.divisions ?? []).flatMap((division) => division.lines),
  );

  // ---- Unsaved edits --------------------------------------------------------------------------------------------------------

  readonly drafts = signal<ReadonlyMap<string, LineDraft>>(new Map());

  readonly changedLines = computed(() => {
    const drafts = this.drafts();
    return this.lines().filter((line) => isChanged(line, drafts.get(line.budgetLineId)));
  });
  readonly changedCount = computed(() => this.changedLines().length);

  /** The date the progress is true on; today until the person picks another. */
  private readonly pickedDate = signal<string | null>(null);
  readonly today = computed(() => todayIn(this.session.timeZoneId()));
  readonly asOfDate = computed(() => this.pickedDate() ?? this.today());

  readonly saving = signal(false);
  /** A failure that is not about one field: a stale figure, an archived project, the server being down. */
  readonly saveError = signal<ApiError | null>(null);
  /** The server's messages for lines of the last refused save, and for the form as a whole (the date, the batch). */
  readonly lineProblems = signal<ReadonlyMap<string, LineProblems>>(new Map());
  readonly formProblems = signal<readonly string[]>([]);

  private readonly requestId = new ClientRequestId();

  draftOf(lineId: string): LineDraft | undefined {
    return this.drafts().get(lineId);
  }

  setPercentText(line: ProgressLine, text: string): void {
    this.update(line.budgetLineId, { text, note: this.draftOf(line.budgetLineId)?.note ?? '' });
  }

  step(line: ProgressLine, direction: 1 | -1): void {
    this.setPercentText(line, steppedText(line, this.draftOf(line.budgetLineId), direction));
  }

  setNote(line: ProgressLine, note: string): void {
    const draft = this.draftOf(line.budgetLineId);
    if (draft) this.update(line.budgetLineId, { text: draft.text, note });
  }

  setDate(date: string): void {
    this.pickedDate.set(date === '' ? null : date);
    this.formProblems.set([]);
  }

  /** The line's own message about its typed percent, while it is not a percent. */
  localProblem(lineId: string): string | null {
    return percentProblem(this.draftOf(lineId));
  }

  discardAll(): void {
    this.drafts.set(new Map());
    this.clearProblems();
  }

  reload(): void {
    this.resource.reload();
  }

  /** Records every changed line together. Resolves true when it went through. */
  async save(): Promise<boolean> {
    if (this.saving()) return false;
    const projectId = this.context.projectId();
    const drafts = this.drafts();
    // A typed percent that is not a percent is shown on its line and blocks the save.
    if (!projectId || this.changedLines().some((line) => percentProblem(drafts.get(line.budgetLineId)))) return false;
    const request = buildRequest(this.lines(), drafts, this.asOfDate(), this.requestId.current);
    if (!request) return false;

    this.saving.set(true);
    this.clearProblems();
    try {
      const result = await firstValueFrom(this.api.record(projectId, request));
      this.requestId.renew();
      this.drafts.set(new Map());
      this.pickedDate.set(null);
      this.toast.success(
        `Progress saved for ${result.linesUpdated} ${result.linesUpdated === 1 ? 'line' : 'lines'}.`,
      );
      this.resource.reload();
      return true;
    } catch (failure) {
      const problem = asApiError(failure);
      if (problem.isValidation && !problem.isConflict) {
        const placed = placeServerErrors(problem.errors, request.lines.map((line) => line.budgetLineId));
        this.lineProblems.set(placed.lines);
        this.formProblems.set(placed.form);
        if (placed.lines.size === 0 && placed.form.length === 0) this.saveError.set(problem);
      } else {
        this.saveError.set(problem);
      }
      return false;
    } finally {
      this.saving.set(false);
    }
  }

  private update(lineId: string, draft: LineDraft): void {
    const next = new Map(this.drafts());
    const line = this.lines().find((item) => item.budgetLineId === lineId);
    // Typing back to the saved percent (with nothing to explain) is no edit at all.
    if (line && draftBps(draft) === line.percentBps && draft.note.trim() === '') next.delete(lineId);
    else next.set(lineId, draft);
    this.drafts.set(next);
    if (this.lineProblems().has(lineId)) {
      const problems = new Map(this.lineProblems());
      problems.delete(lineId);
      this.lineProblems.set(problems);
    }
  }

  private clearProblems(): void {
    this.saveError.set(null);
    this.lineProblems.set(new Map());
    this.formProblems.set([]);
  }
}
