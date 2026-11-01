import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { PagedResult } from '../../core/api/paged-result';
import {
  Bond,
  BondDetailsInput,
  BondListItem,
  BondListParams,
  BondTotals,
  NewBondInput,
  ProjectChoice,
} from './bonds.models';

/** The bonds endpoints, one method each. Every step answers with the whole bond. */
@Injectable({ providedIn: 'root' })
export class BondsApi {
  private readonly http = inject(HttpClient);
  private readonly base = '/api/bonds';

  list(params: BondListParams): Observable<PagedResult<BondListItem>> {
    let query = new HttpParams().set('page', params.page).set('pageSize', params.pageSize);
    if (params.projectId) query = query.set('projectId', params.projectId);
    if (params.direction) query = query.set('direction', params.direction);
    if (params.kind) query = query.set('kind', params.kind);
    if (params.status) query = query.set('status', params.status);
    if (params.expiry) query = query.set('expiry', params.expiry);
    return this.http.get<PagedResult<BondListItem>>(this.base, { params: query });
  }

  totals(): Observable<BondTotals> {
    return this.http.get<BondTotals>(`${this.base}/totals`);
  }

  get(id: string): Observable<Bond> {
    return this.http.get<Bond>(`${this.base}/${id}`);
  }

  /** A project's own bonds that are not voided, Active first. */
  forProject(projectId: string): Observable<readonly BondListItem[]> {
    return this.http.get<readonly BondListItem[]>(`/api/projects/${projectId}/bonds`);
  }

  create(projectId: string, input: NewBondInput, clientRequestId: string): Observable<Bond> {
    return this.http.post<Bond>(this.base, { ...input, projectId, clientRequestId });
  }

  update(id: string, input: BondDetailsInput, version: number): Observable<Bond> {
    return this.http.put<Bond>(`${this.base}/${id}`, { ...input, version });
  }

  extend(id: string, validUntil: string, note: string | null, version: number): Observable<Bond> {
    return this.http.post<Bond>(`${this.base}/${id}/extend`, { validUntil, note, version });
  }

  release(id: string, releasedOn: string, note: string | null, version: number): Observable<Bond> {
    return this.http.post<Bond>(`${this.base}/${id}/release`, { releasedOn, note, version });
  }

  markCalled(id: string, calledOn: string, note: string, version: number): Observable<Bond> {
    return this.http.post<Bond>(`${this.base}/${id}/call`, { calledOn, note, version });
  }

  voidBond(id: string, reason: string, version: number): Observable<Bond> {
    return this.http.post<Bond>(`${this.base}/${id}/void`, { reason, version });
  }

  /** Projects for a picker; the register's filter also lists archived ones, because their bonds still lapse. */
  projects(includeArchived: boolean): Observable<readonly ProjectChoice[]> {
    return this.http.get<readonly ProjectChoice[]>('/api/projects/lookup', {
      params: new HttpParams().set('includeArchived', includeArchived),
    });
  }
}
