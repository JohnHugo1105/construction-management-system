import { convertToParamMap } from '@angular/router';
import { readListQuery, writeListQuery } from '../../shared/list-query/list-query';
import { BUDGET_QUERY, toBudgetFilters } from './project-budget-tab/budget-state';
import { COSTS_QUERY, toCostFilters } from './project-costs-tab/costs-state';
import { budgetLineQuery, costEntryQuery } from './costing-query';
import {
  budgetLinesExportUrl,
  costEntriesExportUrl,
  costSummaryExportUrl,
} from './costing-exports';

const SUB = '3f2504e0-4f89-41d3-9a0c-0305e82c3301';

describe('export links', () => {
  it('has no query string when no filter is set', () => {
    expect(
      budgetLinesExportUrl('p1', {
        subProjectId: '',
        division: 0,
        itemType: '',
        search: '',
        includeRemoved: false,
      }),
    ).toBe('/api/projects/p1/budget-lines/export');
  });

  it('carries the current budget line filters in the API words', () => {
    const url = budgetLinesExportUrl('p1', {
      subProjectId: SUB,
      division: 3,
      itemType: 'Material',
      search: '  rebar ',
      includeRemoved: true,
    });
    const [path, query] = url.split('?');
    expect(path).toBe('/api/projects/p1/budget-lines/export');
    expect(Object.fromEntries(new URLSearchParams(query))).toEqual({
      subProjectId: SUB,
      division: '3',
      itemType: 'Material',
      search: 'rebar',
      includeRemoved: 'true',
    });
  });

  it('carries the cost entry filters, including the dates and the source', () => {
    const url = costEntriesExportUrl('p1', {
      subProjectId: '',
      division: 0,
      itemType: '',
      origin: 'Payroll',
      from: '2026-10-01',
      to: '2026-10-31',
      search: 'CPS & Co',
      includeVoided: true,
    });
    const [path, query] = url.split('?');
    expect(path).toBe('/api/projects/p1/cost-entries/export');
    expect(Object.fromEntries(new URLSearchParams(query))).toEqual({
      origin: 'Payroll',
      from: '2026-10-01',
      to: '2026-10-31',
      search: 'CPS & Co',
      includeVoided: 'true',
    });
    // Text with special characters is encoded, not pasted into the address.
    expect(query).toContain('CPS+%26+Co');
  });

  it('links the cost summary, which has no filters', () => {
    expect(costSummaryExportUrl('p1')).toBe('/api/projects/p1/cost-summary/export');
  });
});

describe('the address bar and the API filters', () => {
  it('reads the budget address into the filters the API gets, and writes it back unchanged', () => {
    const params = convertToParamMap({
      search: 'rebar',
      subProject: SUB,
      division: '3',
      itemType: 'Material',
      removed: '1',
      page: '2',
    });
    const values = readListQuery(BUDGET_QUERY, params);
    expect(values).toEqual({
      search: 'rebar',
      subProject: SUB,
      division: 3,
      itemType: 'Material',
      removed: true,
      page: 2,
    });
    expect(budgetLineQuery(toBudgetFilters(values))).toEqual({
      subProjectId: SUB,
      division: '3',
      itemType: 'Material',
      search: 'rebar',
      includeRemoved: 'true',
    });
    expect(writeListQuery(BUDGET_QUERY, values)).toEqual({
      page: '2',
      search: 'rebar',
      subProject: SUB,
      division: '3',
      itemType: 'Material',
      removed: '1',
    });
  });

  it('leaves the default list out of the address', () => {
    const values = readListQuery(BUDGET_QUERY, convertToParamMap({}));
    expect(writeListQuery(BUDGET_QUERY, values)).toEqual({
      page: null,
      search: null,
      subProject: null,
      division: null,
      itemType: null,
      removed: null,
    });
    expect(budgetLineQuery(toBudgetFilters(values))).toEqual({});
  });

  it('ignores values in the address that the API would refuse', () => {
    const values = readListQuery(
      BUDGET_QUERY,
      convertToParamMap({ subProject: 'not-a-guid', division: '99', itemType: 'Gold', page: '0' }),
    );
    expect(values).toMatchObject({ subProject: '', division: 0, itemType: '', page: 1 });
  });

  it('round-trips the cost filters', () => {
    const params = convertToParamMap({
      search: 'CPS-7781',
      origin: 'Manual',
      from: '2026-10-01',
      to: '2026-10-31',
      voided: '1',
      division: '15',
    });
    const values = readListQuery(COSTS_QUERY, params);
    expect(costEntryQuery(toCostFilters(values))).toEqual({
      division: '15',
      origin: 'Manual',
      from: '2026-10-01',
      to: '2026-10-31',
      search: 'CPS-7781',
      includeVoided: 'true',
    });
    expect(
      readListQuery(
        COSTS_QUERY,
        convertToParamMap(
          Object.fromEntries(
            Object.entries(writeListQuery(COSTS_QUERY, values)).filter(
              ([, value]) => value !== null,
            ),
          ) as Record<string, string>,
        ),
      ),
    ).toEqual(values);
  });

  it('ignores a bad date or source in the address', () => {
    const values = readListQuery(
      COSTS_QUERY,
      convertToParamMap({ from: '03/12/2026', origin: 'Barter' }),
    );
    expect(values).toMatchObject({ from: '', origin: '' });
  });
});
