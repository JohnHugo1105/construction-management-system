import {
  designationProblem,
  designationRule,
  latestRate,
  rateChangeDates,
  rateInForce,
} from './rate-change';
import { WorkerRate } from './workforce.models';

const line = (effectiveFrom: string, designation: string, rateMinor: number): WorkerRate => ({
  id: effectiveFrom,
  designation,
  rateMinor,
  rateUnit: 'Daily',
  category: 'Initial',
  effectiveFrom,
  reason: null,
  recordedAtUtc: '2026-01-01T00:00:00Z',
});

const history = [
  line('2026-03-01', 'Mason', 80000),
  line('2026-10-25', 'Foreman', 95000),
  line('2026-12-01', 'Foreman', 100000),
];

describe('pay history helpers', () => {
  it('finds the newest line, even one that has not started', () => {
    expect(latestRate(history)?.effectiveFrom).toBe('2026-12-01');
    expect(latestRate([])).toBeNull();
  });

  it('finds the line in force on a day: the newest that has started', () => {
    expect(rateInForce(history, '2026-11-01')?.designation).toBe('Foreman');
    expect(rateInForce(history, '2026-11-01')?.rateMinor).toBe(95000);
    expect(rateInForce(history, '2026-02-01')).toBeNull();
  });

  it('only allows a new rate to start after the newest one, never before the hire date', () => {
    const dates = rateChangeDates(history, '2026-03-01', '2026-11-01');
    // The newest line starts on 1 Dec, so nothing earlier than 2 Dec can be added.
    expect(dates.earliest).toBe('2026-12-02');
    expect(dates.suggested).toBe('2026-12-02');
  });

  it('suggests today when today is allowed', () => {
    const dates = rateChangeDates([line('2026-03-01', 'Mason', 80000)], '2026-03-01', '2026-11-01');
    expect(dates.earliest).toBe('2026-03-02');
    expect(dates.suggested).toBe('2026-11-01');
  });

  it('starts a worker hired today tomorrow: the hired line already holds today', () => {
    const dates = rateChangeDates([line('2026-11-01', 'Mason', 80000)], '2026-11-01', '2026-11-01');
    expect(dates.earliest).toBe('2026-11-02');
    expect(dates.suggested).toBe('2026-11-02');
  });
});

describe('the designation follows the category', () => {
  it('keeps the designation for a rate adjustment and changes it for a promotion or demotion', () => {
    expect(designationRule('RateAdjustment')).toBe('keep');
    expect(designationRule('Promotion')).toBe('change');
    expect(designationRule('Demotion')).toBe('change');
    expect(designationRule(null)).toBeNull();
    expect(designationRule('Initial')).toBeNull();
  });

  it('refuses a promotion or demotion that keeps the designation, whatever the capitals', () => {
    expect(designationProblem('Promotion', 'mason', 'Mason')).toContain('changes the designation');
    expect(designationProblem('Demotion', ' Mason ', 'Mason')).toContain('changes the designation');
    expect(designationProblem('Promotion', 'Foreman', 'Mason')).toBeNull();
  });

  it('does not nag about a designation still being typed', () => {
    expect(designationProblem('Promotion', '', 'Mason')).toBeNull();
  });

  it('refuses a rate adjustment that changes the designation', () => {
    expect(designationProblem('RateAdjustment', 'Foreman', 'Mason')).toContain(
      'keeps the designation',
    );
    expect(designationProblem('RateAdjustment', 'mason', 'Mason')).toBeNull();
  });
});
