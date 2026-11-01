import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { PagedResult } from '../../core/api/paged-result';
import {
  BudgetBaseline,
  ChangeOrder,
  ChangeOrderDetails,
  ChangeOrderLineFields,
  ChangeOrderListItem,
} from './change-orders.models';

/** The budget baseline and change order endpoints, one method each. */
@Injectable({ providedIn: 'root' })
export class ChangeOrdersApi {
  private readonly http = inject(HttpClient);

  baseline(projectId: string): Observable<BudgetBaseline> {
    return this.http.get<BudgetBaseline>(`/api/projects/${projectId}/budget-baseline`);
  }

  lock(projectId: string, projectVersion: number): Observable<BudgetBaseline> {
    return this.http.post<BudgetBaseline>(`/api/projects/${projectId}/budget-baseline/lock`, {
      version: projectVersion,
    });
  }

  unlock(projectId: string, reason: string, projectVersion: number): Observable<BudgetBaseline> {
    return this.http.post<BudgetBaseline>(`/api/projects/${projectId}/budget-baseline/unlock`, {
      reason,
      version: projectVersion,
    });
  }

  list(
    projectId: string,
    page: number,
    pageSize: number,
  ): Observable<PagedResult<ChangeOrderListItem>> {
    return this.http.get<PagedResult<ChangeOrderListItem>>(
      `/api/projects/${projectId}/change-orders`,
      {
        params: new HttpParams({ fromObject: { page: String(page), pageSize: String(pageSize) } }),
      },
    );
  }

  get(id: string): Observable<ChangeOrder> {
    return this.http.get<ChangeOrder>(`/api/change-orders/${id}`);
  }

  create(
    projectId: string,
    details: ChangeOrderDetails,
    clientRequestId: string,
  ): Observable<ChangeOrder> {
    return this.http.post<ChangeOrder>(`/api/projects/${projectId}/change-orders`, {
      ...details,
      clientRequestId,
    });
  }

  update(id: string, details: ChangeOrderDetails, version: number): Observable<ChangeOrder> {
    return this.http.put<ChangeOrder>(`/api/change-orders/${id}`, { ...details, version });
  }

  addLine(id: string, line: ChangeOrderLineFields, version: number): Observable<ChangeOrder> {
    return this.http.post<ChangeOrder>(`/api/change-orders/${id}/lines`, { ...line, version });
  }

  updateLine(
    id: string,
    lineId: string,
    line: ChangeOrderLineFields,
    version: number,
  ): Observable<ChangeOrder> {
    return this.http.put<ChangeOrder>(`/api/change-orders/${id}/lines/${lineId}`, {
      ...line,
      version,
    });
  }

  removeLine(id: string, lineId: string, version: number): Observable<ChangeOrder> {
    return this.http.post<ChangeOrder>(`/api/change-orders/${id}/lines/${lineId}/remove`, {
      version,
    });
  }

  submit(id: string, version: number): Observable<ChangeOrder> {
    return this.http.post<ChangeOrder>(`/api/change-orders/${id}/submit`, { version });
  }

  approve(id: string, note: string | null, version: number): Observable<ChangeOrder> {
    return this.http.post<ChangeOrder>(`/api/change-orders/${id}/approve`, { note, version });
  }

  reject(id: string, reason: string, version: number): Observable<ChangeOrder> {
    return this.http.post<ChangeOrder>(`/api/change-orders/${id}/reject`, { reason, version });
  }

  void(id: string, reason: string, version: number): Observable<ChangeOrder> {
    return this.http.post<ChangeOrder>(`/api/change-orders/${id}/void`, { reason, version });
  }

  discard(id: string, reason: string, version: number): Observable<ChangeOrder> {
    return this.http.post<ChangeOrder>(`/api/change-orders/${id}/discard`, { reason, version });
  }
}
