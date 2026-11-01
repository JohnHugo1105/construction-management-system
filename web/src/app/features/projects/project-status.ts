import { ChipTone } from '../../shared/ui/status-chip/status-chip';
import { IconName } from '../../shared/ui/icon/icon';
import { ProjectStatus } from './projects.models';

export interface StatusPresentation {
  readonly label: string;
  readonly tone: ChipTone;
  readonly icon: IconName;
}

/** How each project state looks, in one place: the list and the detail page always describe a state the same way. */
export const PROJECT_STATUS: Record<ProjectStatus, StatusPresentation> = {
  Planning: { label: 'Planning', tone: 'neutral', icon: 'calendar' },
  Active: { label: 'Active', tone: 'success', icon: 'play' },
  OnHold: { label: 'On hold', tone: 'warning', icon: 'pause' },
  Completed: { label: 'Completed', tone: 'info', icon: 'check-circle' },
  Archived: { label: 'Archived', tone: 'neutral', icon: 'archive' },
};

/** The status filter chips on the list, in order. "All" means every project except archived ones. */
export const PROJECT_FILTERS = [
  'All',
  'Planning',
  'Active',
  'OnHold',
  'Completed',
  'Archived',
] as const;
export type ProjectFilter = (typeof PROJECT_FILTERS)[number];

export function filterLabel(filter: ProjectFilter): string {
  return filter === 'All' ? 'All' : PROJECT_STATUS[filter].label;
}

/** The changes a person can make to a project's state. */
export type LifecycleAction =
  'start' | 'hold' | 'resume' | 'complete' | 'reopen' | 'archive' | 'restore';

export const LIFECYCLE_LABELS: Record<LifecycleAction, string> = {
  start: 'Start project',
  hold: 'Put on hold',
  resume: 'Resume project',
  complete: 'Mark completed',
  reopen: 'Reopen project',
  archive: 'Archive project',
  restore: 'Restore project',
};

export interface LifecyclePlan {
  /** The next sensible step, shown as the page's one primary action. Null when nothing can be done. */
  readonly primary: LifecycleAction | null;
  /** Everything else that is allowed, folded into the "More" menu. */
  readonly more: readonly LifecycleAction[];
}

/** What can be done from each state. The API enforces the same rules; this only decides what to offer, and where. */
export function lifecyclePlan(status: ProjectStatus): LifecyclePlan {
  switch (status) {
    case 'Planning':
      return { primary: 'start', more: ['archive'] };
    case 'Active':
      return { primary: 'complete', more: ['hold', 'archive'] };
    case 'OnHold':
      return { primary: 'resume', more: ['complete', 'archive'] };
    case 'Completed':
      return { primary: 'archive', more: ['reopen'] };
    case 'Archived':
      return { primary: 'restore', more: [] };
  }
}

/** The message shown after an action succeeds. */
export const LIFECYCLE_DONE: Record<LifecycleAction, string> = {
  start: 'Project started.',
  hold: 'Project put on hold.',
  resume: 'Project resumed.',
  complete: 'Project marked completed.',
  reopen: 'Project reopened.',
  archive: 'Project archived.',
  restore: 'Project restored.',
};
