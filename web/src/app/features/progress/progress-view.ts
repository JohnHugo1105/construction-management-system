import { Signal, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute } from '@angular/router';
import { map } from 'rxjs';
import { Permission } from '../../core/session/permissions';
import { SessionState } from '../../core/session/session-state';

/** The Progress tab's two views: recording progress by line, or the project's schedule and S-curve. */
export type ProgressView = 'progress' | 'schedule';

/**
 * Which view of the Progress tab is showing. The router owns it (`?view=schedule`), so the view can be shared and survives a
 * reload; someone who may not read the schedule always gets the progress view. Call it inside the tab (or what the tab provides).
 */
export function injectProgressView(): Signal<ProgressView> {
  const route = inject(ActivatedRoute);
  const mayReadSchedule = inject(SessionState).permit(Permission.scheduleRead);
  const asked = toSignal(route.queryParamMap.pipe(map((params) => params.get('view'))), {
    initialValue: route.snapshot.queryParamMap.get('view'),
  });
  return computed(() => (asked() === 'schedule' && mayReadSchedule() ? 'schedule' : 'progress'));
}
