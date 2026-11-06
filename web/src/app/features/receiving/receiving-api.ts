import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  ReceivingListParams,
  ReceivingOrder,
  ReceivingOrderPage,
  RecordDeliveryFields,
} from './receiving.models';

/**
 * The receiving endpoints, one method each. This service talks only to `/api/receiving/...`: the purchase order endpoints carry
 * prices, and the person using this screen may not see them.
 */
@Injectable({ providedIn: 'root' })
export class ReceivingApi {
  private readonly http = inject(HttpClient);

  listOrders(params: ReceivingListParams): Observable<ReceivingOrderPage> {
    let query = new HttpParams().set('page', params.page).set('pageSize', params.pageSize);
    if (params.search) query = query.set('search', params.search);
    return this.http.get<ReceivingOrderPage>('/api/receiving/orders', { params: query });
  }

  getOrder(id: string): Observable<ReceivingOrder> {
    return this.http.get<ReceivingOrder>(`/api/receiving/orders/${id}`);
  }

  /** Records what arrived. The same request id with every attempt means a retry never records it twice. */
  recordDelivery(
    id: string,
    fields: RecordDeliveryFields,
    clientRequestId: string,
  ): Observable<ReceivingOrder> {
    return this.http.post<ReceivingOrder>(`/api/receiving/orders/${id}/deliveries`, {
      ...fields,
      clientRequestId,
    });
  }

  /** Voids a delivery entered by mistake, with the version the person saw. */
  voidDelivery(
    id: string,
    deliveryId: string,
    reason: string,
    version: number,
  ): Observable<ReceivingOrder> {
    return this.http.post<ReceivingOrder>(
      `/api/receiving/orders/${id}/deliveries/${deliveryId}/void`,
      { reason, version },
    );
  }
}
