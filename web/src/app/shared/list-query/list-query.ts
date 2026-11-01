import { Signal, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, ParamMap, Params, Router } from '@angular/router';
import { map } from 'rxjs';

// Router-owned list state. The search text, filters and page of a list live in the address bar's query string, so a refresh,
// the back button and a shared link all show the same list. The page does not keep its own copy: it reads `values()`, asks
// the API with them, and calls `set(...)` when the person changes a filter.
//
//     readonly query = injectListQuery({
//       search: listField.text(),
//       status: listField.choice(['All', 'Active', 'Archived'], 'All'),
//     });
//     // query.values() is { search: string; status: 'All' | 'Active' | 'Archived'; page: number }
//     // query.set({ status: 'Active' })      -> ?status=Active           (the page resets to 1)
//     // query.setPage(3)                      -> ?status=Active&page=3
//
// A value equal to its default is left out of the address, so the plain URL is always the default list.

/** How one query-string key is read, written and defaulted. */
export interface ListQueryField<T> {
  readonly defaultValue: T;
  /** Reads the raw text from the address; anything unusable gives the default. */
  parse(raw: string | null): T;
  /** The text to put in the address, or null to leave the key out. */
  serialize(value: T): string | null;
}

export type ListQuerySchema = Record<string, ListQueryField<unknown>>;

type FieldValues<S extends ListQuerySchema> = {
  [K in keyof S]: S[K] extends ListQueryField<infer V> ? V : never;
};

/** What a list reads: its own fields plus the page number (1 or more). */
export type ListQueryValues<S extends ListQuerySchema> = FieldValues<S> & { readonly page: number };

/** Ready-made field kinds. */
export const listField = {
  /** Free text such as a search box. Trimmed; empty means "not set". */
  text(defaultValue = ''): ListQueryField<string> {
    return {
      defaultValue,
      parse: (raw) => (raw === null ? defaultValue : raw.trim() || defaultValue),
      serialize: (value) => (value.trim() === defaultValue ? null : value.trim()),
    };
  },

  /** A business date written "2026-12-03"; anything else in the address is ignored. Empty means "not set". */
  date(): ListQueryField<string> {
    return {
      defaultValue: '',
      parse: (raw) => (raw !== null && /^\d{4}-\d{2}-\d{2}$/.test(raw) ? raw : ''),
      serialize: (value) => (value === '' ? null : value),
    };
  },

  /** One of a fixed set of values; an unknown value in the address falls back to the default. */
  choice<T extends string>(allowed: readonly T[], defaultValue: T): ListQueryField<T> {
    return {
      defaultValue,
      parse: (raw) => allowed.find((item) => item === raw) ?? defaultValue,
      serialize: (value) => (value === defaultValue ? null : value),
    };
  },

  /** A yes/no switch, written as `1`. */
  flag(defaultValue = false): ListQueryField<boolean> {
    return {
      defaultValue,
      parse: (raw) => (raw === null ? defaultValue : raw === '1' || raw === 'true'),
      serialize: (value) => (value === defaultValue ? null : value ? '1' : '0'),
    };
  },

  /** A whole number within limits, such as a page size. */
  integer(defaultValue: number, min = 1, max = Number.MAX_SAFE_INTEGER): ListQueryField<number> {
    return {
      defaultValue,
      parse: (raw) => {
        const value = raw !== null && /^\d+$/.test(raw) ? Number(raw) : Number.NaN;
        return Number.isSafeInteger(value) && value >= min && value <= max ? value : defaultValue;
      },
      serialize: (value) => (value === defaultValue ? null : String(value)),
    };
  },
};

const PAGE_KEY = 'page';
const pageField = listField.integer(1, 1);

/** Reads the list's values out of a query string. Pure, so it is easy to test. */
export function readListQuery<S extends ListQuerySchema>(
  schema: S,
  params: ParamMap,
): ListQueryValues<S> {
  const values: Record<string, unknown> = { [PAGE_KEY]: pageField.parse(params.get(PAGE_KEY)) };
  for (const [key, field] of Object.entries(schema)) values[key] = field.parse(params.get(key));
  return values as ListQueryValues<S>;
}

/** The query parameters for a set of values: a key at its default is `null`, which removes it from the address. */
export function writeListQuery<S extends ListQuerySchema>(
  schema: S,
  values: ListQueryValues<S>,
): Params {
  const params: Params = { [PAGE_KEY]: pageField.serialize(values.page) };
  for (const [key, field] of Object.entries(schema))
    params[key] = field.serialize(values[key as keyof S]);
  return params;
}

export interface ListQueryChange {
  /** Replace the current history entry instead of adding one (right for typing in a search box). */
  readonly replace?: boolean;
}

/** The router-owned state of one list. Create it with `injectListQuery` in the page or its state class. */
export class ListQuery<S extends ListQuerySchema> {
  /** The current values, from the address bar. Changes only when a value really changes. */
  readonly values: Signal<ListQueryValues<S>>;

  constructor(
    private readonly schema: S,
    private readonly router: Router,
    private readonly route: ActivatedRoute,
  ) {
    const fromAddress = toSignal(
      route.queryParamMap.pipe(map((params) => readListQuery(schema, params))),
      {
        initialValue: readListQuery(schema, route.snapshot.queryParamMap),
      },
    );
    this.values = computed(() => fromAddress(), {
      equal: (a, b) => JSON.stringify(a) === JSON.stringify(b),
    });
  }

  /** Changes one or more filters. Showing a different list starts again at page 1. */
  set(patch: Partial<FieldValues<S>>, change: ListQueryChange = {}): void {
    void this.navigate({ ...this.values(), ...patch, [PAGE_KEY]: 1 } as ListQueryValues<S>, change);
  }

  setPage(page: number): void {
    void this.navigate(
      { ...this.values(), [PAGE_KEY]: Math.max(1, Math.floor(page)) } as ListQueryValues<S>,
      {},
    );
  }

  /** Back to the default list: no search, no filters, page 1. */
  reset(): void {
    const cleared: Record<string, unknown> = { [PAGE_KEY]: 1 };
    for (const [key, field] of Object.entries(this.schema)) cleared[key] = field.defaultValue;
    void this.navigate(cleared as ListQueryValues<S>, {});
  }

  /** True when anything differs from the default list. */
  readonly isFiltered = computed(() => {
    const values = this.values() as Record<string, unknown>;
    return Object.entries(this.schema).some(
      ([key, field]) => field.serialize(values[key]) !== null,
    );
  });

  private navigate(values: ListQueryValues<S>, change: ListQueryChange): Promise<boolean> {
    return this.router.navigate([], {
      relativeTo: this.route,
      queryParams: writeListQuery(this.schema, values),
      queryParamsHandling: 'merge',
      replaceUrl: change.replace ?? false,
    });
  }
}

/** Creates the router-owned query for the list being built. Call it where `inject()` is allowed (a field initializer). */
export function injectListQuery<S extends ListQuerySchema>(schema: S): ListQuery<S> {
  return new ListQuery(schema, inject(Router), inject(ActivatedRoute));
}
