import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { Capabilities } from './capabilities.models';

/** The System readiness endpoint (administrators only). */
@Injectable({ providedIn: 'root' })
export class CapabilitiesApi {
  private readonly http = inject(HttpClient);

  list(): Observable<Capabilities> {
    return this.http.get<Capabilities>('/api/capabilities');
  }
}
