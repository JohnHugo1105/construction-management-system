import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { PagedResult } from '../../core/api/paged-result';
import {
  AssignRequest,
  AttendanceBatchRequest,
  AttendanceEntry,
  AttendanceSheet,
  BulkAssignRequest,
  CaseRequest,
  ChangeRateRequest,
  ChangeStatusRequest,
  HireRequest,
  ProfileRequest,
  ProjectChoice,
  ReinstateRequest,
  ReleaseRequest,
  ReviseAttendanceRequest,
  VoidAttendanceRequest,
  WorkerDetail,
  WorkerListItem,
  WorkerListParams,
  WorkerStatutory,
} from './workforce.models';

/** The worker and attendance endpoints, one method each. Every worker action answers with the whole worker. */
@Injectable({ providedIn: 'root' })
export class WorkforceApi {
  private readonly http = inject(HttpClient);

  list(params: WorkerListParams): Observable<PagedResult<WorkerListItem>> {
    let query = new HttpParams().set('page', params.page).set('pageSize', params.pageSize);
    if (params.search) query = query.set('search', params.search);
    if (params.status) query = query.set('status', params.status);
    if (params.projectId) query = query.set('projectId', params.projectId);
    return this.http.get<PagedResult<WorkerListItem>>('/api/workers', { params: query });
  }

  get(id: string): Observable<WorkerDetail> {
    return this.http.get<WorkerDetail>(`/api/workers/${id}`);
  }

  hire(request: HireRequest): Observable<WorkerDetail> {
    return this.http.post<WorkerDetail>('/api/workers', request);
  }

  updateProfile(id: string, request: ProfileRequest): Observable<WorkerDetail> {
    return this.http.put<WorkerDetail>(`/api/workers/${id}`, request);
  }

  /** Which contributions the worker pays and whether tax is withheld (people who manage workers and see pay). */
  setStatutory(id: string, flags: WorkerStatutory, version: number): Observable<WorkerDetail> {
    return this.http.put<WorkerDetail>(`/api/workers/${id}/statutory`, { ...flags, version });
  }

  changeRate(id: string, request: ChangeRateRequest): Observable<WorkerDetail> {
    return this.http.post<WorkerDetail>(`/api/workers/${id}/change-rate`, request);
  }

  assign(id: string, request: AssignRequest): Observable<WorkerDetail> {
    return this.http.post<WorkerDetail>(`/api/workers/${id}/assign`, request);
  }

  /** Posts a crew to one project, all or nothing. A refusal is a 422 with `errors.workers`, one line per worker. */
  assignMany(request: BulkAssignRequest): Observable<readonly WorkerListItem[]> {
    return this.http.post<readonly WorkerListItem[]>('/api/workers/assign', request);
  }

  release(id: string, request: ReleaseRequest): Observable<WorkerDetail> {
    return this.http.post<WorkerDetail>(`/api/workers/${id}/release`, request);
  }

  changeStatus(id: string, request: ChangeStatusRequest): Observable<WorkerDetail> {
    return this.http.post<WorkerDetail>(`/api/workers/${id}/status`, request);
  }

  reinstate(id: string, request: ReinstateRequest): Observable<WorkerDetail> {
    return this.http.post<WorkerDetail>(`/api/workers/${id}/reinstate`, request);
  }

  addCase(id: string, request: CaseRequest): Observable<WorkerDetail> {
    return this.http.post<WorkerDetail>(`/api/workers/${id}/cases`, request);
  }

  /** Projects that can still receive records, with their sub-projects, for pickers. */
  projectChoices(): Observable<readonly ProjectChoice[]> {
    return this.http.get<readonly ProjectChoice[]>('/api/projects/lookup');
  }

  /** A worker's latest attendance, newest day first. */
  recentAttendance(workerId: string, pageSize: number): Observable<PagedResult<AttendanceEntry>> {
    const query = new HttpParams()
      .set('workerId', workerId)
      .set('page', 1)
      .set('pageSize', pageSize);
    return this.http.get<PagedResult<AttendanceEntry>>('/api/attendance', { params: query });
  }

  /** One project's sheet for one day. */
  sheet(projectId: string, date: string): Observable<AttendanceSheet> {
    return this.http.get<AttendanceSheet>(`/api/projects/${projectId}/attendance`, {
      params: new HttpParams().set('date', date),
    });
  }

  /** A crew's day, all or nothing. Field errors come keyed `entries[3].regularHours`. */
  recordDay(request: AttendanceBatchRequest): Observable<readonly AttendanceEntry[]> {
    return this.http.post<readonly AttendanceEntry[]>('/api/attendance/batch', request);
  }

  reviseAttendance(id: string, request: ReviseAttendanceRequest): Observable<AttendanceEntry> {
    return this.http.put<AttendanceEntry>(`/api/attendance/${id}`, request);
  }

  voidAttendance(id: string, request: VoidAttendanceRequest): Observable<AttendanceEntry> {
    return this.http.post<AttendanceEntry>(`/api/attendance/${id}/void`, request);
  }

  /** The company's paid hours in a working day, from the company settings that anyone signed in can read. */
  hoursPerDay(): Observable<number> {
    return this.http
      .get<{ readonly hoursPerDay: number }>('/api/organization')
      .pipe(map((organization) => organization.hoursPerDay));
  }
}
