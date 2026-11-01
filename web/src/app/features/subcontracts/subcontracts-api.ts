import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { PagedResult } from '../../core/api/paged-result';
import {
  ClaimInput,
  ClaimPreview,
  FirmChoice,
  PaymentInput,
  ProjectChoice,
  Subcontract,
  SubcontractListItem,
  SubcontractListParams,
  SubcontractTerms,
} from './subcontracts.models';

/** The subcontract endpoints, one method each. Every action answers with the whole subcontract. */
@Injectable({ providedIn: 'root' })
export class SubcontractsApi {
  private readonly http = inject(HttpClient);
  private readonly base = '/api/subcontracts';

  list(params: SubcontractListParams): Observable<PagedResult<SubcontractListItem>> {
    let query = new HttpParams().set('page', params.page).set('pageSize', params.pageSize);
    if (params.projectId) query = query.set('projectId', params.projectId);
    if (params.status) query = query.set('status', params.status);
    if (params.search) query = query.set('search', params.search);
    return this.http.get<PagedResult<SubcontractListItem>>(this.base, { params: query });
  }

  get(id: string): Observable<Subcontract> {
    return this.http.get<Subcontract>(`${this.base}/${id}`);
  }

  create(projectId: string, terms: SubcontractTerms, clientRequestId: string): Observable<Subcontract> {
    return this.http.post<Subcontract>(this.base, { ...terms, projectId, clientRequestId });
  }

  update(id: string, terms: SubcontractTerms, version: number): Observable<Subcontract> {
    return this.http.put<Subcontract>(`${this.base}/${id}`, { ...terms, version });
  }

  award(id: string, version: number): Observable<Subcontract> {
    return this.http.post<Subcontract>(`${this.base}/${id}/award`, { version });
  }

  complete(id: string, version: number): Observable<Subcontract> {
    return this.http.post<Subcontract>(`${this.base}/${id}/complete`, { version });
  }

  cancel(id: string, reason: string, version: number): Observable<Subcontract> {
    return this.http.post<Subcontract>(`${this.base}/${id}/cancel`, { reason, version });
  }

  addVariation(
    id: string,
    variation: { description: string; amountMinor: number | null; variationDate: string },
    version: number,
  ): Observable<Subcontract> {
    return this.http.post<Subcontract>(`${this.base}/${id}/variations`, { ...variation, version });
  }

  previewClaim(id: string, cumulativeWorkMinor: number): Observable<ClaimPreview> {
    return this.http.get<ClaimPreview>(`${this.base}/${id}/claims/preview`, {
      params: new HttpParams().set('cumulativeWorkMinor', cumulativeWorkMinor),
    });
  }

  draftClaim(id: string, claim: ClaimInput, version: number, clientRequestId: string): Observable<Subcontract> {
    return this.http.post<Subcontract>(`${this.base}/${id}/claims`, { ...claim, version, clientRequestId });
  }

  updateClaim(id: string, claimId: string, claim: ClaimInput, version: number): Observable<Subcontract> {
    return this.http.put<Subcontract>(`${this.base}/${id}/claims/${claimId}`, { ...claim, version });
  }

  discardClaim(id: string, claimId: string, reason: string, version: number): Observable<Subcontract> {
    return this.http.post<Subcontract>(`${this.base}/${id}/claims/${claimId}/discard`, { reason, version });
  }

  certifyClaim(id: string, claimId: string, version: number): Observable<Subcontract> {
    return this.http.post<Subcontract>(`${this.base}/${id}/claims/${claimId}/certify`, { version });
  }

  voidClaim(id: string, claimId: string, reason: string, version: number): Observable<Subcontract> {
    return this.http.post<Subcontract>(`${this.base}/${id}/claims/${claimId}/void`, { reason, version });
  }

  payClaim(id: string, claimId: string, payment: PaymentInput, version: number, clientRequestId: string): Observable<Subcontract> {
    return this.http.post<Subcontract>(`${this.base}/${id}/claims/${claimId}/payments`, { ...payment, version, clientRequestId });
  }

  releaseRetention(id: string, payment: PaymentInput, version: number, clientRequestId: string): Observable<Subcontract> {
    return this.http.post<Subcontract>(`${this.base}/${id}/retention-releases`, { ...payment, version, clientRequestId });
  }

  voidPayment(id: string, paymentId: string, reason: string, version: number): Observable<Subcontract> {
    return this.http.post<Subcontract>(`${this.base}/${id}/payments/${paymentId}/void`, { reason, version });
  }

  /** Active firms that do subcontract work, for the picker. */
  firms(): Observable<readonly FirmChoice[]> {
    return this.http.get<readonly FirmChoice[]>('/api/suppliers/lookup', { params: new HttpParams().set('kind', 'Subcontractor') });
  }

  /** Projects that can still receive records, for the picker. */
  projects(): Observable<readonly ProjectChoice[]> {
    return this.http.get<readonly ProjectChoice[]>('/api/projects/lookup');
  }
}
