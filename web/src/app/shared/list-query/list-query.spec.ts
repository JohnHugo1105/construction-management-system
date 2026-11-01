import { ChangeDetectionStrategy, Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { convertToParamMap, provideRouter, Router } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { ListQuery, injectListQuery, listField, readListQuery, writeListQuery } from './list-query';

const schema = {
  search: listField.text(),
  status: listField.choice(['All', 'Active', 'Archived'], 'All'),
  from: listField.date(),
  archived: listField.flag(),
  size: listField.integer(25, 1, 100),
};

describe('reading a list query from the address', () => {
  it('gives the defaults when the address has no query', () => {
    expect(readListQuery(schema, convertToParamMap({}))).toEqual({
      page: 1,
      search: '',
      status: 'All',
      from: '',
      archived: false,
      size: 25,
    });
  });

  it('reads what is there', () => {
    const values = readListQuery(
      schema,
      convertToParamMap({
        search: '  tower  ',
        status: 'Active',
        page: '3',
        from: '2026-12-03',
        archived: '1',
        size: '50',
      }),
    );

    expect(values).toEqual({
      page: 3,
      search: 'tower',
      status: 'Active',
      from: '2026-12-03',
      archived: true,
      size: 50,
    });
  });

  it('falls back to the default for anything unusable instead of failing', () => {
    const values = readListQuery(
      schema,
      convertToParamMap({
        status: 'Bogus',
        page: '-2',
        from: 'yesterday',
        size: '5000',
        archived: 'maybe',
      }),
    );

    expect(values).toEqual({
      page: 1,
      search: '',
      status: 'All',
      from: '',
      archived: false,
      size: 25,
    });
    expect(readListQuery(schema, convertToParamMap({ page: 'abc' })).page).toBe(1);
    expect(readListQuery(schema, convertToParamMap({ page: '0' })).page).toBe(1);
  });
});

describe('writing a list query to the address', () => {
  it('leaves out everything that equals its default, so the plain URL is the default list', () => {
    const params = writeListQuery(schema, {
      page: 1,
      search: '',
      status: 'All',
      from: '',
      archived: false,
      size: 25,
    });

    expect(Object.values(params).every((value) => value === null)).toBe(true);
  });

  it('writes only what differs', () => {
    const params = writeListQuery(schema, {
      page: 2,
      search: 'tower',
      status: 'Active',
      from: '',
      archived: true,
      size: 25,
    });

    expect(params).toEqual({
      page: '2',
      search: 'tower',
      status: 'Active',
      from: null,
      archived: '1',
      size: null,
    });
  });
});

@Component({ template: '', changeDetection: ChangeDetectionStrategy.OnPush })
class ListPage {
  readonly query: ListQuery<typeof schema> = injectListQuery(schema);
}

/** The query parameters of an address, so a test does not depend on their order. */
function paramsOf(url: string): Record<string, string> {
  return Object.fromEntries(new URL(url, 'http://app.test').searchParams.entries());
}

async function open(url: string) {
  TestBed.configureTestingModule({
    providers: [provideRouter([{ path: 'list', component: ListPage }])],
  });
  const harness = await RouterTestingHarness.create();
  const page = await harness.navigateByUrl(url, ListPage);
  return { harness, page, router: TestBed.inject(Router) };
}

describe('injectListQuery', () => {
  it('follows the address bar', async () => {
    const { page } = await open('/list?search=tower&status=Active&page=2');

    expect(page.query.values()).toEqual({
      page: 2,
      search: 'tower',
      status: 'Active',
      from: '',
      archived: false,
      size: 25,
    });
    expect(page.query.isFiltered()).toBe(true);
  });

  it('writes a changed filter to the address and starts again at page 1', async () => {
    const { harness, page, router } = await open('/list?status=Active&page=3');

    page.query.set({ search: 'tower' });
    await harness.fixture.whenStable();

    expect(paramsOf(router.url)).toEqual({ search: 'tower', status: 'Active' });
    expect(page.query.values().page).toBe(1);
    expect(page.query.values().search).toBe('tower');
  });

  it('changes only the page when paging, keeping the filters', async () => {
    const { harness, page, router } = await open('/list?status=Active');

    page.query.setPage(4);
    await harness.fixture.whenStable();

    expect(paramsOf(router.url)).toEqual({ status: 'Active', page: '4' });
    expect(page.query.values()).toMatchObject({ status: 'Active', page: 4 });
  });

  it('removes a filter from the address when it goes back to its default', async () => {
    const { harness, page, router } = await open('/list?search=tower&status=Active');

    page.query.set({ status: 'All' });
    await harness.fixture.whenStable();

    expect(paramsOf(router.url)).toEqual({ search: 'tower' });
  });

  it('resets to the plain list', async () => {
    const { harness, page, router } = await open('/list?search=tower&status=Active&page=2');

    page.query.reset();
    await harness.fixture.whenStable();

    expect(router.url).toBe('/list');
    expect(page.query.isFiltered()).toBe(false);
  });

  it('keeps the same values object while an unrelated query parameter changes', async () => {
    const { harness, page, router } = await open('/list?search=tower');
    const before = page.query.values();

    await router.navigateByUrl('/list?search=tower&utm=1');
    await harness.fixture.whenStable();

    expect(page.query.values()).toBe(before);
  });
});
