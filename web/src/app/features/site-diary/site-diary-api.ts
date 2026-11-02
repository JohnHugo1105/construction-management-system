import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { PagedResult } from '../../core/api/paged-result';
import { DiaryDetailsInput, DiaryEntry, DiaryFilledIn, DiaryListItem } from './site-diary.models';

/** The site diary endpoints of one project, one method each. Every change answers with the whole report. */
@Injectable({ providedIn: 'root' })
export class SiteDiaryApi {
  private readonly http = inject(HttpClient);

  private base(projectId: string): string {
    return `/api/projects/${projectId}/diary`;
  }

  /** A project's reports, newest first. */
  list(projectId: string, page: number, pageSize: number): Observable<PagedResult<DiaryListItem>> {
    const params = new HttpParams().set('page', page).set('pageSize', pageSize);
    return this.http.get<PagedResult<DiaryListItem>>(this.base(projectId), { params });
  }

  get(projectId: string, date: string): Observable<DiaryEntry> {
    return this.http.get<DiaryEntry>(`${this.base(projectId)}/${date}`);
  }

  /** Who was on site and what was delivered on a day, for the form that writes that day's report. */
  filledIn(projectId: string, date: string): Observable<DiaryFilledIn> {
    return this.http.get<DiaryFilledIn>(`${this.base(projectId)}/${date}/filled-in`);
  }

  create(
    projectId: string,
    reportDate: string,
    input: DiaryDetailsInput,
    clientRequestId: string,
  ): Observable<DiaryEntry> {
    return this.http.post<DiaryEntry>(this.base(projectId), {
      ...input,
      reportDate,
      clientRequestId,
    });
  }

  correct(
    projectId: string,
    date: string,
    input: DiaryDetailsInput,
    version: number,
  ): Observable<DiaryEntry> {
    return this.http.put<DiaryEntry>(`${this.base(projectId)}/${date}`, { ...input, version });
  }

  voidReport(
    projectId: string,
    date: string,
    reason: string,
    version: number,
  ): Observable<DiaryEntry> {
    return this.http.post<DiaryEntry>(`${this.base(projectId)}/${date}/void`, { reason, version });
  }
}
