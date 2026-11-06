import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  BadgeSheet,
  ConfirmRequest,
  ConfirmResult,
  DeviceKind,
  DeviceWithKey,
  ImportResult,
  IssueMissingResult,
  ManualPunchRequest,
  ProjectOption,
  PunchCodeIssued,
  TimeClockDayList,
  TimeClockDevice,
  TimeClockDeviceList,
  TimeClockPunch,
  TimeClockSummary,
  WorkerOption,
} from './timeclock.models';

/**
 * The time clock endpoints for signed-in people, one method each: the review (timeclock.review), badges and imports (timeclock.manage) and the
 * registered machines (settings.manage). The kiosk's machine route is in `kiosk-api.ts`.
 */
@Injectable({ providedIn: 'root' })
export class TimeClockApi {
  private readonly http = inject(HttpClient);

  /** One project's day of scans; without a date, the earliest day still waiting (or today). */
  days(projectId: string, date: string | null, page = 1): Observable<TimeClockDayList> {
    let params = new HttpParams().set('projectId', projectId).set('page', page);
    if (date) params = params.set('date', date);
    return this.http.get<TimeClockDayList>('/api/timeclock/days', { params });
  }

  summary(projectId: string): Observable<TimeClockSummary> {
    return this.http.get<TimeClockSummary>('/api/timeclock/summary', {
      params: new HttpParams().set('projectId', projectId),
    });
  }

  confirm(request: ConfirmRequest): Observable<ConfirmResult> {
    return this.http.post<ConfirmResult>('/api/timeclock/days/confirm', request);
  }

  addManual(request: ManualPunchRequest): Observable<TimeClockPunch> {
    return this.http.post<TimeClockPunch>('/api/timeclock/punches/manual', request);
  }

  voidPunch(id: string, reason: string, version: number): Observable<TimeClockPunch> {
    return this.http.post<TimeClockPunch>(`/api/timeclock/punches/${id}/void`, { reason, version });
  }

  /** A machine's exported file, sent as its text. The server reads it and keeps only the scans. */
  importFile(projectId: string, csv: string): Observable<ImportResult> {
    return this.http.post<ImportResult>(
      '/api/timeclock/import',
      { csv },
      {
        params: new HttpParams().set('projectId', projectId),
      },
    );
  }

  badges(workerIds: readonly string[]): Observable<BadgeSheet> {
    let params = new HttpParams();
    for (const id of workerIds) params = params.append('workerIds', id);
    return this.http.get<BadgeSheet>('/api/timeclock/badges', { params });
  }

  issueMissing(): Observable<IssueMissingResult> {
    return this.http.post<IssueMissingResult>('/api/timeclock/badges/issue-missing', {});
  }

  /** Issues the worker a badge code, or replaces the one they have (the old badge stops working at once). */
  issueCode(workerId: string, version: number): Observable<PunchCodeIssued> {
    return this.http.post<PunchCodeIssued>(`/api/workers/${workerId}/punch-code`, { version });
  }

  devices(): Observable<TimeClockDeviceList> {
    return this.http.get<TimeClockDeviceList>('/api/timeclock/devices');
  }

  registerDevice(
    name: string,
    projectId: string,
    kind: DeviceKind,
    clientRequestId: string,
  ): Observable<DeviceWithKey> {
    return this.http.post<DeviceWithKey>('/api/timeclock/devices', {
      name,
      projectId,
      kind,
      clientRequestId,
    });
  }

  rotateKey(id: string, version: number): Observable<DeviceWithKey> {
    return this.http.post<DeviceWithKey>(`/api/timeclock/devices/${id}/rotate-key`, { version });
  }

  revoke(id: string, version: number): Observable<TimeClockDevice> {
    return this.http.post<TimeClockDevice>(`/api/timeclock/devices/${id}/revoke`, { version });
  }

  /** Projects that can still receive records, for the pickers. */
  projects(): Observable<readonly ProjectOption[]> {
    return this.http.get<readonly ProjectOption[]>('/api/projects/lookup');
  }

  /** Workers by name or employee number, for choosing whose badges to print. */
  findWorkers(search: string): Observable<readonly WorkerOption[]> {
    return this.http.get<readonly WorkerOption[]>('/api/workers/lookup', {
      params: new HttpParams().set('search', search),
    });
  }
}
