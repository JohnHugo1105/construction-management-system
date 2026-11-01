import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { PagedResult } from '../../core/api/paged-result';
import {
  OpenPurchaseOrderLine,
  ProjectChoice,
  PurchaseOrder,
  PurchaseOrderFields,
  PurchaseOrderListItem,
  PurchaseOrderListParams,
  RecordDeliveryFields,
  Supplier,
  SupplierFields,
  SupplierListItem,
  SupplierListParams,
  SupplierLookup,
} from './purchasing.models';

/** The purchase order endpoints, one method each. A filter at its default is left out of the request. */
@Injectable({ providedIn: 'root' })
export class PurchasingApi {
  private readonly http = inject(HttpClient);

  listOrders(params: PurchaseOrderListParams): Observable<PagedResult<PurchaseOrderListItem>> {
    let query = new HttpParams().set('page', params.page).set('pageSize', params.pageSize);
    if (params.projectId) query = query.set('projectId', params.projectId);
    if (params.supplierId) query = query.set('supplierId', params.supplierId);
    if (params.status) query = query.set('status', params.status);
    if (params.search) query = query.set('search', params.search);
    return this.http.get<PagedResult<PurchaseOrderListItem>>('/api/purchase-orders', {
      params: query,
    });
  }

  getOrder(id: string): Observable<PurchaseOrder> {
    return this.http.get<PurchaseOrder>(`/api/purchase-orders/${id}`);
  }

  createOrder(fields: PurchaseOrderFields, clientRequestId: string): Observable<PurchaseOrder> {
    return this.http.post<PurchaseOrder>('/api/purchase-orders', { ...fields, clientRequestId });
  }

  updateOrder(id: string, fields: PurchaseOrderFields, version: number): Observable<PurchaseOrder> {
    return this.http.put<PurchaseOrder>(`/api/purchase-orders/${id}`, { ...fields, version });
  }

  approveOrder(id: string, version: number): Observable<PurchaseOrder> {
    return this.http.post<PurchaseOrder>(`/api/purchase-orders/${id}/approve`, { version });
  }

  cancelOrder(id: string, reason: string, version: number): Observable<PurchaseOrder> {
    return this.http.post<PurchaseOrder>(`/api/purchase-orders/${id}/cancel`, { reason, version });
  }

  closeOrder(id: string, reason: string | null, version: number): Observable<PurchaseOrder> {
    return this.http.post<PurchaseOrder>(`/api/purchase-orders/${id}/close`, { reason, version });
  }

  /** Records what arrived against an approved order. The same request id with every attempt means a retry never records it twice. */
  recordDelivery(
    id: string,
    fields: RecordDeliveryFields,
    clientRequestId: string,
  ): Observable<PurchaseOrder> {
    return this.http.post<PurchaseOrder>(`/api/purchase-orders/${id}/receipts`, {
      ...fields,
      clientRequestId,
    });
  }

  /** Voids a delivery entered by mistake, with the version the person saw. */
  voidDelivery(
    id: string,
    receiptId: string,
    reason: string,
    version: number,
  ): Observable<PurchaseOrder> {
    return this.http.post<PurchaseOrder>(`/api/purchase-orders/${id}/receipts/${receiptId}/void`, {
      reason,
      version,
    });
  }

  /** Id, name, state and sub-projects of the projects for a picker. */
  projectLookup(includeArchived: boolean): Observable<readonly ProjectChoice[]> {
    return this.http.get<readonly ProjectChoice[]>('/api/projects/lookup', {
      params: new HttpParams().set('includeArchived', includeArchived),
    });
  }

  /** The lines of a project's approved orders: what a supplier invoice can be recorded against. */
  openLines(projectId: string): Observable<readonly OpenPurchaseOrderLine[]> {
    return this.http.get<readonly OpenPurchaseOrderLine[]>(
      `/api/projects/${projectId}/purchase-order-lines`,
    );
  }

  // ---- Firms ----------------------------------------------------------------------------------------------------------------

  listSuppliers(params: SupplierListParams): Observable<PagedResult<SupplierListItem>> {
    let query = new HttpParams().set('page', params.page).set('pageSize', params.pageSize);
    if (params.search) query = query.set('search', params.search);
    if (params.kind) query = query.set('kind', params.kind);
    if (params.includeArchived) query = query.set('includeArchived', true);
    return this.http.get<PagedResult<SupplierListItem>>('/api/suppliers', { params: query });
  }

  /** Every active firm of a kind, by name, for pickers. */
  supplierLookup(kind: string): Observable<readonly SupplierLookup[]> {
    return this.http.get<readonly SupplierLookup[]>('/api/suppliers/lookup', {
      params: new HttpParams().set('kind', kind),
    });
  }

  getSupplier(id: string): Observable<Supplier> {
    return this.http.get<Supplier>(`/api/suppliers/${id}`);
  }

  createSupplier(fields: SupplierFields): Observable<Supplier> {
    return this.http.post<Supplier>('/api/suppliers', fields);
  }

  updateSupplier(id: string, fields: SupplierFields, version: number): Observable<Supplier> {
    return this.http.put<Supplier>(`/api/suppliers/${id}`, { ...fields, version });
  }

  archiveSupplier(id: string, version: number): Observable<Supplier> {
    return this.http.post<Supplier>(`/api/suppliers/${id}/archive`, { version });
  }

  restoreSupplier(id: string, version: number): Observable<Supplier> {
    return this.http.post<Supplier>(`/api/suppliers/${id}/restore`, { version });
  }
}
