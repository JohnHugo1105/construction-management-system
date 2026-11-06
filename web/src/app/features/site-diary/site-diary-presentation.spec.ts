import {
  dayBefore,
  dayWord,
  headcountLine,
  quantityText,
  reportDayLabel,
  weatherLabel,
  workersWords,
} from './site-diary-presentation';

describe('site diary presentation', () => {
  it('names the weather in the words a person uses', () => {
    expect(weatherLabel('HeavyRain')).toBe('Heavy rain');
    expect(weatherLabel('Fair')).toBe('Fair');
  });

  it('writes a report day with its day of the week', () => {
    expect(reportDayLabel('2026-11-01')).toBe('Sun 1 Nov 2026');
    expect(reportDayLabel('2026-12-03')).toBe('Thu 3 Dec 2026');
  });

  it('says "today" for today and "that day" for any other day', () => {
    expect(dayWord('2026-11-02', '2026-11-02')).toBe('today');
    expect(dayWord('2026-11-01', '2026-11-02')).toBe('that day');
  });

  it('counts workers in the singular and the plural, and leaves out an empty subcontractor crew', () => {
    expect(workersWords(1)).toBe('1 worker');
    expect(workersWords(12)).toBe('12 workers');
    expect(headcountLine(12, 6)).toBe('12 workers on site, 6 subcontractor workers');
    expect(headcountLine(1, 1)).toBe('1 worker on site, 1 subcontractor worker');
    expect(headcountLine(0, 0)).toBe('0 workers on site');
  });

  it('shows a quantity without trailing zeros', () => {
    expect(quantityText(55)).toBe('55');
    expect(quantityText(12.5)).toBe('12.5');
    expect(quantityText(1200)).toBe('1,200');
  });

  it('finds the day before a date, across a month and a year', () => {
    expect(dayBefore('2026-11-01')).toBe('2026-10-31');
    expect(dayBefore('2027-01-01')).toBe('2026-12-31');
  });
});
