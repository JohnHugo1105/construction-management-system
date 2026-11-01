import { EarnedValue, Progress, ProgressDivision, ProgressLine } from '../progress.models';

// Plain data for the specs, shaped as the API returns it (a miniature Riverside Residences). No test framework is used here.

export const FOOTINGS_ID = 'a1111111-0000-4000-8000-000000000001';
export const COLUMNS_ID = 'a1111111-0000-4000-8000-000000000002';
export const OFFICE_ID = 'a1111111-0000-4000-8000-000000000003';

export function lineFixture(overrides: Partial<ProgressLine> = {}): ProgressLine {
  return {
    budgetLineId: FOOTINGS_ID,
    description: 'Footings',
    wbsCode: '3100',
    budgetMinor: 100000000,
    percentBps: 5000,
    lastUpdatedOn: '2026-10-20',
    earnedMinor: 50000000,
    ...overrides,
  };
}

export function divisionFixture(overrides: Partial<ProgressDivision> = {}): ProgressDivision {
  return {
    division: 3,
    name: 'Concrete',
    budgetMinor: 150000000,
    earnedMinor: 60000000,
    percentBps: 4000,
    lines: [
      lineFixture(),
      lineFixture({
        budgetLineId: COLUMNS_ID,
        description: 'Columns',
        wbsCode: '3300',
        budgetMinor: 50000000,
        percentBps: 2000,
        earnedMinor: 10000000,
      }),
    ],
    ...overrides,
  };
}

export function earnedValueFixture(overrides: Partial<EarnedValue> = {}): EarnedValue {
  return {
    budgetMinor: 170000000,
    earnedMinor: 80000000,
    spentMinor: 40000000,
    plannedMinor: 68000000,
    costVarianceMinor: 40000000,
    scheduleVarianceMinor: 12000000,
    cpiBps: 20000,
    spiBps: 11765,
    forecastMinor: 85000000,
    toCompleteMinor: 45000000,
    forecastVarianceMinor: 85000000,
    percentCompleteBps: 4706,
    plannedPercentBps: 4000,
    contractPriceMinor: 300000000,
    forecastMarginMinor: 215000000,
    ...overrides,
  };
}

export function progressFixture(overrides: Partial<Progress> = {}): Progress {
  return {
    projectId: '7dbf3734-a33b-481c-a877-fb4c4e002bce',
    asOfDate: '2026-11-02',
    basis: 'IncludingVat',
    hasProgress: true,
    lastUpdatedOn: '2026-10-20',
    earnedValue: earnedValueFixture(),
    divisions: [
      divisionFixture(),
      {
        division: null,
        name: 'Unclassified',
        budgetMinor: 20000000,
        earnedMinor: 20000000,
        percentBps: 10000,
        lines: [
          lineFixture({
            budgetLineId: OFFICE_ID,
            description: 'Site office',
            wbsCode: null,
            budgetMinor: 20000000,
            percentBps: 10000,
            earnedMinor: 20000000,
          }),
        ],
      },
    ],
    ...overrides,
  };
}

/** A project with progress on no line yet. */
export function untouchedFixture(): Progress {
  const clear = (line: ProgressLine): ProgressLine => ({
    ...line,
    percentBps: 0,
    lastUpdatedOn: null,
    earnedMinor: 0,
  });
  const base = progressFixture();
  return {
    ...base,
    hasProgress: false,
    lastUpdatedOn: null,
    earnedValue: earnedValueFixture({
      earnedMinor: 0,
      cpiBps: 0,
      spiBps: 0,
      percentCompleteBps: 0,
      forecastMinor: 170000000,
      forecastVarianceMinor: 0,
    }),
    divisions: base.divisions.map((division) => ({
      ...division,
      earnedMinor: 0,
      percentBps: 0,
      lines: division.lines.map(clear),
    })),
  };
}

/** One division holding this many lines, for the cases about long lists. */
export function manyLinesFixture(count: number): Progress {
  const lines = Array.from({ length: count }, (_, index) =>
    lineFixture({
      budgetLineId: `b0000000-0000-4000-8000-${String(index).padStart(12, '0')}`,
      description: `Line ${index + 1}`,
      percentBps: 1000,
    }),
  );
  return progressFixture({
    divisions: [divisionFixture({ lines, name: 'Concrete' })],
  });
}
