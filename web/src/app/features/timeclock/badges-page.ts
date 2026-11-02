import {
  ChangeDetectionStrategy,
  Component,
  ViewEncapsulation,
  computed,
  effect,
  inject,
  input,
  signal,
  untracked,
} from '@angular/core';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { ApiError, asApiError } from '../../core/api/api-error';
import { ToastService } from '../../core/notifications/toast-service';
import { Alert } from '../../shared/ui/alert/alert';
import { ApiErrorAlert } from '../../shared/ui/api-error-alert/api-error-alert';
import { Button } from '../../shared/ui/button/button';
import { EmptyState } from '../../shared/ui/empty-state/empty-state';
import { PageHeader } from '../../shared/ui/page-header/page-header';
import { Skeleton } from '../../shared/ui/skeleton/skeleton';
import { BadgeCard } from './badge-card';
import { TimeClockApi } from './timeclock-api';
import { workers } from './timeclock-presentation';
import { BadgeSheet, WorkerOption } from './timeclock.models';

/** Badges printed on one A4 sheet: two across, five down. */
export const BADGES_PER_SHEET = 10;

/** The most badges chosen at once (the API prints at most 100). */
export const MAXIMUM_BADGES = 100;

/**
 * Badge cards for the time clock (`/time-clock/badges`, HR and administrators). Choose workers, or issue codes to everyone who has none, then print:
 * the print layout puts ten card-sized badges on each A4 sheet. The chosen workers are in the address (`?workers=`), so the page can be shared and
 * reloaded.
 */
@Component({
  selector: 'app-badges-page',
  imports: [PageHeader, Alert, ApiErrorAlert, Button, EmptyState, Skeleton, BadgeCard],
  changeDetection: ChangeDetectionStrategy.OnPush,
  // Unencapsulated so the print rules can hide the app around the badges; every class here starts with "badges-".
  encapsulation: ViewEncapsulation.None,
  template: `
    <app-page-header title="Badges" backLabel="Time clock" [backLink]="['/time-clock']">
      @if (printable().length > 0) {
        <button appButton variant="primary" type="button" (click)="print()">Print badges</button>
      }
    </app-page-header>

    <div class="stack badges-controls">
      @if (error(); as problem) {
        <app-api-error-alert
          [error]="problem"
          [allowRetry]="true"
          reloadLabel="Try again"
          (reload)="load()"
        />
      }

      @if ((sheet()?.activeWithoutCode ?? 0) > 0) {
        <app-alert tone="info" [title]="withoutCodeTitle()">
          A worker needs a code before their badge can be printed. Codes are random and say nothing
          about the worker.
          <div alertActions>
            <button
              appButton
              [variant]="printable().length > 0 ? 'secondary' : 'primary'"
              type="button"
              [busy]="issuing()"
              (click)="issueMissing()"
            >
              Issue codes and show their badges
            </button>
          </div>
        </app-alert>
      }

      <section class="card stack" aria-labelledby="badges-choose-heading">
        <h2 id="badges-choose-heading" class="card__title">Choose workers</h2>
        <form class="badges-search" role="search" (submit)="search($event)">
          <label class="field">
            <span class="field__label">Name or employee number</span>
            <input
              #searchBox
              class="input"
              name="search"
              autocomplete="off"
              (input)="term.set(searchBox.value)"
            />
          </label>
          <button appButton type="submit" [busy]="searching()">Find workers</button>
        </form>
        @if (searched() && found().length === 0) {
          <p class="badges-muted">No active worker matches "{{ lastTerm() }}".</p>
        }
        @if (found().length > 0) {
          <ul class="badges-found">
            @for (worker of found(); track worker.id) {
              <li>
                <span
                  >{{ worker.fullName }}
                  <span class="badges-muted">{{ worker.employeeNumber }}</span></span
                >
                @if (chosen().includes(worker.id)) {
                  <span class="badges-muted">Added</span>
                } @else {
                  <button
                    appButton
                    type="button"
                    (click)="add(worker)"
                    [attr.aria-label]="'Add ' + worker.fullName"
                  >
                    Add
                  </button>
                }
              </li>
            }
          </ul>
        }
      </section>
    </div>

    @if (loading() && !sheet()) {
      <app-skeleton variant="card" [lines]="3" label="Loading badges" />
    } @else if (chosen().length === 0) {
      <app-empty-state
        icon="users"
        title="No badges chosen yet"
        message="Find workers above, or issue codes to everyone who has none, to see their badges here."
      />
    } @else if (sheet(); as current) {
      <p class="badges-muted badges-controls">
        {{ workers(current.items.length) }} chosen.
        <button appButton variant="ghost" type="button" (click)="clear()">Clear the list</button>
      </p>
      @for (page of pages(); track $index) {
        <div class="badges-sheet">
          @for (badge of page; track badge.workerId) {
            <div class="badges-slot">
              @if (badge.code) {
                <app-badge-card [badge]="badge" [company]="current.companyName" />
              } @else {
                <div class="card badges-missing">
                  <p>
                    <strong>{{ badge.fullName }}</strong> has no badge code yet.
                  </p>
                </div>
              }
              <button
                appButton
                variant="ghost"
                type="button"
                class="badges-remove"
                [attr.aria-label]="'Remove ' + badge.fullName + ' from the list'"
                (click)="remove(badge.workerId)"
              >
                Remove
              </button>
            </div>
          }
        </div>
      }
    }
  `,
  styles: `
    .badges-search {
      display: grid;
      gap: var(--space-2);
    }

    .badges-found {
      display: grid;
      gap: var(--space-1);
      padding: 0;
      list-style: none;
    }

    .badges-found li {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      justify-content: space-between;
      gap: var(--space-2);
      min-height: var(--touch-target);
    }

    .badges-muted {
      color: var(--color-text-muted);
    }

    .badges-sheet {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(min(86mm, 100%), 1fr));
      gap: var(--space-4);
      margin-top: var(--space-4);
    }

    .badges-slot {
      display: grid;
      justify-items: start;
      gap: var(--space-1);
    }

    @media (min-width: 640px) {
      .badges-search {
        grid-template-columns: 1fr auto;
        align-items: end;
      }
    }

    @media print {
      @page {
        size: A4;
        margin: 10mm;
      }

      body * {
        visibility: hidden;
      }

      .badges-sheet,
      .badges-sheet * {
        visibility: visible;
      }

      .badges-controls,
      .badges-remove,
      .badges-missing {
        display: none !important;
      }

      .badges-sheet {
        position: relative;
        display: grid;
        grid-template-columns: 85.6mm 85.6mm;
        grid-auto-rows: 54mm;
        gap: 2mm 6mm;
        margin: 0;
        break-after: page;
      }

      .badges-sheet:last-of-type {
        break-after: auto;
      }
    }
  `,
})
export class BadgesPage {
  /** The chosen workers, comma-separated, from the address. */
  readonly workersParam = input<string | undefined>(undefined, { alias: 'workers' });

