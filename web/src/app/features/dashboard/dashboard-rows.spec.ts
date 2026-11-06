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

/** A project whose work done says it will finish `over` centavos above its budget (negative: under it). */
function forecast(over: number): Pick<DashboardRow, 'progressForecast'> {
  return {
    progressForecast: {
      estimateAtCompletionMinor: 100_000 + over,
      overBudgetMinor: over,
      percentCompleteBps: 3_000,
    },
  };
}

describe('sortDashboardRows with a forecast', () => {
  it('puts projects forecast over budget right after the over budget ones, biggest overrun first', () => {
    const sorted = sortDashboardRows([
      row('Annex', 'OnTrack'),
      row('Pier', 'Unbudgeted'),
      row('Riverside', 'NearLimit', forecast(500)),
      row('Delta', 'OnTrack', forecast(9_000)),
      row('Tower', 'OverBudget'),
      row('Clinic', 'NearLimit'),
      row('Bridge', 'OverBudget', forecast(70_000)),
      row('Echo', 'OnTrack', forecast(500)),
    ]);

    expect(sorted.map((item) => item.name)).toEqual([
      'Bridge',
      'Tower',
      'Delta',
      'Echo',
      'Riverside',
      'Pier',
      'Clinic',
      'Annex',
    ]);
  });

  it('keeps an over budget project in the over budget group whatever its forecast', () => {
    const sorted = sortDashboardRows([
      row('Zeta', 'OnTrack', forecast(9_000_000)),
      row('Alpha', 'OverBudget', forecast(1)),
      row('Beta', 'OverBudget'),
    ]);

    expect(sorted.map((item) => item.name)).toEqual(['Alpha', 'Beta', 'Zeta']);
  });

  it('does not move a project whose forecast is within its budget or is missing', () => {
    const sorted = sortDashboardRows([
      row('Delta', 'OnTrack', forecast(0)),
      row('Echo', 'OnTrack', forecast(-4_000)),
      row('Annex', 'OnTrack', { progressForecast: null }),
      row('Clinic', 'NearLimit'),
    ]);

    expect(sorted.map((item) => item.name)).toEqual(['Clinic', 'Annex', 'Delta', 'Echo']);
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

  it('turns amber when nothing is over budget but projects are forecast to finish over it', () => {
    const fact = leadFact([row('A', 'OnTrack', forecast(900)), row('B', 'OnTrack')], 1);

    expect(fact).toMatchObject({
      headline: '1 project forecast to finish over budget',
      detail:
        "Spending is running ahead of the work done. Open the project's Progress tab to see why.",
      tone: 'warning',
      icon: 'warning',
    });
    expect(leadFact([row('A', 'OnTrack'), row('B', 'OnTrack')], 3).headline).toBe(
      '3 projects forecast to finish over budget',
    );
  });

  it('keeps the over budget headline and adds the forecast count to its second line', () => {
    const fact = leadFact([row('A', 'OverBudget'), row('B', 'OnTrack', forecast(900))], 2);

    expect(fact.headline).toBe('1 project needs attention');
    expect(fact.detail).toBe('1 over budget, 2 forecast to finish over budget');
    expect(fact.tone).toBe('warning');
  });

  it('stays green when nothing is over and nothing is forecast over, with no forecast words at all', () => {
    for (const count of [0, undefined]) {
      const fact = leadFact([row('A', 'OnTrack', forecast(-1_000))], count);
      expect(fact.headline).toBe('No project is over its total budget');
      expect(fact.tone).toBe('success');
      expect(`${fact.headline} ${fact.detail}`).not.toContain('forecast');
    }
  });

  it('does not claim anything is on track when nothing has been budgeted or spent', () => {
    expect(leadFact([row('A', 'NoActivity')])).toMatchObject({
      headline: 'Nothing to track yet',
      tone: 'neutral',
    });
  });
});
