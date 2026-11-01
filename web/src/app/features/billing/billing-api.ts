import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import type { Progress } from '../progress/progress.models';
import { DEFAULT_PAGE_SIZE } from '../../core/api/paged-result';
import {
  Bill,
  BillPreview,
  BillingTerms,
  CreateBillRequest,
  ProjectBilling,
  Receivables,
  RecordPaymentRequest,
  SaveTermsRequest,
  UpdateBillRequest,
} from './billing.models';

/**
 * The billing endpoints, one method each (src/Cms.Api/Endpoints/BillingEndpoints.cs). Every bill action answers with the whole
 * bill, so a page shows the server's figures after each step. Nothing here sends anything to a client or collects money.
 */
@Injectable({ providedIn: 'root' })
export class BillingApi {
  private readonly http = inject(HttpClient);

  projectBilling(
    projectId: string,
    page = 1,
    pageSize = DEFAULT_PAGE_SIZE,
  ): Observable<ProjectBilling> {
    const params = new HttpParams().set('page', page).set('pageSize', pageSize);
    return this.http.get<ProjectBilling>(`/api/projects/${projectId}/billing`, { params });
  }

  saveTerms(projectId: string, request: SaveTermsRequest): Observable<BillingTerms> {
    return this.http.put<BillingTerms>(`/api/projects/${projectId}/billing/terms`, request);
  }

  /** The breakdown of a progress bill for this much work to date, worked out by the server. Nothing is saved. */
  preview(
    projectId: string,
    cumulativeWorkMinor: number,
    billingDate: string,
  ): Observable<BillPreview> {
    const params = new HttpParams()
      .set('cumulativeWorkMinor', cumulativeWorkMinor)
      .set('billingDate', billingDate);
    return this.http.get<BillPreview>(`/api/projects/${projectId}/billing/preview`, { params });
  }

  create(projectId: string, request: CreateBillRequest): Observable<Bill> {
    return this.http.post<Bill>(`/api/projects/${projectId}/billing/invoices`, request);
  }

  receivables(page = 1, pageSize = DEFAULT_PAGE_SIZE): Observable<Receivables> {
    const params = new HttpParams().set('page', page).set('pageSize', pageSize);
    return this.http.get<Receivables>('/api/billing/receivables', { params });
  }

  get(id: string): Observable<Bill> {
    return this.http.get<Bill>(`/api/client-invoices/${id}`);
  }

  update(id: string, request: UpdateBillRequest): Observable<Bill> {
    return this.http.put<Bill>(`/api/client-invoices/${id}`, request);
  }

  issue(id: string, version: number): Observable<Bill> {
    return this.http.post<Bill>(`/api/client-invoices/${id}/issue`, { version });
  }

  void(id: string, reason: string, version: number): Observable<Bill> {
    return this.http.post<Bill>(`/api/client-invoices/${id}/void`, { reason, version });
  }

  discard(id: string, reason: string, version: number): Observable<Bill> {
    return this.http.post<Bill>(`/api/client-invoices/${id}/discard`, { reason, version });
  }

  recordPayment(id: string, request: RecordPaymentRequest): Observable<Bill> {
    return this.http.post<Bill>(`/api/client-invoices/${id}/payments`, request);
  }

  voidPayment(id: string, paymentId: string, reason: string, version: number): Observable<Bill> {
    return this.http.post<Bill>(`/api/client-invoices/${id}/payments/${paymentId}/void`, {
      reason,
      version,
    });
  }

  /**
   * The project's progress, read only for the "suggested from progress" hint. That feature is separate: the caller ignores any
   * refusal or failure.
   */
  progress(projectId: string): Observable<Progress> {
    return this.http.get<Progress>(`/api/projects/${projectId}/progress`);
  }
}
