import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { CreateUserRequest, UpdateUserRequest, UserAccount } from './users.models';

/** The user administration endpoints (`users.manage`), one method each. */
@Injectable({ providedIn: 'root' })
export class UsersApi {
  private readonly http = inject(HttpClient);

  list(): Observable<UserAccount[]> {
    return this.http.get<UserAccount[]>('/api/users');
  }

  get(id: string): Observable<UserAccount> {
    return this.http.get<UserAccount>(`/api/users/${id}`);
  }

  create(request: CreateUserRequest): Observable<UserAccount> {
    return this.http.post<UserAccount>('/api/users', request);
  }

  update(id: string, request: UpdateUserRequest): Observable<UserAccount> {
    return this.http.put<UserAccount>(`/api/users/${id}`, request);
  }

  deactivate(id: string): Observable<UserAccount> {
    return this.http.post<UserAccount>(`/api/users/${id}/deactivate`, {});
  }

  reactivate(id: string): Observable<UserAccount> {
    return this.http.post<UserAccount>(`/api/users/${id}/reactivate`, {});
  }

  resetPassword(id: string, newPassword: string): Observable<void> {
    return this.http.post<void>(`/api/users/${id}/reset-password`, { newPassword });
  }
}
