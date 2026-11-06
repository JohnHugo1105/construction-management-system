import { addDays, formatDate } from '../../shared/presentation/business-time';
import { DiaryWeather } from './site-diary.models';

export interface WeatherChoice {
  readonly value: DiaryWeather;
  readonly label: string;
}

/** The weather chips of the form, in the order a person scans them. */
export const WEATHER_CHOICES: readonly WeatherChoice[] = [
  { value: 'Fair', label: 'Fair' },
  { value: 'Cloudy', label: 'Cloudy' },
  { value: 'Rain', label: 'Rain' },
  { value: 'HeavyRain', label: 'Heavy rain' },
  { value: 'Storm', label: 'Storm' },
];

export function weatherLabel(weather: DiaryWeather): string {
  return WEATHER_CHOICES.find((choice) => choice.value === weather)?.label ?? weather;
}

const WEEKDAYS = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'];

/** "2026-11-02" gives "Mon 2 Nov 2026": the day of the week helps a person find a day in a list of daily reports. */
export function reportDayLabel(date: string): string {
  const match = /^(\d{4})-(\d{2})-(\d{2})/.exec(date);
  if (!match) return formatDate(date);
  const weekday = new Date(Date.UTC(Number(match[1]), Number(match[2]) - 1, Number(match[3]))).getUTCDay();
  return `${WEEKDAYS[weekday]} ${formatDate(date)}`;
}

/** "today" for today, "that day" for any other, so the panels read "On site today" or "On site that day". */
export function dayWord(date: string, today: string): string {
  return date === today ? 'today' : 'that day';
}

/** "1 worker", "12 workers". */
export function workersWords(count: number, noun = 'worker'): string {
  return `${count} ${noun}${count === 1 ? '' : 's'}`;
}

/** The headcount line of a list row: "12 workers on site, 6 subcontractor workers". */
export function headcountLine(onSite: number, subcontractors: number): string {
  const parts = [`${workersWords(onSite)} on site`];
  if (subcontractors > 0) parts.push(workersWords(subcontractors, 'subcontractor worker'));
  return parts.join(', ');
}

/** A quantity as people write it: "40", "12.5" (never more than four decimals, no trailing zeros). */
export function quantityText(quantity: number): string {
  return new Intl.NumberFormat('en-PH', { maximumFractionDigits: 4 }).format(quantity);
}

/** The day before a business date, for the form's "an earlier day" shortcut. */
export function dayBefore(date: string): string {
  return addDays(date, -1) ?? date;
}
