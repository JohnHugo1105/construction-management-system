import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { budgetLineQuery, costEntryQuery } from './costing-query';
import {
  BudgetLine,
  BudgetLineFilters,
  CostEntry,
  CostEntryFields,
  CostEntryFilters,
  CostSummary,
  CostingReference,
  LineFields,
  PageParams,
  TotaledPage,
} from './costing.models';

/** The costing endpoints, one method each. A filter at its default is left out of the request. */
@Injectable({ providedIn: 'root' })
export class CostingApi {
  private readonly http = inject(HttpClient);

  reference(): Observable<CostingReference> {
    return this.http.get<CostingReference>('/api/costing/reference');
  }

  costSummary(projectId: string): Observable<CostSummary> {
    return this.http.get<CostSummary>(`/api/projects/${projectId}/cost-summary`);
  }

  // ---- Budget lines ---------------------------------------------------------------------------------------------------------

  listBudgetLines(
    projectId: string,
    filters: BudgetLineFilters,
    page: PageParams,
  ): Observable<TotaledPage<BudgetLine>> {
    return this.http.get<TotaledPage<BudgetLine>>(`/api/projects/${projectId}/budget-lines`, {
      params: listParams(page, budgetLineQuery(filters)),
    });
  }

  getBudgetLine(id: string): Observable<BudgetLine> {
    return this.http.get<BudgetLine>(`/api/budget-lines/${id}`);
  }

  createBudgetLine(
    projectId: string,
    fields: LineFields,
    clientRequestId: string,
  ): Observable<BudgetLine> {
    return this.http.post<BudgetLine>(`/api/projects/${projectId}/budget-lines`, {
      ...fields,
      clientRequestId,
    });
  }

  updateBudgetLine(id: string, fields: LineFields, version: number): Observable<BudgetLine> {
    return this.http.put<BudgetLine>(`/api/budget-lines/${id}`, { ...fields, version });
  }

  removeBudgetLine(id: string, reason: string, version: number): Observable<BudgetLine> {
    return this.http.post<BudgetLine>(`/api/budget-lines/${id}/remove`, { reason, version });
  }

  // ---- Cost entries ---------------------------------------------------------------------------------------------------------

  listCostEntries(
    projectId: string,
    filters: CostEntryFilters,
    page: PageParams,
  ): Observable<TotaledPage<CostEntry>> {
    return this.http.get<TotaledPage<CostEntry>>(`/api/projects/${projectId}/cost-entries`, {
      params: listParams(page, costEntryQuery(filters)),
    });
  }

  getCostEntry(id: string): Observable<CostEntry> {
    return this.http.get<CostEntry>(`/api/cost-entries/${id}`);
  }

  createCostEntry(
    projectId: string,
    fields: CostEntryFields,
    clientRequestId: string,
  ): Observable<CostEntry> {
    return this.http.post<CostEntry>(`/api/projects/${projectId}/cost-entries`, {
      ...fields,
      clientRequestId,
    });
  }

  updateCostEntry(id: string, fields: CostEntryFields, version: number): Observable<CostEntry> {
    return this.http.put<CostEntry>(`/api/cost-entries/${id}`, { ...fields, version });
  }

  voidCostEntry(id: string, reason: string, version: number): Observable<CostEntry> {
    return this.http.post<CostEntry>(`/api/cost-entries/${id}/void`, { reason, version });
  }
}

function listParams(page: PageParams, query: Record<string, string>): HttpParams {
  return new HttpParams({
    fromObject: { page: String(page.page), pageSize: String(page.pageSize), ...query },
  });
}
