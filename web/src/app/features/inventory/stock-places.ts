import { Injectable, computed, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { InventoryApi } from './inventory-api';
import { WAREHOUSE_NAME } from './inventory-presentation';
import { ProjectLookup, StockBalance, StockLocation } from './inventory.models';

/** A place as a select holds it: "warehouse", or a project's id. */
export const WAREHOUSE_KEY = 'warehouse';

export function balanceKey(balance: StockBalance): string {
  return balance.location === 'Warehouse' ? WAREHOUSE_KEY : (balance.projectId ?? '');
}

/** The request's way of naming a place, from a select's value. */
export function toLocation(key: string): StockLocation {
  return key === WAREHOUSE_KEY ? { kind: 'Warehouse' } : { kind: 'Project', projectId: key };
}

const GUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

export function isProjectKey(key: string): boolean {
  return GUID.test(key);
}

export interface PlaceOption {
  readonly value: string;
  readonly label: string;
}

/**
 * The warehouse and the company's projects, for the places a person picks from (the place filter, the From and To of a move, a
 * count). Provide it on the page that needs it and call `load()` once a picker is on screen: nothing is fetched before that.
 * Archived projects are kept apart because they cannot receive stock.
 */
@Injectable()
export class StockPlaces {
  private readonly api = inject(InventoryApi);
  private readonly requested = signal(false);

  private readonly lookup = rxResource({
    params: () => (this.requested() ? true : undefined),
    stream: () => this.api.projectLookup(true),
  });

  readonly loading = computed(() => this.lookup.isLoading());
  readonly failed = computed(() => this.lookup.error() !== undefined);
  readonly loaded = computed(() => this.lookup.hasValue());

  private readonly projects = computed<readonly ProjectLookup[]>(() =>
    this.lookup.hasValue() ? this.lookup.value() : [],
  );

  /** Projects that can receive stock, by name. */
  readonly open = computed(() =>
    this.projects()
      .filter((project) => project.status !== 'Archived')
      .map((project) => ({ value: project.id, label: project.name })),
  );

  /** Archived projects: stock can leave them but not arrive. */
  readonly archived = computed(() =>
    this.projects()
      .filter((project) => project.status === 'Archived')
      .map((project) => ({ value: project.id, label: project.name })),
  );

  /** Every project, open ones first. */
  readonly everyProject = computed<readonly PlaceOption[]>(() => [
    ...this.open(),
    ...this.archived(),
  ]);

  load(): void {
    this.requested.set(true);
  }

  isArchived(key: string): boolean {
    return this.archived().some((place) => place.value === key);
  }

  /** The name of a place from its key. */
  nameOf(key: string): string {
    if (key === WAREHOUSE_KEY) return WAREHOUSE_NAME;
    return this.everyProject().find((place) => place.value === key)?.label ?? 'A project';
  }
}
