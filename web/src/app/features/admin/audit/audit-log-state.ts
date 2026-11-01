import { Injectable, computed, inject, linkedSignal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { asApiError } from '../../../core/api/api-error';
import { DEFAULT_PAGE_SIZE, PagedResult } from '../../../core/api/paged-result';
import { SessionState } from '../../../core/session/session-state';
import { addDays, startOfDayUtc } from '../../../shared/presentation/business-time';
import { injectListQuery, listField } from '../../../shared/list-query/list-query';
import { AuditApi } from './audit-api';
import { AUDIT_ENTITY_TYPES, AuditEntry } from './audit.models';

/** The "all areas" value of the area filter. */
export const ALL_AREAS = 'All';

/**
 * The activity log's state: search, area, date range and page live in the address bar. The dates are business days in the
 * company time zone, turned into the exact instants the API expects (the end day is included).
 */
@Injectable()
export class AuditLogState {
  private readonly api = inject(AuditApi);
  private readonly session = inject(SessionState);

  readonly query = injectListQuery({
    search: listField.text(),
    area: listField.choice<string>(
      [ALL_AREAS, ...AUDIT_ENTITY_TYPES.map((type) => type.value)],
      ALL_AREAS,
    ),
    from: listField.date(),
    to: listField.date(),
  });

  private readonly resource = rxResource({
    params: () => ({ values: this.query.values(), zone: this.session.timeZoneId() }),
    stream: ({ params }) =>
      this.api.list({
        search: params.values.search,
        entityType: params.values.area === ALL_AREAS ? '' : params.values.area,
        from: params.values.from ? (startOfDayUtc(params.values.from, params.zone) ?? '') : '',
        to: params.values.to
          ? (startOfDayUtc(addDays(params.values.to, 1) ?? params.values.to, params.zone) ?? '')
          : '',
        page: params.values.page,
        pageSize: DEFAULT_PAGE_SIZE,
      }),
  });

  readonly result = linkedSignal<
    PagedResult<AuditEntry> | undefined,
    PagedResult<AuditEntry> | undefined
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
