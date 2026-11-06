import {
  activityStatus,
  barPosition,
  behindText,
  compactMoney,
  curveGeometry,
  curveSummary,
  datesText,
  linesText,
  slipText,
  timelineSpan,
  todayPosition,
} from './schedule-presentation';
import { ScheduleBudgetLine } from './schedule.models';
import {
  FOOTINGS_LINE,
  activityFixture,
  budgetLineFixture,
  curveFixture,
  scheduleFixture,
} from './testing/schedule-fixtures';

describe('activityStatus', () => {
  it('says the state in words with an icon, from the flags the server sent', () => {
    expect(activityStatus(activityFixture()).label).toBe('Done');
    expect(activityStatus(activityFixture({ percentCompleteBps: 4000, isBehind: true }))).toEqual({
      label: 'Behind',
      tone: 'danger',
      icon: 'warning',
    });
    expect(activityStatus(activityFixture({ percentCompleteBps: 0 })).label).toBe('On track');
    expect(
      activityStatus(activityFixture({ needsLines: true, percentCompleteBps: null })).label,
    ).toBe('No budget lines');
  });

  it('never calls an activity done at 99.99%', () => {
    expect(activityStatus(activityFixture({ percentCompleteBps: 9999 })).label).toBe('On track');
  });
});

describe('words for an activity', () => {
  it('writes the dates with the planned days, and a one-day activity once', () => {
    expect(datesText(activityFixture())).toBe('1 Sep 2026 to 30 Sep 2026 · 30 days');
    expect(
      datesText(
        activityFixture({
          plannedStart: '2026-09-01',
          plannedFinish: '2026-09-01',
          plannedDays: 1,
        }),
      ),
    ).toBe('1 Sep 2026 · 1 day');
  });

  it('says how the finish stands against the baseline', () => {
    expect(slipText(null)).toBe('');
    expect(slipText(0)).toBe('Finishes on its baseline date');
    expect(slipText(5)).toBe('Finishes 5 days later than the baseline');
    expect(slipText(-1)).toBe('Finishes 1 day earlier than the baseline');
  });

  it('counts the activities the server marked behind', () => {
    expect(behindText(scheduleFixture().activities)).toBe('1 of 3 activities is behind');
    expect(behindText([activityFixture()])).toBe('');
  });

  it('names the first linked line and how many more', () => {
    const lines = new Map<string, ScheduleBudgetLine>([[FOOTINGS_LINE, budgetLineFixture()]]);
    expect(linesText(activityFixture(), lines)).toBe('Footings');
    expect(
      linesText(
        activityFixture({ budgetLineIds: [FOOTINGS_LINE, FOOTINGS_LINE, FOOTINGS_LINE] }),
        lines,
      ),
    ).toBe('Footings and 2 more');
    expect(linesText(activityFixture({ budgetLineIds: [] }), lines)).toBe('No budget lines linked');
  });
});

describe('the timeline', () => {
  it('spans every planned and baseline date, and places bars by whole days', () => {
    const activities = [
      activityFixture({ plannedStart: '2026-09-01', plannedFinish: '2026-09-10' }),
      activityFixture({
        plannedStart: '2026-09-11',
        plannedFinish: '2026-09-20',
        baselineStart: '2026-09-11',
        baselineFinish: '2026-09-30',
      }),
    ];
    const span = timelineSpan(activities)!;
    expect(span.end - span.first).toBe(30);

    const first = barPosition('2026-09-01', '2026-09-10', span);
    expect(first.left).toBe(0);
    expect(first.width).toBeCloseTo(33.33, 1);
    expect(barPosition('2026-09-11', '2026-09-30', span).left).toBeCloseTo(33.33, 1);
    expect(todayPosition('2026-09-15', span)).toBe(50);
    expect(todayPosition('2027-01-01', span)).toBeNull();
    expect(timelineSpan([])).toBeNull();
  });
});

describe('the S-curve', () => {
  it('sums up the point for today in words, with the server figures', () => {
    expect(curveSummary(curveFixture(), 'PHP')).toBe(
      'By today (2 Nov 2026), ₱1,300,000.00 of work was planned, ₱1,240,000.00 was earned and ₱1,180,000.00 was spent.',
    );
  });

  it('mentions the baseline when there is one, and the start when today is before it', () => {
    const withBaseline = curveFixture({
      hasBaseline: true,
      points: curveFixture().points.map((point) => ({ ...point, baselinePlannedMinor: 100000000 })),
    });
    expect(curveSummary(withBaseline, 'PHP')).toContain(
      'The baseline planned ₱1,000,000.00 by then.',
    );

    const future = curveFixture({
      points: curveFixture().points.map((point) => ({
        ...point,
        isToday: false,
        earnedMinor: null,
        actualMinor: null,
      })),
    });
    expect(curveSummary(future, 'PHP')).toBe(
      'The schedule starts on 1 Sep 2026. Nothing is planned before then.',
    );
  });

  it('draws planned through the end, earned and actual only to today, and a line for today', () => {
    const chart = curveGeometry(curveFixture(), 'PHP', 640, 280)!;
    const planned = chart.series.find((series) => series.key === 'planned')!;
    const earned = chart.series.find((series) => series.key === 'earned')!;
    expect(planned.points.split(' ')).toHaveLength(5);
    expect(earned.points.split(' ')).toHaveLength(3);
    expect(chart.series.some((series) => series.key === 'baseline')).toBe(false);
    expect(chart.todayX).not.toBeNull();
    // Money goes up from zero to a round top: ₱2M here.
    expect(chart.yTicks.map((tick) => tick.label)).toEqual(['₱0', '₱1M', '₱2M']);
    expect(chart.xTicks[0]!.label).toBe('Sep 2026');
  });

  it('draws nothing from fewer than two points', () => {
    expect(curveGeometry(curveFixture({ points: [] }), 'PHP')).toBeNull();
  });

  it('writes axis money short', () => {
    expect(compactMoney(250000000, 'PHP')).toBe('₱2.5M');
    expect(compactMoney(75000000, 'PHP')).toBe('₱750K');
    expect(compactMoney(0, 'PHP')).toBe('₱0');
  });
});
