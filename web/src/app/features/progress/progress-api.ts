import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  Progress,
  ProgressHistoryPage,
  RecordProgressRequest,
  RecordProgressResult,
} from './progress.models';

/** The progress endpoints, one method each. */
@Injectable({ providedIn: 'root' })
export class ProgressApi {
  private readonly http = inject(HttpClient);

  /** Progress and earned value as of today. */
  get(projectId: string): Observable<Progress> {
    return this.http.get<Progress>(`/api/projects/${projectId}/progress`);
  }

  /** The change history of one budget line, newest first. */
  history(budgetLineId: string, page: number, pageSize: number): Observable<ProgressHistoryPage> {
    const params = new HttpParams().set('page', page).set('pageSize', pageSize);
    return this.http.get<ProgressHistoryPage>(`/api/budget-lines/${budgetLineId}/progress`, {
      params,
    });
  }

  record(projectId: string, request: RecordProgressRequest): Observable<RecordProgressResult> {
    return this.http.post<RecordProgressResult>(`/api/projects/${projectId}/progress`, request);
  }
}
