import {
  completeHeadline,
  costIndexSentence,
  forecastMargin,
  forecastSentence,
  scheduleSentence,
  spentForWorth,
  wholePercent,
} from './progress-presentation';
import { earnedValueFixture } from './testing/progress-fixtures';

describe('wholePercent and completeHeadline', () => {
  it('rounds basis points to a whole percent', () => {
    expect(wholePercent(4706)).toBe('47%');
    expect(wholePercent(10000)).toBe('100%');
    expect(wholePercent(49)).toBe('0%');
    expect(wholePercent(null)).toBe('');
  });

  it('says nothing when there is no budget to measure against', () => {
    expect(completeHeadline(earnedValueFixture())).toBe('47% complete');
    expect(completeHeadline(earnedValueFixture({ percentCompleteBps: null }))).toBe('');
  });
});

describe('spentForWorth', () => {
  it('states what was spent for what the work is worth, with a warning when it cost more', () => {
    const good = spentForWorth(earnedValueFixture({ spentMinor: 40000000, earnedMinor: 80000000 }), 'PHP');
    expect(good).toEqual({ text: 'Spent ₱400,000.00 for work worth ₱800,000.00', tone: 'good' });

    const bad = spentForWorth(earnedValueFixture({ spentMinor: 45000000, earnedMinor: 40000000 }), 'PHP');
    expect(bad.tone).toBe('warning');
  });
});

describe('costIndexSentence', () => {
  it('says how many centavos each peso spent is earning', () => {
    expect(costIndexSentence(9300, 1)).toEqual({
      text: 'Each peso spent is earning 93 centavos of work.',
      tone: 'warning',
    });
    expect(costIndexSentence(100, 1)?.text).toBe('Each peso spent is earning 1 centavo of work.');
  });

  it('counts in pesos once each peso earns more than a peso', () => {
    expect(costIndexSentence(12500, 1)).toEqual({ text: 'Each peso spent is earning ₱1.25 of work.', tone: 'good' });
    expect(costIndexSentence(10000, 1)?.tone).toBe('good');
  });

  it('says so when money is spent and no work is recorded, and says nothing when nothing is spent', () => {
    expect(costIndexSentence(0, 500)?.text).toBe('Money has been spent, but no work is recorded against it yet.');
    expect(costIndexSentence(null, 0)).toBeNull();
  });
});

describe('scheduleSentence', () => {
  it('says ahead, on or behind the plan for today', () => {
    expect(scheduleSentence(13333)).toEqual({
      text: 'Ahead of schedule: the work done is 133% of what was planned by today.',
      tone: 'good',
    });
    expect(scheduleSentence(7900)).toEqual({
      text: 'Behind schedule: the work done is 79% of what was planned by today.',
      tone: 'warning',
    });
    expect(scheduleSentence(10000)?.text).toBe('On schedule: the work done matches the plan for today.');
  });

  it('says nothing without a schedule to measure against', () => {
    expect(scheduleSentence(null)).toBeNull();
  });

  it('says the plan is the schedule when the planned value follows the project schedule', () => {
    expect(scheduleSentence(7900, 'Schedule')?.text).toBe(
      'Behind schedule: the work done is 79% of what the schedule planned by today.',
    );
    expect(scheduleSentence(10000, 'Schedule')?.text).toBe(
      'On schedule: the work done matches what the schedule planned for today.',
    );
    expect(scheduleSentence(13333, 'ProjectDates')?.text).toBe(
      'Ahead of schedule: the work done is 133% of what was planned by today.',
    );
  });
});

describe('forecastSentence', () => {
  it('says how far over or under the budget the forecast is', () => {
    const over = forecastSentence(earnedValueFixture({ forecastMinor: 2127000000, forecastVarianceMinor: -336000000 }), 'PHP');
    expect(over).toEqual({
      text: 'Forecast to finish at ₱21,270,000.00, which is ₱3,360,000.00 over the budget.',
      tone: 'warning',
    });

    const under = forecastSentence(earnedValueFixture({ forecastMinor: 85000000, forecastVarianceMinor: 85000000 }), 'PHP');
    expect(under.text).toBe('Forecast to finish at ₱850,000.00, which is ₱850,000.00 under the budget.');
    expect(under.tone).toBe('good');

    expect(forecastSentence(earnedValueFixture({ forecastVarianceMinor: 0 }), 'PHP').text).toContain('right on the budget');
  });
});

describe('forecastMargin', () => {
  it('shows the server margin, flags a negative one, and is absent without a contract price', () => {
    expect(forecastMargin(earnedValueFixture({ forecastMarginMinor: 215000000 }), 'PHP')).toEqual({
      text: '₱2,150,000.00',
      negative: false,
    });
    expect(forecastMargin(earnedValueFixture({ forecastMarginMinor: -5000 }), 'PHP')).toEqual({
      text: '-₱50.00',
      negative: true,
    });
    expect(forecastMargin(earnedValueFixture({ forecastMarginMinor: null }), 'PHP')).toBeNull();
  });
});
