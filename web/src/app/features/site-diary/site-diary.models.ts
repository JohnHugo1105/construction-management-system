/** The site diary as the API sends it: one report per project per day. A report carries no money anywhere. */

export type DiaryWeather = 'Fair' | 'Cloudy' | 'Rain' | 'HeavyRain' | 'Storm';

export interface DiaryListItem {
  readonly id: string;
  /** The day the report is about, "2026-11-02". */
  readonly reportDate: string;
  readonly weather: DiaryWeather;
  /** Workers with attendance that day on the project (a count, for every reader). */
  readonly workersOnSite: number;
  readonly subcontractorWorkers: number;
  readonly workDoneFirstLine: string;
  readonly authorName: string | null;
  readonly version: number;
}

/** Who was on site that day. The names come only for a reader who may see the worker list. */
export interface DiaryOnSite {
  readonly count: number;
  readonly names?: readonly string[];
}

/** One item received that day: never a price or a total. */
export interface DiaryDelivery {
  readonly supplierName: string;
  readonly description: string;
  readonly unit: string;
  readonly quantity: number;
}

/** What is read beside a report instead of being typed. */
export interface DiaryFilledIn {
  readonly onSite: DiaryOnSite;
  readonly deliveries: readonly DiaryDelivery[];
}

export interface DiaryEntry extends DiaryFilledIn {
  readonly id: string;
  readonly projectId: string;
  readonly projectName: string;
  readonly projectArchived: boolean;
  readonly reportDate: string;
  readonly weather: DiaryWeather;
  readonly weatherNote: string | null;
  readonly workDone: string;
  readonly issuesAndDelays: string | null;
  readonly visitorsAndInspections: string | null;
  readonly safetyNotes: string | null;
  readonly subcontractorWorkers: number;
  readonly status: 'Posted' | 'Void';
  readonly authorName: string | null;
  readonly createdAtUtc: string;
  readonly updatedAtUtc: string;
  /** The last day the author may change the report; a Project Manager or an Administrator are not limited by it. */
  readonly changeableUntil: string;
  /** The signed-in person may correct or void this report now. */
  readonly canChange: boolean;
  readonly version: number;
}

/** What a person types in a report. */
export interface DiaryDetailsInput {
  readonly weather: DiaryWeather;
  readonly weatherNote: string | null;
  readonly workDone: string;
  readonly issuesAndDelays: string | null;
  readonly visitorsAndInspections: string | null;
  readonly safetyNotes: string | null;
  readonly subcontractorWorkers: number;
}
