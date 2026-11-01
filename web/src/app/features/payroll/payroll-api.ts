import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { PagedResult } from '../../core/api/paged-result';
import {
  CreateRunRequest,
  DeductionInput,
  PayrollListParams,
  PayrollRun,
  PayrollRunListItem,
} from './payroll.models';

/**
 * Where a run's payroll register (a CSV for a spreadsheet) is downloaded. It is a plain link: the browser sends the sign-in
 * cookie, and no pay figure ever appears in the address.
 */
export function payrollExportUrl(runId: string): string {
  return `/api/payroll-runs/${encodeURIComponent(runId)}/export`;
}

/** The payroll endpoints, one method each. Every action answers with the whole run. */
@Injectable({ providedIn: 'root' })
export class PayrollApi {
  private readonly http = inject(HttpClient);

  list(params: PayrollListParams): Observable<PagedResult<PayrollRunListItem>> {
    const query = new HttpParams().set('page', params.page).set('pageSize', params.pageSize);
    return this.http.get<PagedResult<PayrollRunListItem>>('/api/payroll-runs', { params: query });
  }

  get(id: string): Observable<PayrollRun> {
    return this.http.get<PayrollRun>(`/api/payroll-runs/${id}`);
  }

  create(request: CreateRunRequest): Observable<PayrollRun> {
    return this.http.post<PayrollRun>('/api/payroll-runs', request);
  }

  /** Prepares the 13th-month pay for a calendar year. The request id makes a second press return the same payroll. */
  createThirteenthMonth(year: number, clientRequestId: string): Observable<PayrollRun> {
    return this.http.post<PayrollRun>('/api/payroll-runs/thirteenth-month', {
      year,
      clientRequestId,
    });
  }

  setDeductions(
    runId: string,
    workerId: string,
    deductions: readonly DeductionInput[],
    version: number,
  ): Observable<PayrollRun> {
    return this.http.put<PayrollRun>(`/api/payroll-runs/${runId}/workers/${workerId}/deductions`, {
      deductions: deductions.map((item) => ({
        description: item.description,
        amountMinor: item.amountMinor,
      })),
      version,
    });
  }

  approve(id: string, version: number): Observable<PayrollRun> {
    return this.http.post<PayrollRun>(`/api/payroll-runs/${id}/approve`, { version });
  }

  void(id: string, reason: string, version: number): Observable<PayrollRun> {
    return this.http.post<PayrollRun>(`/api/payroll-runs/${id}/void`, { reason, version });
  }
}
