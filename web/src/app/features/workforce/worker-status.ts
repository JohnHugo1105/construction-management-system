import { IconName } from '../../shared/ui/icon/icon';
import { ChipTone } from '../../shared/ui/status-chip/status-chip';
import { RateCategory, RateUnit, WorkerStatus } from './workforce.models';

export interface StatusPresentation {
  readonly label: string;
  readonly tone: ChipTone;
  readonly icon: IconName;
}

/** How each worker state looks, in one place: the list, the detail page and the dialogs always describe a state the same way. */
export const WORKER_STATUS: Record<WorkerStatus, StatusPresentation> = {
  Active: { label: 'Active', tone: 'success', icon: 'check-circle' },
  OnLeave: { label: 'On leave', tone: 'info', icon: 'calendar' },
  Suspended: { label: 'Suspended', tone: 'warning', icon: 'pause' },
  Resigned: { label: 'Resigned', tone: 'neutral', icon: 'logout' },
  Retired: { label: 'Retired', tone: 'neutral', icon: 'logout' },
  Terminated: { label: 'Terminated', tone: 'danger', icon: 'minus-circle' },
};

/** The status chips above the list, in order. "All" means every status. */
export const WORKER_FILTERS = [
  'All',
  'Active',
  'OnLeave',
  'Suspended',
  'Resigned',
  'Retired',
  'Terminated',
] as const;
export type WorkerFilter = (typeof WORKER_FILTERS)[number];

export function filterLabel(filter: WorkerFilter): string {
  return filter === 'All' ? 'All' : WORKER_STATUS[filter].label;
}

/** States that mean the person has left. Only reinstating brings them back. */
export const LEFT_STATUSES: readonly WorkerStatus[] = ['Resigned', 'Retired', 'Terminated'];

export function hasLeft(status: WorkerStatus): boolean {
  return LEFT_STATUSES.includes(status);
}

export interface StatusChoice {
  readonly status: WorkerStatus;
  /** One plain line saying what the state means, shown beside the choice. */
  readonly help: string;
  /** The API refuses this state without a reason. */
  readonly needsReason: boolean;
}

const CHOICES: Record<WorkerStatus, StatusChoice> = {
  Active: {
    status: 'Active',
    help: 'Back at work and can be posted to a project.',
    needsReason: false,
  },
  OnLeave: {
    status: 'OnLeave',
    help: 'Away for a while. Attendance can be recorded only for days before the leave began.',
    needsReason: false,
  },
  Suspended: {
    status: 'Suspended',
    help: 'Not allowed to work for now. Say why.',
    needsReason: true,
  },
  Resigned: {
    status: 'Resigned',
    help: 'Chose to leave. This also ends their project posting.',
    needsReason: false,
  },
  Retired: {
    status: 'Retired',
    help: 'Reached retirement. This also ends their project posting.',
    needsReason: false,
  },
  Terminated: {
    status: 'Terminated',
    help: 'Dismissed by the company. Say why. This also ends their project posting.',
    needsReason: true,
  },
};

/**
 * The states a worker can be moved to from where they are, in the order they are offered. A worker who has left can only be
 * reinstated (a separate action), so nothing is offered here. The API enforces the same rules; this decides what to offer.
 */
export function nextStatusChoices(current: WorkerStatus): readonly StatusChoice[] {
  if (hasLeft(current)) return [];
  const order: readonly WorkerStatus[] = [
    'Active',
    'OnLeave',
    'Suspended',
    'Resigned',
    'Retired',
    'Terminated',
  ];
  return order.filter((status) => status !== current).map((status) => CHOICES[status]);
}

/** Whether the API will want a reason for moving to this state. */
export function statusNeedsReason(status: WorkerStatus): boolean {
  return CHOICES[status].needsReason;
}

export const RATE_UNIT_LABEL: Record<RateUnit, string> = {
  Daily: 'per day',
  Hourly: 'per hour',
};

export interface RateCategoryChoice {
  readonly value: Exclude<RateCategory, 'Initial'>;
  readonly label: string;
  /** What the choice means, and what the designation must do. */
  readonly help: string;
  /** A promotion or demotion changes the designation; a rate adjustment keeps it. */
  readonly changesDesignation: boolean;
}

export const RATE_CATEGORIES: readonly RateCategoryChoice[] = [
  {
    value: 'Promotion',
    label: 'Promotion',
    help: 'A move up to a new designation, usually with a higher rate.',
    changesDesignation: true,
  },
  {
    value: 'Demotion',
    label: 'Demotion',
    help: 'A move down to a new designation, usually with a lower rate.',
    changesDesignation: true,
  },
  {
    value: 'RateAdjustment',
    label: 'Rate adjustment',
    help: 'A new rate for the same designation, such as a yearly raise.',
    changesDesignation: false,
  },
];

export const RATE_CATEGORY_LABEL: Record<RateCategory, string> = {
  Initial: 'Hired',
  Promotion: 'Promotion',
  Demotion: 'Demotion',
  RateAdjustment: 'Rate adjustment',
};
