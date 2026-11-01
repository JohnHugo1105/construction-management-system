import { RestDay } from './workforce.models';

/** What the form sends for "no fixed rest day". */
export const NO_REST_DAY = 'None';

export type RestDayChoice = RestDay | typeof NO_REST_DAY;

/** The choices, Sunday first (the usual rest day of a crew), after "no fixed rest day" (the default). */
export const REST_DAY_CHOICES: readonly {
  readonly value: RestDayChoice;
  readonly label: string;
}[] = [
  { value: NO_REST_DAY, label: 'No fixed rest day' },
  { value: 'Sunday', label: 'Sunday' },
  { value: 'Monday', label: 'Monday' },
  { value: 'Tuesday', label: 'Tuesday' },
  { value: 'Wednesday', label: 'Wednesday' },
  { value: 'Thursday', label: 'Thursday' },
  { value: 'Friday', label: 'Friday' },
  { value: 'Saturday', label: 'Saturday' },
];

export const REST_DAY_HINT =
  'Hours on the rest day earn the rest-day rate. Leave it as no fixed rest day when the worker has none.';

/** "Sunday", or "No fixed rest day". */
export function restDayWords(restDay: RestDay | null | undefined): string {
  return restDay ?? 'No fixed rest day';
}
