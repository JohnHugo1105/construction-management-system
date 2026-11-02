import { Injectable, computed, inject } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { Observable, firstValueFrom, tap } from 'rxjs';
import { asApiError } from '../../core/api/api-error';
import { latestValue } from '../costing/resource-helpers';
import { ProjectContext } from '../projects/project-context';
import { ScheduleApi } from './schedule-api';
import {
  ActivityDetails,
  Schedule,
  ScheduleActivity,
  ScheduleBudgetLine,
  ScheduleCurve,
} from './schedule.models';

/**
 * The Schedule view's data: the schedule and its S-curve, read side by side (two requests at once, never one after the other).
 * Every change answers with the whole schedule as it now stands, which replaces the one on screen; the S-curve is then read
 * again because a change moves it. Provide it on the view.
 */
@Injectable()
export class ScheduleState {
  private readonly api = inject(ScheduleApi);
  private readonly context = inject(ProjectContext);

  private readonly scheduleResource = rxResource({
    params: () => this.context.projectId() ?? undefined,
    stream: ({ params }) => this.api.get(params),
  });

  private readonly curveResource = rxResource({
    params: () => this.context.projectId() ?? undefined,
    stream: ({ params }) => this.api.curve(params),
  });

  readonly schedule = latestValue<Schedule>(this.scheduleResource);
  readonly curve = latestValue<ScheduleCurve>(this.curveResource);

  readonly loadingFirst = computed(
    () => this.scheduleResource.isLoading() && this.schedule() === undefined,
  );
  readonly error = computed(() => {
    const failure = this.scheduleResource.error();
    return failure ? asApiError(failure) : null;
  });
  readonly curveError = computed(() => {
    const failure = this.curveResource.error();
    return failure ? asApiError(failure) : null;
  });

  readonly activities = computed<readonly ScheduleActivity[]>(
    () => this.schedule()?.activities ?? [],
  );

  /** The budget lines by id, to name an activity's lines. */
  readonly lineById = computed<ReadonlyMap<string, ScheduleBudgetLine>>(
    () => new Map((this.schedule()?.budgetLines ?? []).map((line) => [line.id, line])),
  );

  reload(): void {
    this.scheduleResource.reload();
    this.curveResource.reload();
  }

  create(details: ActivityDetails, clientRequestId: string): Promise<Schedule> {
    return this.run((projectId) => this.api.create(projectId, details, clientRequestId));
  }

  update(activity: ScheduleActivity, details: ActivityDetails): Promise<Schedule> {
    return this.run((projectId) =>
      this.api.update(projectId, activity.id, details, activity.version),
    );
  }

  /** For the reason dialog, which runs it and keeps itself open on a refusal. */
  remove(activity: ScheduleActivity): (reason: string) => Observable<Schedule> {
    return (reason) =>
      this.api
        .remove(this.projectId(), activity.id, reason, activity.version)
        .pipe(tap((schedule) => this.replace(schedule)));
  }

  /** Copies the planned dates to the baseline, for exactly the activities and versions on screen. */
  setBaseline(): Promise<Schedule> {
    const activities = this.activities();
    return this.run((projectId) => this.api.setBaseline(projectId, activities));
  }

  private async run(call: (projectId: string) => Observable<Schedule>): Promise<Schedule> {
    const schedule = await firstValueFrom(call(this.projectId()));
    this.replace(schedule);
    return schedule;
  }

  private replace(schedule: Schedule): void {
    this.scheduleResource.set(schedule);
    this.curveResource.reload();
  }

  private projectId(): string {
    const id = this.context.projectId();
    if (!id) throw new Error('The schedule needs an open project.');
    return id;
  }
}
