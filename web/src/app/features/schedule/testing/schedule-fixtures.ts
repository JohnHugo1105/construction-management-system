import { Schedule, ScheduleActivity, ScheduleBudgetLine, ScheduleCurve } from '../schedule.models';

// Plain data for the specs, shaped as the API returns it (a miniature Riverside Residences schedule on 2 Nov 2026). No test
// framework is used here.

export const FOUNDATIONS_ID = 'c1111111-0000-4000-8000-000000000001';
export const MASONRY_ID = 'c1111111-0000-4000-8000-000000000002';
export const TILES_ID = 'c1111111-0000-4000-8000-000000000003';

export const FOOTINGS_LINE = 'd1111111-0000-4000-8000-000000000001';
export const WALLS_LINE = 'd1111111-0000-4000-8000-000000000002';
export const TILES_LINE = 'd1111111-0000-4000-8000-000000000003';
export const OFFICE_LINE = 'd1111111-0000-4000-8000-000000000004';

export function activityFixture(overrides: Partial<ScheduleActivity> = {}): ScheduleActivity {
  return {
    id: FOUNDATIONS_ID,
    name: 'Foundations',
    plannedStart: '2026-09-01',
    plannedFinish: '2026-09-30',
    plannedDays: 30,
    baselineStart: null,
    baselineFinish: null,
    slipDays: null,
    sortOrder: 0,
    weightMinor: 100000000,
    percentCompleteBps: 10000,
    isBehind: false,
    needsLines: false,
    budgetLineIds: [FOOTINGS_LINE],
    version: 3,
    ...overrides,
  };
}

export function budgetLineFixture(overrides: Partial<ScheduleBudgetLine> = {}): ScheduleBudgetLine {
  return {
    id: FOOTINGS_LINE,
    description: 'Footings',
    wbsCode: '3100',
    division: 3,
    divisionName: 'Concrete',
    budgetMinor: 100000000,
    percentBps: 10000,
    activityId: FOUNDATIONS_ID,
    ...overrides,
  };
}

export function scheduleFixture(overrides: Partial<Schedule> = {}): Schedule {
  return {
    projectId: '7dbf3734-a33b-481c-a877-fb4c4e002bce',
    today: '2026-11-02',
    basis: 'IncludingVat',
    projectArchived: false,
    usesSchedule: true,
    plannedMinor: 130000000,
    baselinePlannedMinor: null,
    planWeightMinor: 200000000,
    budgetMinor: 220000000,
    unplannedBudgetMinor: 20000000,
    unplannedLineCount: 1,
    baselineSetAtUtc: null,
    activities: [
      activityFixture(),
      activityFixture({
        id: MASONRY_ID,
        name: 'Masonry walls',
        plannedStart: '2026-10-01',
        plannedFinish: '2026-10-28',
        plannedDays: 28,
        sortOrder: 1,
        weightMinor: 60000000,
        percentCompleteBps: 4000,
        isBehind: true,
        budgetLineIds: [WALLS_LINE],
        version: 2,
      }),
      activityFixture({
        id: TILES_ID,
        name: 'Floor tiles',
        plannedStart: '2026-11-15',
        plannedFinish: '2026-12-15',
        plannedDays: 31,
        sortOrder: 2,
        weightMinor: 40000000,
        percentCompleteBps: 0,
        budgetLineIds: [TILES_LINE],
        version: 1,
      }),
    ],
    budgetLines: [
      budgetLineFixture(),
      budgetLineFixture({
        id: WALLS_LINE,
        description: 'CHB walls',
        wbsCode: '4200',
        division: 4,
        divisionName: 'Masonry',
        budgetMinor: 60000000,
        percentBps: 4000,
        activityId: MASONRY_ID,
      }),
      budgetLineFixture({
        id: TILES_LINE,
        description: 'Floor tiles',
        wbsCode: '9300',
        division: 9,
        divisionName: 'Finishes',
        budgetMinor: 40000000,
        percentBps: 0,
        activityId: TILES_ID,
      }),
      budgetLineFixture({
        id: OFFICE_LINE,
        description: 'Site office',
        wbsCode: null,
        division: null,
        divisionName: 'Unclassified',
        budgetMinor: 20000000,
        percentBps: 10000,
        activityId: null,
      }),
    ],
    ...overrides,
  };
}

export function curveFixture(overrides: Partial<ScheduleCurve> = {}): ScheduleCurve {
  return {
    projectId: '7dbf3734-a33b-481c-a877-fb4c4e002bce',
    today: '2026-11-02',
    basis: 'IncludingVat',
    hasBaseline: false,
    stepDays: 7,
    points: [
      {
        date: '2026-09-01',
        isToday: false,
        plannedMinor: 3333333,
        baselinePlannedMinor: null,
        earnedMinor: 0,
        actualMinor: 0,
      },
      {
        date: '2026-10-06',
        isToday: false,
        plannedMinor: 108571429,
        baselinePlannedMinor: null,
        earnedMinor: 95000000,
        actualMinor: 90000000,
      },
      {
        date: '2026-11-02',
        isToday: true,
        plannedMinor: 130000000,
        baselinePlannedMinor: null,
        earnedMinor: 124000000,
        actualMinor: 118000000,
      },
      {
        date: '2026-11-03',
        isToday: false,
        plannedMinor: 160000000,
        baselinePlannedMinor: null,
        earnedMinor: null,
        actualMinor: null,
      },
      {
        date: '2026-12-15',
        isToday: false,
        plannedMinor: 200000000,
        baselinePlannedMinor: null,
        earnedMinor: null,
        actualMinor: null,
      },
    ],
    ...overrides,
  };
}
