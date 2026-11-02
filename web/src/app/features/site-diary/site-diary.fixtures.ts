import { Permission } from '../../core/session/permissions';
import { addDays, todayIn } from '../../shared/presentation/business-time';
import { DiaryEntry, DiaryListItem } from './site-diary.models';

/** Reads reports only (a Warehouse Keeper, an Accountant, a Viewer). */
export const READER = [Permission.projectsRead, Permission.diaryRead];
/** Reads and writes reports (a Cost Engineer, a Project Manager, an Administrator). */
export const WRITER = [...READER, Permission.diaryWrite];

/** Today where the company works, as the screens see it. */
export const TODAY = todayIn('Asia/Manila');

export function daysFromToday(days: number): string {
  return addDays(TODAY, days) ?? TODAY;
}

export function listItem(overrides: Partial<DiaryListItem> = {}): DiaryListItem {
  return {
    id: 'd1',
    reportDate: daysFromToday(-1),
    weather: 'Fair',
    workersOnSite: 12,
    subcontractorWorkers: 6,
    workDoneFirstLine: 'Poured the second-floor slab.',
    authorName: 'Pia Santos',
    version: 0,
    ...overrides,
  };
}

export function entry(overrides: Partial<DiaryEntry> = {}): DiaryEntry {
  return {
    id: 'd1',
    projectId: 'p1',
    projectName: 'Riverside Residences',
    projectArchived: false,
    reportDate: daysFromToday(-1),
    weather: 'Rain',
    weatherNote: 'Showers from 1 pm',
    workDone: 'Poured the second-floor slab.\nTied rebar for the stair.',
    issuesAndDelays: 'The concrete truck came late.',
    visitorsAndInspections: null,
    safetyNotes: null,
    subcontractorWorkers: 6,
    status: 'Posted',
    authorName: 'Pia Santos',
    createdAtUtc: '2026-11-02T10:00:00Z',
    updatedAtUtc: '2026-11-02T10:00:00Z',
    changeableUntil: TODAY,
    canChange: true,
    onSite: { count: 2, names: ['Ana Abad', 'Juan Reyes'] },
    deliveries: [
      { supplierName: 'Visayas Steel Corp.', description: 'Rebar 16 mm', unit: 'pc', quantity: 55 },
    ],
    version: 0,
    ...overrides,
  };
}
