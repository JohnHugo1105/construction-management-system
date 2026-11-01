import { Injectable, computed, inject, linkedSignal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { asApiError } from '../../core/api/api-error';
import { DEFAULT_PAGE_SIZE, PagedResult } from '../../core/api/paged-result';
import { injectListQuery, listField } from '../../shared/list-query/list-query';
import { BondsApi } from './bonds-api';
import { BOND_STATES, BondState, stateFilter } from './bonds-presentation';
import { BondDirection, BondKind, BondListItem } from './bonds.models';

const DIRECTIONS: readonly ('' | BondDirection)[] = ['', 'Posted', 'Received'];
const KINDS: readonly ('' | BondKind)[] = [
  '',
  'Performance',
  'AdvancePayment',
  'Warranty',
  'Bid',
  'Other',
];

/**
 * The register's list: the filters (project, direction, kind, state) and the page live in the address bar, the rows come from the API.
 * It opens on the Active bonds. Provide it on the page that shows the register.
 */
@Injectable()
export class BondListState {
  private readonly api = inject(BondsApi);

  readonly query = injectListQuery({
    project: listField.text(),
    direction: listField.choice<'' | BondDirection>(DIRECTIONS, ''),
    kind: listField.choice<'' | BondKind>(KINDS, ''),
    state: listField.choice<BondState>(BOND_STATES, 'Active'),
  });

  private readonly resource = rxResource({
    params: () => this.query.values(),
    stream: ({ params }) =>
      this.api.list({
        projectId: params.project,
        direction: params.direction,
        kind: params.kind,
        ...stateFilter(params.state),
        page: params.page,
        pageSize: DEFAULT_PAGE_SIZE,
      }),
  });

  /** The latest page; the one before it while the next loads. */
  readonly result = linkedSignal<
    PagedResult<BondListItem> | undefined,
    PagedResult<BondListItem> | undefined
  >({
    source: () => (this.resource.hasValue() ? this.resource.value() : undefined),
    computation: (value, previous) => value ?? previous?.value,
  });

  readonly items = computed(() => this.result()?.items ?? []);
  readonly total = computed(() => this.result()?.total ?? 0);
  readonly pageSize = DEFAULT_PAGE_SIZE;
  readonly loadingFirst = computed(() => this.resource.isLoading() && this.result() === undefined);
  readonly refreshing = computed(() => this.resource.isLoading() && this.result() !== undefined);
  readonly error = computed(() => {
    const failure = this.resource.error();
    return failure ? asApiError(failure) : null;
  });

  reload(): void {
    this.resource.reload();
  }
}
