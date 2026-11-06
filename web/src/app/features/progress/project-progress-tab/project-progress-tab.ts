import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { Permission } from '../../../core/session/permissions';
import { SessionState } from '../../../core/session/session-state';
import { formatDate } from '../../../shared/presentation/business-time';
import { formatMoney } from '../../../shared/presentation/money';
import { formatBps } from '../../../shared/presentation/percent';
import { ApiErrorAlert } from '../../../shared/ui/api-error-alert/api-error-alert';
import { Button } from '../../../shared/ui/button/button';
import { EmptyState } from '../../../shared/ui/empty-state/empty-state';
import { Control } from '../../../shared/ui/form-field/control';
import { FormField } from '../../../shared/ui/form-field/form-field';
import { Icon, IconName } from '../../../shared/ui/icon/icon';
import { Skeleton } from '../../../shared/ui/skeleton/skeleton';
import { describeBasis } from '../../costing/figures';
import { SegmentOption, SegmentedControl } from '../../costing/ui/segmented-control';
import { ProjectContext } from '../../projects/project-context';
import { ScheduleView } from '../../schedule/schedule-view/schedule-view';
import { fieldText, isLowering } from '../progress-draft';
import {
  Sentence,
  Tone,
  completeHeadline,
  costIndexSentence,
  forecastMargin,
  forecastSentence,
  scheduleSentence,
  spentForWorth,
  wholePercent,
} from '../progress-presentation';
import { ProgressState } from '../progress-state';
import { ProgressView, injectProgressView } from '../progress-view';
import { ProgressDivision, ProgressLine } from '../progress.models';
import { ProgressHistoryDialog } from '../ui/progress-history-dialog';

/** Lines shown at first in an open division; "Show more lines" adds this many again. Keeps a very large project light. */
const LINES_PER_STEP = 40;
/** Up to this many lines in all, every division starts open; above it, they start folded. */
const OPEN_ALL_UP_TO = 25;

/**
 * A project's Progress tab: how far along the work is and what that says about cost. The summary leads (percent complete, spent
 * for work worth, forecast, margin), then the lines grouped by division (each folds). People with `progress.write` set a percent
 * per line (typed or with the 5% stepper) and save everything changed together with one Save progress; everyone else sees the
 * figures. An archived project is read-only. The server works out every figure; this tab only shows them.
 *
 * People who may read the schedule switch between "Update progress" and "Schedule" (the router keeps the choice in `?view=`);
 * the Schedule view lists the planned activities and draws the S-curve.
 */
@Component({
  selector: 'app-project-progress-tab',
  imports: [
    ApiErrorAlert,
    Button,
    Control,
    EmptyState,
    FormField,
    Icon,
    Skeleton,
    ProgressHistoryDialog,
    ScheduleView,
    SegmentedControl,
  ],
  providers: [ProgressState],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './project-progress-tab.html',
  styleUrl: './project-progress-tab.scss',
})
export class ProjectProgressTab {
  protected readonly state = inject(ProgressState);
  private readonly context = inject(ProjectContext);
  private readonly session = inject(SessionState);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  protected readonly view = injectProgressView();
  protected readonly canSchedule = this.session.permit(Permission.scheduleRead);

  private readonly mayWrite = this.session.permit(Permission.progressWrite);
  protected readonly canWrite = computed(() => this.mayWrite() && !this.context.isArchived());
  /** Says why the editing is missing, to the people who would otherwise have it. */
  protected readonly archivedNote = computed(() => this.mayWrite() && this.context.isArchived());
  /** "Update progress" for the people who record it, plain "Progress" for those who only read it. */
  protected readonly viewOptions = computed<readonly SegmentOption<ProgressView>[]>(() => [
    { value: 'progress', label: this.canWrite() ? 'Update progress' : 'Progress' },
    { value: 'schedule', label: 'Schedule' },
  ]);

  protected readonly noLines = computed(() => {
    const report = this.state.report();
    return report !== undefined && report.divisions.length === 0;
  });

  protected readonly basisLine = computed(() => {
    const report = this.state.report();
    return report ? describeBasis(report.basis) : '';
  });

  protected readonly updatedLine = computed(() => {
    const date = this.state.report()?.lastUpdatedOn;
    return date ? `Last updated ${formatDate(date)}` : '';
  });

