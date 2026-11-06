import { HttpBackend, HttpClient, HttpErrorResponse, HttpHeaders } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, map, of } from 'rxjs';
import { KioskScan, PushResult } from './timeclock.models';

/** How a kiosk send ended, in the terms the kiosk acts on. */
export type KioskSendResult =
  | { readonly kind: 'answered'; readonly result: PushResult }
  /** The key is not accepted (unknown, rotated or revoked). The scans stay on the tablet. */
  | { readonly kind: 'key-refused' }
  /** The server could not be reached, was busy or failed: keep the scans and try again later. */
  | { readonly kind: 'unreachable' };

/**
 * The kiosk's one route, `POST /api/timeclock/punches`, authenticated by the device key in the `X-Device-Key` header. It goes straight to the
 * network, past the app's interceptors: the kiosk is not a signed-in session, so a refused key must never sign anyone out or redirect, and it
 * sends no cookie-based token.
 */
@Injectable({ providedIn: 'root' })
export class KioskApi {
  private readonly http = new HttpClient(inject(HttpBackend));

  send(key: string, scans: readonly KioskScan[]): Observable<KioskSendResult> {
    return this.http
      .post<PushResult>(
        '/api/timeclock/punches',
        { punches: scans },
        {
          headers: new HttpHeaders({ 'X-Device-Key': key }),
        },
      )
      .pipe(
        map((result): KioskSendResult => ({ kind: 'answered', result })),
        catchError((failure: unknown) =>
          of<KioskSendResult>(
            failure instanceof HttpErrorResponse && failure.status === 401
              ? { kind: 'key-refused' }
              : { kind: 'unreachable' },
          ),
        ),
      );
  }

  /**
   * Checks a key without recording anything: an empty batch is refused as empty (422) only after the key is accepted, and with 401 when it isn't.
   * Null when the server can't be reached (the key can't be checked now).
   */
  checkKey(key: string): Observable<boolean | null> {
    return this.http
      .post(
        '/api/timeclock/punches',
        { punches: [] },
        { headers: new HttpHeaders({ 'X-Device-Key': key }) },
      )
      .pipe(
        map(() => true),
        catchError((failure: unknown) => {
          if (!(failure instanceof HttpErrorResponse)) return of(null);
          if (failure.status === 422) return of(true);
          if (failure.status === 401) return of(false);
          return of(null);
        }),
      );
  }
}
