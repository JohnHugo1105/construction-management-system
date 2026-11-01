import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { Organization, UpdateOrganizationRequest } from './organization.models';

/** The company settings endpoints. Anyone signed in can read; `settings.manage` changes. */
@Injectable({ providedIn: 'root' })
export class OrganizationApi {
  private readonly http = inject(HttpClient);

  get(): Observable<Organization> {
    return this.http.get<Organization>('/api/organization');
  }

  update(request: UpdateOrganizationRequest): Observable<Organization> {
    return this.http.put<Organization>('/api/organization', request);
  }
}