  /** The summary in words, from the figures the server sent. */
  protected readonly summary = computed(() => {
    const report = this.state.report();
    if (!report) return null;
    const value = report.earnedValue;
    const currency = this.session.currencyCode();
    const extras = [costIndexSentence(value.cpiBps, value.spentMinor), scheduleSentence(value.spiBps, report.plannedFrom ?? null)].filter(
      (sentence): sentence is Sentence => sentence !== null,
    );
    return {
      headline: completeHeadline(value),
      barPercent: Math.min(100, (value.percentCompleteBps ?? 0) / 100),
      spent: spentForWorth(value, currency),
      forecast: forecastSentence(value, currency),
      margin: forecastMargin(value, currency),
      extras,
    };
  });

  // ---- Divisions: folded or open, and how many lines of each are drawn ------------------------------------------------------

  private readonly toggled = signal<ReadonlyMap<string, boolean>>(new Map());
  private readonly limits = signal<ReadonlyMap<string, number>>(new Map());

  protected readonly divisions = computed(() => {
    const report = this.state.report();
    if (!report) return [];
    const total = report.divisions.reduce((sum, division) => sum + division.lines.length, 0);
    const openByDefault = total <= OPEN_ALL_UP_TO;
    const toggled = this.toggled();
    const limits = this.limits();
    return report.divisions.map((division) => {
      const key = this.keyOf(division);
      const limit = limits.get(key) ?? LINES_PER_STEP;
      return {
        key,
        division,
        open: toggled.get(key) ?? openByDefault,
        shown: division.lines.slice(0, limit),
        hidden: Math.max(0, division.lines.length - limit),
      };
    });
  });

  protected readonly historyOf = signal<{ readonly id: string; readonly description: string } | null>(null);

  protected toggle(key: string, open: boolean): void {
    this.toggled.set(new Map(this.toggled()).set(key, !open));
  }

  protected showMore(key: string, current: number): void {
    this.limits.set(new Map(this.limits()).set(key, current + LINES_PER_STEP));
  }

  // ---- What the template shows per line ------------------------------------------------------------------------------------

  protected money(minor: number): string {
    return formatMoney(minor, this.session.currencyCode());
  }

  protected toneIcon(tone: Tone): IconName {
    return tone === 'good' ? 'check-circle' : tone === 'warning' ? 'warning' : 'info';
  }

  protected whole(bps: number | null): string {
    return wholePercent(bps);
  }

  protected percent(bps: number | null): string {
    return bps === null ? '' : formatBps(bps);
  }

  protected date(value: string | null): string {
    return value ? formatDate(value) : '';
  }

  protected text(line: ProgressLine): string {
    return fieldText(line, this.state.draftOf(line.budgetLineId));
  }

  protected lowering(line: ProgressLine): boolean {
    return isLowering(line, this.state.draftOf(line.budgetLineId));
  }

  protected noteOf(line: ProgressLine): string {
    return this.state.draftOf(line.budgetLineId)?.note ?? '';
  }

  protected percentProblem(line: ProgressLine): string | null {
    return (
      this.state.lineProblems().get(line.budgetLineId)?.percent ??
      this.state.lineProblems().get(line.budgetLineId)?.line ??
      this.state.localProblem(line.budgetLineId)
    );
  }

  protected noteProblem(line: ProgressLine): string | null {
    return this.state.lineProblems().get(line.budgetLineId)?.note ?? null;
  }

  protected edited(line: ProgressLine): boolean {
    return this.state.draftOf(line.budgetLineId) !== undefined;
  }

  protected countLabel(): string {
    const count = this.state.changedCount();
    return `${count} ${count === 1 ? 'line' : 'lines'} changed`;
  }

  // ---- Events ---------------------------------------------------------------------------------------------------------------

  protected typed(line: ProgressLine, event: Event): void {
    this.state.setPercentText(line, (event.target as HTMLInputElement).value);
  }

  protected noted(line: ProgressLine, event: Event): void {
    this.state.setNote(line, (event.target as HTMLTextAreaElement).value);
  }

  protected dated(event: Event): void {
    this.state.setDate((event.target as HTMLInputElement).value);
  }

  protected show(view: ProgressView): void {
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { view: view === 'schedule' ? 'schedule' : null },
      queryParamsHandling: 'merge',
    });
  }

  protected save(): void {
    void this.state.save();
  }

  private keyOf(division: ProgressDivision): string {
    return division.division === null ? 'unclassified' : `division-${division.division}`;
  }
}
