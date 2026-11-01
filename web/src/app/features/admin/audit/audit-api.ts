import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { PagedResult } from '../../../core/api/paged-result';
import { AuditEntry, AuditListParams } from './audit.models';

/** The activity log endpoint (`audit.read`). */
@Injectable({ providedIn: 'root' })
export class AuditApi {
  private readonly http = inject(HttpClient);

  list(params: AuditListParams): Observable<PagedResult<AuditEntry>> {
    let query = new HttpParams().set('page', params.page).set('pageSize', params.pageSize);
    if (params.search) query = query.set('search', params.search);
    if (params.entityType) query = query.set('entityType', params.entityType);
    if (params.from) query = query.set('from', params.from);
    if (params.to) query = query.set('to', params.to);
    return this.http.get<PagedResult<AuditEntry>>('/api/audit', { params: query });
  }
}
