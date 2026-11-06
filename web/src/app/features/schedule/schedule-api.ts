import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ActivityDetails, Schedule, ScheduleActivity, ScheduleCurve } from './schedule.models';

/** The schedule endpoints, one method each. Every change answers with the whole schedule as it now stands. */
@Injectable({ providedIn: 'root' })
export class ScheduleApi {
  private readonly http = inject(HttpClient);

  get(projectId: string): Observable<Schedule> {
    return this.http.get<Schedule>(`/api/projects/${projectId}/schedule`);
  }

  curve(projectId: string): Observable<ScheduleCurve> {
    return this.http.get<ScheduleCurve>(`/api/projects/${projectId}/schedule/s-curve`);
  }

  create(
    projectId: string,
    details: ActivityDetails,
    clientRequestId: string,
  ): Observable<Schedule> {
    return this.http.post<Schedule>(`/api/projects/${projectId}/schedule/activities`, {
      ...details,
      clientRequestId,
    });
  }

  update(
    projectId: string,
    activityId: string,
    details: ActivityDetails,
    version: number,
  ): Observable<Schedule> {
    return this.http.put<Schedule>(`/api/projects/${projectId}/schedule/activities/${activityId}`, {
      ...details,
      sortOrder: null,
      version,
    });
  }

  remove(
    projectId: string,
    activityId: string,
    reason: string,
    version: number,
  ): Observable<Schedule> {
    return this.http.post<Schedule>(
      `/api/projects/${projectId}/schedule/activities/${activityId}/remove`,
      { reason, version },
    );
  }

  /** Copies the planned dates to the baseline; the activities and versions on screen guard against a schedule that changed meanwhile. */
  setBaseline(projectId: string, activities: readonly ScheduleActivity[]): Observable<Schedule> {
    return this.http.post<Schedule>(`/api/projects/${projectId}/schedule/baseline`, {
      activities: activities.map((activity) => ({ id: activity.id, version: activity.version })),
    });
  }
}
