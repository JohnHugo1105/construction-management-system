import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  BootstrapRequest,
  ChangePasswordRequest,
  Session,
  SetupStatus,
  SignInRequest,
} from './session.models';

/** The identity endpoints, one method each. Nothing here decides anything; SessionState does. */
@Injectable({ providedIn: 'root' })
export class IdentityApi {
  private readonly http = inject(HttpClient);

  /** Asks the API to set the anti-forgery cookie that the HTTP layer then sends back on every write. */
  csrf(): Observable<void> {
    return this.http.get<void>('/api/identity/csrf');
  }

  setupStatus(): Observable<SetupStatus> {
    return this.http.get<SetupStatus>('/api/identity/setup-status');
  }

  signIn(request: SignInRequest): Observable<Session> {
    return this.http.post<Session>('/api/identity/sign-in', request);
  }

  bootstrap(request: BootstrapRequest): Observable<Session> {
    return this.http.post<Session>('/api/identity/bootstrap', request);
  }

  signOut(): Observable<void> {
    return this.http.post<void>('/api/identity/sign-out', {});
  }

  session(): Observable<Session> {
    return this.http.get<Session>('/api/identity/session');
  }

  changePassword(request: ChangePasswordRequest): Observable<Session> {
    return this.http.post<Session>('/api/identity/change-password', request);
  }
}
