import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { Dashboard } from './dashboard.models';

/** The dashboard report endpoint. */
@Injectable({ providedIn: 'root' })
export class DashboardApi {
  private readonly http = inject(HttpClient);

  /** Every project's budget against spent cost. Archived projects are left out unless asked for. */
  load(includeArchived: boolean): Observable<Dashboard> {
    const params = includeArchived ? new HttpParams().set('includeArchived', 'true') : undefined;
    return this.http.get<Dashboard>('/api/reports/dashboard', { params });
  }
}
