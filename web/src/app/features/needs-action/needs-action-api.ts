import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { NeedsActionCount, NeedsActionResult } from './needs-action.models';

/** The "Needs your action" endpoints, one method each. Both are reads: nothing is approved or issued from here. */
@Injectable({ providedIn: 'root' })
export class NeedsActionApi {
  private readonly http = inject(HttpClient);

  /** The person's groups with their most urgent rows. */
  get(): Observable<NeedsActionResult> {
    return this.http.get<NeedsActionResult>('/api/needs-action');
  }

  /** Only the number waiting, for the navigation badge (counts only, cheaper than the full read). */
  count(): Observable<NeedsActionCount> {
    return this.http.get<NeedsActionCount>('/api/needs-action/count');
  }
}
