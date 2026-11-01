import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { PagedResult } from '../../core/api/paged-result';
import { PayrollRun, StatutoryAgency } from './payroll.models';
import {
  PayrollStatutory,
  SaveScheduleRequest,
  StatutorySchedule,
  StatutoryScheduleListItem,
  StatutoryStatus,
} from './statutory.models';

/** The contribution and tax endpoints, one method each. */
@Injectable({ providedIn: 'root' })
export class StatutoryApi {
  private readonly http = inject(HttpClient);

  status(): Observable<StatutoryStatus> {
    return this.http.get<StatutoryStatus>('/api/statutory/status');
  }

  list(includeRetired: boolean): Observable<PagedResult<StatutoryScheduleListItem>> {
    const params = new HttpParams().set('includeRetired', includeRetired).set('pageSize', 100);
    return this.http.get<PagedResult<StatutoryScheduleListItem>>('/api/statutory/schedules', {
      params,
    });
  }

  get(id: string): Observable<StatutorySchedule> {
    return this.http.get<StatutorySchedule>(`/api/statutory/schedules/${encodeURIComponent(id)}`);
  }

  create(request: SaveScheduleRequest): Observable<StatutorySchedule> {
    return this.http.post<StatutorySchedule>('/api/statutory/schedules', request);
  }

  update(id: string, request: SaveScheduleRequest): Observable<StatutorySchedule> {
    return this.http.put<StatutorySchedule>(
      `/api/statutory/schedules/${encodeURIComponent(id)}`,
      request,
    );
  }

  retire(id: string, version: number): Observable<StatutorySchedule> {
    return this.http.post<StatutorySchedule>(
      `/api/statutory/schedules/${encodeURIComponent(id)}/retire`,
      { version },
    );
  }

  runStatutory(runId: string): Observable<PayrollStatutory> {
    return this.http.get<PayrollStatutory>(
      `/api/payroll-runs/${encodeURIComponent(runId)}/statutory`,
    );
  }

  adjust(
    runId: string,
    workerId: string,
    agency: StatutoryAgency,
    amountMinor: number,
    reason: string,
    version: number,
  ): Observable<PayrollRun> {
    return this.http.post<PayrollRun>(
      `/api/payroll-runs/${encodeURIComponent(runId)}/workers/${encodeURIComponent(workerId)}/statutory/${agency}/adjust`,
      { amountMinor, reason, version },
    );
  }

  recalculate(runId: string, version: number): Observable<PayrollRun> {
    return this.http.post<PayrollRun>(
      `/api/payroll-runs/${encodeURIComponent(runId)}/recalculate-statutory`,
      { version },
    );
  }
}
