import { budgetLineQuery, costEntryQuery } from './costing-query';
import { BudgetLineFilters, CostEntryFilters } from './costing.models';

// CSV downloads are plain same-origin links: the browser sends the session cookie and saves the file. The server picks the file
// name and sends every row that matches the filters (not only the page on screen).

function withQuery(path: string, query: Record<string, string>): string {
  const text = new URLSearchParams(query).toString();
  return text === '' ? path : `${path}?${text}`;
}

/** The budget lines CSV for the current filters. */
export function budgetLinesExportUrl(projectId: string, filters: BudgetLineFilters): string {
  return withQuery(`/api/projects/${projectId}/budget-lines/export`, budgetLineQuery(filters));
}

/** The cost entries CSV for the current filters. */
export function costEntriesExportUrl(projectId: string, filters: CostEntryFilters): string {
  return withQuery(`/api/projects/${projectId}/cost-entries/export`, costEntryQuery(filters));
}

/** Budget against actual by work-breakdown code. It has no filters: it always covers the whole project. */
export function costSummaryExportUrl(projectId: string): string {
  return `/api/projects/${projectId}/cost-summary/export`;
}
