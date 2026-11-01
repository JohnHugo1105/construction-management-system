import { Signal, computed, inject } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { ApiError, asApiError } from '../../core/api/api-error';
import { WorkforceApi } from './workforce-api';
import { ProjectChoice } from './workforce.models';

export interface ProjectChoices {
  /** The projects that can still receive records, with their active sub-projects. */
  readonly projects: Signal<readonly ProjectChoice[]>;
  readonly loading: Signal<boolean>;
  readonly error: Signal<ApiError | null>;
  reload(): void;
}

/** Loads the project picker rows once for the component that calls it. Create it in a field initializer. */
export function injectProjectChoices(): ProjectChoices {
  const api = inject(WorkforceApi);
  const resource = rxResource({ stream: () => api.projectChoices() });
  return {
    projects: computed(() =>
      (resource.hasValue() ? resource.value() : []).map((project) => ({
        ...project,
        subProjects: project.subProjects.filter((sub) => sub.status === 'Active'),
      })),
    ),
    loading: computed(() => resource.isLoading()),
    error: computed(() => {
      const failure = resource.error();
      return failure ? asApiError(failure) : null;
    }),
    reload: () => resource.reload(),
  };
}
