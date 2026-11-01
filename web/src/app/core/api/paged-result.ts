/** What every list endpoint returns: one page of rows and the total across all pages. Page size is at most 100. */
export interface PagedResult<T> {
  readonly items: readonly T[];
  readonly total: number;
  readonly page: number;
  readonly pageSize: number;
}

/** The page size lists ask for unless they have a reason to differ (the API default is also 25). */
export const DEFAULT_PAGE_SIZE = 25;
