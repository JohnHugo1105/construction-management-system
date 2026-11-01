import { leadFact, sortDashboardRows } from './dashboard-rows';
import { DashboardRow } from './dashboard.models';

function row(
  name: string,
  health: DashboardRow['health'],
  overrides: Partial<DashboardRow> = {},
): DashboardRow {
  return {
    id: name.toLowerCase().replaceAll(' ', '-'),
    name,
    status: 'Active',
    contractPriceMinor: null,
    budgetMinor: 100,
    actualMinor: 50,
    remainingMinor: 50,
    usedBps: 5000,
    health,
    marginAfterActualMinor: null,
    budgetMarginMinor: null,
    ...overrides,
  };
}

describe('sortDashboardRows', () => {
  it('lists over budget first, then unbudgeted, near limit, on track and no activity, each by name', () => {
    const sorted = sortDashboardRows([
      row('Harbor Warehouse', 'NoActivity'),
      row('Delta', 'OnTrack'),
      row('Riverside', 'NearLimit'),
      row('Annex', 'OnTrack'),
      row('Pier', 'Unbudgeted'),
      row('Tower', 'OverBudget'),
      row('Bridge', 'OverBudget'),
    ]);

    expect(sorted.map((item) => item.name)).toEqual([
      'Bridge',
      'Tower',
      'Pier',
      'Riverside',
      'Annex',
      'Delta',
      'Harbor Warehouse',
    ]);
  });
});

describe('leadFact', () => {
  it('leads with how many projects need attention, and what kind', () => {
    const fact = leadFact([
      row('A', 'OverBudget'),
      row('B', 'NearLimit'),
      row('C', 'OnTrack'),
      row('D', 'NoActivity'),
    ]);

    expect(fact.headline).toBe('2 projects need attention');
    expect(fact.detail).toBe('1 over budget, 1 near the limit');
    expect(fact.tone).toBe('warning');
  });

  it('says "1 project needs attention" in the singular', () => {
    expect(leadFact([row('A', 'Unbudgeted'), row('B', 'OnTrack')])).toMatchObject({
      headline: '1 project needs attention',
      detail: '1 unbudgeted',
    });
  });

  it('says plainly that no project is over its total budget when none needs attention, and points to the breakdown', () => {
    const fact = leadFact([row('A', 'OnTrack'), row('B', 'NoActivity')]);

    expect(fact.headline).toBe('No project is over its total budget');
    expect(fact.detail).toContain('whole budget');
    expect(fact.tone).toBe('success');
  });

  it('does not claim anything is on track when nothing has been budgeted or spent', () => {
    expect(leadFact([row('A', 'NoActivity')])).toMatchObject({
      headline: 'Nothing to track yet',
      tone: 'neutral',
    });
  });
});
