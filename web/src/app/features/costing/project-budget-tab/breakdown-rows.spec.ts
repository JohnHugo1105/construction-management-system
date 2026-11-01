import { SUB_PROJECT_ID, summaryFixture } from '../testing/costing-fixtures';
import { availableViews, breakdownRows, startsOpen } from './breakdown-rows';

describe('breakdownRows', () => {
  it('lists the divisions in the order the API sent them, each with its codes', () => {
    const rows = breakdownRows(summaryFixture(), 'division');

    expect(rows.map((row) => row.name)).toEqual(['Mechanical', 'Concrete', 'Finishes']);
    expect(rows[1]!.children.map((child) => child.name)).toEqual([
      'Cast-In-Place Concrete',
      'Concrete Reinforcement',
    ]);
    expect(rows[1]!.children[0]!.note).toBe('Code 3300');
  });

  it('says in plain words where money was spent with no budget', () => {
    const rows = breakdownRows(summaryFixture(), 'division');

    expect(rows[0]!.children[0]!.note).toBe('Code 15200 · No budget was set for this.');
    expect(rows[0]!.children[0]!.figures.health).toBe('Unbudgeted');
  });

  it('flags a division that holds something over budget, unbudgeted or near its limit, even when its own health is fine', () => {
    const rows = breakdownRows(summaryFixture(), 'division');

    expect(rows[1]!.figures.health).toBe('OnTrack');
    expect(rows[1]!.attention).toBe('1 over budget, 1 near the limit');
    expect(rows[0]!.attention).toBe('1 unbudgeted');
    expect(rows[2]!.attention).toBe('');
  });

  it('opens only the divisions that need a look', () => {
    const rows = breakdownRows(summaryFixture(), 'division');

    expect(rows.map(startsOpen)).toEqual([true, true, false]);
  });

  it('labels lines with no division as unclassified, in plain words', () => {
    const base = summaryFixture();
    const rows = breakdownRows(
      summaryFixture({
        byDivision: [
          ...base.byDivision,
          {
            division: null,
            name: 'Unclassified',
            budgetMinor: 0,
            actualMinor: 5000,
            remainingMinor: -5000,
            usedBps: null,
            health: 'Unbudgeted',
            wbs: [],
          },
        ],
      }),
      'division',
    );

    const unclassified = rows.at(-1)!;
    expect(unclassified.name).toBe('Unclassified');
    expect(unclassified.note).toBe('These lines have no division yet.');
    expect(startsOpen(unclassified)).toBe(false);
  });

  it('gives the sub-project and type views with their own names', () => {
    const summary = summaryFixture();
    expect(breakdownRows(summary, 'itemType').map((row) => row.name)).toEqual([
      'Equipment',
      'Material',
    ]);
    expect(breakdownRows(summary, 'itemType')[0]!.note).toBe('No budget was set for this.');
    expect(breakdownRows(summary, 'subProject').map((row) => row.name)).toEqual(['Whole project']);
  });
});

describe('availableViews', () => {
  it('offers sub-projects only when the project has a real one with money in it', () => {
    expect(availableViews(summaryFixture()).map((view) => view.value)).toEqual([
      'division',
      'itemType',
    ]);

    const split = summaryFixture({
      bySubProject: [
        {
          subProjectId: SUB_PROJECT_ID,
          name: 'Tower A',
          budgetMinor: 100,
          actualMinor: 0,
          remainingMinor: 100,
          usedBps: 0,
          health: 'OnTrack',
        },
      ],
    });
    expect(availableViews(split).map((view) => view.label)).toEqual([
      'By division',
      'By sub-project',
      'By type',
    ]);
  });

  it('offers no type view when there is nothing to split', () => {
    expect(availableViews(summaryFixture({ byItemType: [] })).map((view) => view.value)).toEqual([
      'division',
    ]);
  });
});
