import {
  WORKER_FILTERS,
  WORKER_STATUS,
  filterLabel,
  hasLeft,
  nextStatusChoices,
  statusNeedsReason,
} from './worker-status';
import { WorkerStatus } from './workforce.models';

const offered = (status: WorkerStatus) => nextStatusChoices(status).map((choice) => choice.status);

describe('worker status rules', () => {
  it('offers every other state to a worker who is still with the company, and never the state they are in', () => {
    expect(offered('Active')).toEqual([
      'OnLeave',
      'Suspended',
      'Resigned',
      'Retired',
      'Terminated',
    ]);
    expect(offered('OnLeave')).toEqual([
      'Active',
      'Suspended',
      'Resigned',
      'Retired',
      'Terminated',
    ]);
    expect(offered('Suspended')).toEqual([
      'Active',
      'OnLeave',
      'Resigned',
      'Retired',
      'Terminated',
    ]);
  });

  it('offers nothing to a worker who has left: the only way back is reinstating', () => {
    for (const status of ['Resigned', 'Retired', 'Terminated'] as const) {
      expect(hasLeft(status)).toBe(true);
      expect(offered(status)).toEqual([]);
    }
    for (const status of ['Active', 'OnLeave', 'Suspended'] as const)
      expect(hasLeft(status)).toBe(false);
  });

  it('needs a reason for Suspended and Terminated only', () => {
    expect(statusNeedsReason('Suspended')).toBe(true);
    expect(statusNeedsReason('Terminated')).toBe(true);
    for (const status of ['Active', 'OnLeave', 'Resigned', 'Retired'] as const)
      expect(statusNeedsReason(status)).toBe(false);
  });

  it('says in the choice itself that leaving ends the posting and that a reason is wanted', () => {
    const helpFor = (status: WorkerStatus) =>
      nextStatusChoices('Active').find((choice) => choice.status === status)!.help;
    expect(helpFor('Resigned')).toContain('ends their project posting');
    expect(helpFor('Terminated')).toContain('Say why');
    expect(helpFor('Suspended')).toContain('Say why');
  });

  it('words every state the same way on the filter chips and the status chip', () => {
    expect(WORKER_FILTERS).toEqual([
      'All',
      'Active',
      'OnLeave',
      'Suspended',
      'Resigned',
      'Retired',
      'Terminated',
    ]);
    expect(WORKER_FILTERS.map(filterLabel)).toEqual([
      'All',
      'Active',
      'On leave',
      'Suspended',
      'Resigned',
      'Retired',
      'Terminated',
    ]);
    expect(WORKER_STATUS.OnLeave.label).toBe('On leave');
  });
});
