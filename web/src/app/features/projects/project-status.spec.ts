import {
  LIFECYCLE_LABELS,
  LifecycleAction,
  PROJECT_FILTERS,
  PROJECT_STATUS,
  filterLabel,
  lifecyclePlan,
} from './project-status';
import { ProjectStatus } from './projects.models';

const allActions = (status: ProjectStatus): LifecycleAction[] => {
  const plan = lifecyclePlan(status);
  return plan.primary ? [plan.primary, ...plan.more] : [...plan.more];
};

describe('lifecyclePlan', () => {
  it('leads with the next sensible step for each state', () => {
    expect(lifecyclePlan('Planning').primary).toBe('start');
    expect(lifecyclePlan('Active').primary).toBe('complete');
    expect(lifecyclePlan('OnHold').primary).toBe('resume');
    expect(lifecyclePlan('Completed').primary).toBe('archive');
    expect(lifecyclePlan('Archived').primary).toBe('restore');
  });

  it('offers exactly the moves the API allows, and nothing else', () => {
    expect(allActions('Planning').sort()).toEqual(['archive', 'start']);
    expect(allActions('Active').sort()).toEqual(['archive', 'complete', 'hold']);
    expect(allActions('OnHold').sort()).toEqual(['archive', 'complete', 'resume']);
    expect(allActions('Completed').sort()).toEqual(['archive', 'reopen']);
    expect(allActions('Archived')).toEqual(['restore']);
  });

  it('folds everything except the one next step into "More", and shows no More when there is nothing to fold', () => {
    expect(lifecyclePlan('Active').more).toEqual(['hold', 'archive']);
    expect(lifecyclePlan('Archived').more).toEqual([]);
  });

  it('never offers the same action twice', () => {
    for (const status of ['Planning', 'Active', 'OnHold', 'Completed', 'Archived'] as const) {
      const actions = allActions(status);
      expect(new Set(actions).size).toBe(actions.length);
    }
  });

  it('names each action by its result', () => {
    expect(LIFECYCLE_LABELS).toEqual({
      start: 'Start project',
      hold: 'Put on hold',
      resume: 'Resume project',
      complete: 'Mark completed',
      reopen: 'Reopen project',
      archive: 'Archive project',
      restore: 'Restore project',
    });
  });
});

describe('project states', () => {
  it('gives every state words, an icon and a tone, so none relies on colour', () => {
    for (const presentation of Object.values(PROJECT_STATUS)) {
      expect(presentation.label.length).toBeGreaterThan(0);
      expect(presentation.icon).toBeTruthy();
    }
    expect(PROJECT_STATUS.OnHold.label).toBe('On hold');
  });

  it('offers a filter chip for each state plus "All"', () => {
    expect(PROJECT_FILTERS.map(filterLabel)).toEqual([
      'All',
      'Planning',
      'Active',
      'On hold',
      'Completed',
      'Archived',
    ]);
  });
});
