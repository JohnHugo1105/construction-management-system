import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { PagedResult } from '../../core/api/paged-result';
import {
  AdjustRequest,
  InventoryItem,
  InventoryListItem,
  InventoryListParams,
  ProjectLookup,
  ProjectStock,
  ReceiveRequest,
  RegisterItemRequest,
  RetireRequest,
  ReverseRequest,
  StockMovement,
  TransferRequest,
  UpdateItemRequest,
  WriteOffRequest,
} from './inventory.models';

/** The inventory endpoints, one method each. Every write answers with the item as it now stands. */
@Injectable({ providedIn: 'root' })
export class InventoryApi {
  private readonly http = inject(HttpClient);

  list(params: InventoryListParams): Observable<PagedResult<InventoryListItem>> {
    let query = new HttpParams().set('page', params.page).set('pageSize', params.pageSize);
    if (params.search) query = query.set('search', params.search);
    if (params.category) query = query.set('category', params.category);
    if (params.status) query = query.set('status', params.status);
    if (params.projectId) query = query.set('projectId', params.projectId);
    if (params.inWarehouse) query = query.set('inWarehouse', true);
    return this.http.get<PagedResult<InventoryListItem>>('/api/inventory/items', {
      params: query,
    });
  }

  get(id: string): Observable<InventoryItem> {
    return this.http.get<InventoryItem>(`/api/inventory/items/${id}`);
  }

  /** The item's history, newest first. */
  movements(id: string, page: number, pageSize: number): Observable<PagedResult<StockMovement>> {
    const params = new HttpParams().set('page', page).set('pageSize', pageSize);
    return this.http.get<PagedResult<StockMovement>>(`/api/inventory/items/${id}/movements`, {
      params,
    });
  }

  /** What is on one project now. */
  projectStock(
    projectId: string,
    page: number,
    pageSize: number,
  ): Observable<PagedResult<ProjectStock>> {
    const params = new HttpParams().set('page', page).set('pageSize', pageSize);
    return this.http.get<PagedResult<ProjectStock>>(`/api/projects/${projectId}/inventory`, {
      params,
    });
  }

  /** Id, name and state of the projects for a picker. Archived ones only when asked for. */
  projectLookup(includeArchived: boolean): Observable<ProjectLookup[]> {
    const params = new HttpParams().set('includeArchived', includeArchived);
    return this.http.get<ProjectLookup[]>('/api/projects/lookup', { params });
  }

  register(request: RegisterItemRequest): Observable<InventoryItem> {
    return this.http.post<InventoryItem>('/api/inventory/items', request);
  }

  update(id: string, request: UpdateItemRequest): Observable<InventoryItem> {
    return this.http.put<InventoryItem>(`/api/inventory/items/${id}`, request);
  }

  receive(id: string, request: ReceiveRequest): Observable<InventoryItem> {
    return this.http.post<InventoryItem>(`/api/inventory/items/${id}/receive`, request);
  }

  transfer(id: string, request: TransferRequest): Observable<InventoryItem> {
    return this.http.post<InventoryItem>(`/api/inventory/items/${id}/transfer`, request);
  }

  adjust(id: string, request: AdjustRequest): Observable<InventoryItem> {
    return this.http.post<InventoryItem>(`/api/inventory/items/${id}/adjust`, request);
  }

  writeOff(id: string, request: WriteOffRequest): Observable<InventoryItem> {
    return this.http.post<InventoryItem>(`/api/inventory/items/${id}/write-off`, request);
  }

  reverse(id: string, movementId: string, request: ReverseRequest): Observable<InventoryItem> {
    return this.http.post<InventoryItem>(
      `/api/inventory/items/${id}/movements/${movementId}/reverse`,
      request,
    );
  }

  retire(id: string, request: RetireRequest): Observable<InventoryItem> {
    return this.http.post<InventoryItem>(`/api/inventory/items/${id}/retire`, request);
  }
}
