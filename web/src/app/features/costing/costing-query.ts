import { BudgetLineFilters, CostEntryFilters } from './costing.models';

// The query-string parameters the list and export endpoints share. A filter at its default is left out, so the list, its
// export and the address bar all describe the same set of rows with the same words.

type Query = Record<string, string>;

function put(query: Query, name: string, value: string): void {
  if (value !== '') query[name] = value;
}

function divisionText(division: number): string {
  return division > 0 ? String(division) : '';
}

/** The API's filter parameters for budget lines. */
export function budgetLineQuery(filters: BudgetLineFilters): Query {
  const query: Query = {};
  put(query, 'subProjectId', filters.subProjectId);
  put(query, 'division', divisionText(filters.division));
  put(query, 'itemType', filters.itemType);
  put(query, 'search', filters.search.trim());
  if (filters.includeRemoved) query['includeRemoved'] = 'true';
  return query;
}

/** The API's filter parameters for cost entries. */
export function costEntryQuery(filters: CostEntryFilters): Query {
  const query: Query = {};
  put(query, 'subProjectId', filters.subProjectId);
  put(query, 'division', divisionText(filters.division));
  put(query, 'itemType', filters.itemType);
  put(query, 'origin', filters.origin);
  put(query, 'from', filters.from);
  put(query, 'to', filters.to);
  put(query, 'search', filters.search.trim());
  if (filters.includeVoided) query['includeVoided'] = 'true';
  return query;
}