  private readonly api = inject(TimeClockApi);
  private readonly router = inject(Router);
  private readonly toast = inject(ToastService);

  protected readonly sheet = signal<BadgeSheet | null>(null);
  protected readonly loading = signal(false);
  protected readonly error = signal<ApiError | null>(null);
  protected readonly issuing = signal(false);
  protected readonly term = signal('');
  protected readonly lastTerm = signal('');
  protected readonly found = signal<readonly WorkerOption[]>([]);
  protected readonly searched = signal(false);
  protected readonly searching = signal(false);

  protected readonly workers = workers;

  protected readonly chosen = computed(() =>
    (this.workersParam() ?? '')
      .split(',')
      .map((id) => id.trim())
      .filter((id) => id !== '')
      .slice(0, MAXIMUM_BADGES),
  );

  protected readonly printable = computed(() =>
    (this.sheet()?.items ?? []).filter((badge) => badge.code),
  );

  protected readonly pages = computed(() => {
    const items = this.sheet()?.items ?? [];
    const pages = [];
    for (let start = 0; start < items.length; start += BADGES_PER_SHEET)
      pages.push(items.slice(start, start + BADGES_PER_SHEET));
    return pages;
  });

  protected readonly withoutCodeTitle = computed(() => {
    const count = this.sheet()?.activeWithoutCode ?? 0;
    return count === 1
      ? '1 active worker has no badge code'
      : `${count} active workers have no badge code`;
  });

  constructor() {
    effect(() => {
      const ids = this.chosen();
      untracked(() => void this.load(ids));
    });
  }

  async load(ids: readonly string[] = this.chosen()): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      this.sheet.set(await firstValueFrom(this.api.badges(ids)));
    } catch (failure) {
      this.error.set(asApiError(failure));
    } finally {
      this.loading.set(false);
    }
  }

  protected async search(event: Event): Promise<void> {
    event.preventDefault();
    const term = this.term().trim();
    if (term === '' || this.searching()) return;
    this.searching.set(true);
    try {
      const results = await firstValueFrom(this.api.findWorkers(term));
      this.found.set(results.filter((worker) => worker.status === 'Active'));
      this.lastTerm.set(term);
      this.searched.set(true);
    } catch (failure) {
      this.error.set(asApiError(failure));
    } finally {
      this.searching.set(false);
    }
  }

  protected add(worker: WorkerOption): void {
    void this.choose([...this.chosen(), worker.id]);
  }

  protected remove(workerId: string): void {
    void this.choose(this.chosen().filter((id) => id !== workerId));
  }

  protected clear(): void {
    void this.choose([]);
  }

  protected async issueMissing(): Promise<void> {
    if (this.issuing()) return;
    this.issuing.set(true);
    this.error.set(null);
    try {
      const result = await firstValueFrom(this.api.issueMissing());
      this.toast.success(
        result.issued === 1 ? 'Issued 1 badge code.' : `Issued ${result.issued} badge codes.`,
      );
      await this.choose([...new Set([...result.workerIds, ...this.chosen()])]);
    } catch (failure) {
      this.error.set(asApiError(failure));
    } finally {
      this.issuing.set(false);
    }
  }

  protected print(): void {
    globalThis.print?.();
  }

  private async choose(ids: readonly string[]): Promise<void> {
    await this.router.navigate([], {
      queryParams: { workers: ids.length ? ids.slice(0, MAXIMUM_BADGES).join(',') : null },
      queryParamsHandling: 'merge',
    });
  }
}
