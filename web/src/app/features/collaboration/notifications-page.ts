import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  linkedSignal,
  signal,
} from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { ApiError, asApiError } from '../../core/api/api-error';
import { DEFAULT_PAGE_SIZE, PagedResult } from '../../core/api/paged-result';
import { SessionState } from '../../core/session/session-state';
import { injectListQuery, listField } from '../../shared/list-query/list-query';
import { formatDateTime } from '../../shared/presentation/business-time';
import { ApiErrorAlert } from '../../shared/ui/api-error-alert/api-error-alert';
import { Button } from '../../shared/ui/button/button';
import { EmptyState } from '../../shared/ui/empty-state/empty-state';
import { Icon } from '../../shared/ui/icon/icon';
import { PageHeader } from '../../shared/ui/page-header/page-header';
import { Pager } from '../../shared/ui/pager/pager';
import { Skeleton } from '../../shared/ui/skeleton/skeleton';
import { CollaborationApi } from './collaboration-api';
import { notificationIcon, notificationLink } from './collaboration-presentation';
import { AppNotification } from './collaboration.models';
import { NotificationBellState } from './notification-bell-state';

type Show = 'all' | 'unread';

/**
 * What happened on the work you take part in: tasks given to you, your tasks done, mentions, and your drafts decided. Newest first;
 * unread ones say "New" and are in bold. Tapping one opens the record and marks it read. Shown in the app only: no email is sent (the
 * company has not set up email delivery). "Needs your action" is where waiting work is; this page says what happened.
 */
@Component({
  selector: 'app-notifications-page',
  imports: [RouterLink, PageHeader, ApiErrorAlert, Button, EmptyState, Icon, Pager, Skeleton],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-page-header title="Notifications" />

    <div class="stack">
      @if (hasAny()) {
        <div class="toolbar">
          <div class="filter-chips" role="group" aria-label="Which notifications">
            <button
              type="button"
              class="filter-chip"
              [attr.aria-pressed]="query.values().show === 'all'"
              (click)="query.set({ show: 'all' })"
            >
              All
            </button>
            <button
              type="button"
              class="filter-chip"
              [attr.aria-pressed]="query.values().show === 'unread'"
              (click)="query.set({ show: 'unread' })"
            >
              Unread
            </button>
          </div>
          @if (bell.unread() > 0) {
            <button appButton type="button" [busy]="markingAll()" (click)="markAll()">
              Mark all as read
            </button>
          }
        </div>
      }

      @if (actionError(); as problem) {
        <app-api-error-alert [error]="problem" />
      }
      @if (error(); as problem) {
        <app-api-error-alert
          [error]="problem"
          [allowRetry]="true"
          reloadLabel="Try again"
          (reload)="reload()"
        />
      }

      @if (loadingFirst()) {
        <app-skeleton variant="card" [lines]="3" label="Loading notifications" />
      } @else if (rows().length > 0) {
        <ul class="list" [attr.aria-busy]="refreshing()">
          @for (row of rows(); track row.id) {
            <li class="item" [class.item--unread]="row.unread">
              <app-icon class="item__icon" [name]="row.icon" />
              <div class="item__body">
                <a
                  class="item__message row-link--stretch"
                  [routerLink]="row.link"
                  [queryParams]="row.queryParams"
                  (click)="opened(row.source)"
                  >{{ row.message }}</a
                >
                <p class="item__meta">
                  @if (row.unread) {
                    <span class="item__new">New</span>
                  }
                  <span>{{ row.when }}</span>
                </p>
              </div>
            </li>
          }
        </ul>
        <app-pager
          [page]="query.values().page"
          [pageSize]="pageSize"
          [total]="total()"
          noun="notifications"
          (pageChange)="query.setPage($event)"
        />
      } @else if (!error()) {
        @if (query.values().show === 'unread') {
          <app-empty-state
            icon="check-circle"
            title="You're all caught up"
            message="Nothing unread."
          >
            <button appButton type="button" (click)="query.set({ show: 'all' })">Show all</button>
          </app-empty-state>
        } @else {
          <app-empty-state
            icon="bell"
            title="No notifications yet"
            message="You are told here when someone gives you a task, mentions you, finishes a task you gave, or decides a draft you prepared."
          />
        }
      }
    </div>
  `,
  styles: `
    :host {
      display: block;
    }

    .toolbar {
      display: flex;
      flex-wrap: wrap;
      gap: var(--space-2) var(--space-3);
      align-items: center;
      justify-content: space-between;
    }

    .toolbar .filter-chips {
      flex: 1 1 auto;
      min-width: 0;
    }

    .list {
      display: grid;
      margin: 0;
      padding: 0;
      list-style: none;
      border: 1px solid var(--color-border);
      border-radius: var(--radius-lg);
      background: var(--color-surface);
    }

    .item {
      position: relative;
      display: flex;
      align-items: flex-start;
      gap: var(--space-3);
      min-height: var(--touch-target);
      padding: var(--space-3) var(--space-4);
      border-bottom: 1px solid var(--color-border);
    }

    .item:last-child {
      border-bottom: 0;
    }

    .item:hover {
      background: var(--color-surface-2);
    }

    .item:has(.item__message:focus-visible) {
      outline: var(--focus-ring-width) solid var(--color-focus);
      outline-offset: -2px;
    }

    .item__icon {
      margin-top: 2px;
      color: var(--color-text-muted);
    }

    .item__body {
      display: grid;
      gap: 2px;
      min-width: 0;
    }

    .item__message {
      color: var(--color-text);
      text-decoration: none;
      overflow-wrap: anywhere;
    }

    .item__message:focus-visible {
      outline: none;
    }

    .item--unread .item__message {
      font-weight: 650;
    }

    .item--unread .item__icon {
      color: var(--color-brand-text);
    }

    .item__meta {
      display: flex;
      flex-wrap: wrap;
      gap: 0 var(--space-2);
      font-size: var(--text-sm);
      color: var(--color-text-muted);
    }

    .item__new {
      color: var(--color-brand-text);
      font-weight: 650;
    }
  `,
})
export class NotificationsPage {
  private readonly api = inject(CollaborationApi);
  private readonly session = inject(SessionState);
  protected readonly bell = inject(NotificationBellState);

  protected readonly query = injectListQuery({
    show: listField.choice<Show>(['all', 'unread'], 'all'),
  });
  protected readonly pageSize = DEFAULT_PAGE_SIZE;

  private readonly resource = rxResource({
    params: () => this.query.values(),
    stream: ({ params }) =>
      this.api.notifications(params.show === 'unread', params.page, DEFAULT_PAGE_SIZE),
  });

  private readonly result = linkedSignal<
    PagedResult<AppNotification> | undefined,
    PagedResult<AppNotification> | undefined
  >({
    source: () => (this.resource.hasValue() ? this.resource.value() : undefined),
    computation: (value, previous) => value ?? previous?.value,
  });

  /** Read ones the person opened from this page, shown as read at once without reloading the list. */
  private readonly readHere = signal<ReadonlySet<string>>(new Set());

  protected readonly markingAll = signal(false);
  protected readonly actionError = signal<ApiError | null>(null);

  protected readonly total = computed(() => this.result()?.total ?? 0);
  protected readonly loadingFirst = computed(
    () => this.resource.isLoading() && this.result() === undefined,
  );
  protected readonly refreshing = computed(
    () => this.resource.isLoading() && this.result() !== undefined,
  );
  protected readonly error = computed(() => {
    const failure = this.resource.error();
    return failure ? asApiError(failure) : null;
  });

  /** Something has ever been notified, so the filter and "Mark all as read" have a reason to show. */
  protected readonly hasAny = computed(
    () => this.total() > 0 || this.query.values().show === 'unread',
  );

  protected readonly rows = computed(() => {
    const zone = this.session.timeZoneId();
    const readHere = this.readHere();
    return (this.result()?.items ?? []).map((item) => {
      const target = notificationLink(item);
      return {
        id: item.id,
        source: item,
        message: item.message,
        icon: notificationIcon(item.kind),
        unread: !item.isRead && !readHere.has(item.id),
        when: formatDateTime(item.createdAtUtc, zone),
        link: target.link,
        queryParams: target.queryParams,
      };
    });
  });

  constructor() {
    // Opening the page reads the bell's number again, so the two agree.
    this.bell.refresh();
  }

  protected reload(): void {
    this.resource.reload();
  }

  /** Tapping one goes to the record and marks it read on the way. */
  protected opened(notification: AppNotification): void {
    if (notification.isRead || this.readHere().has(notification.id)) return;
    this.readHere.update((ids) => new Set([...ids, notification.id]));
    this.bell.markRead(notification.id);
  }

  protected async markAll(): Promise<void> {
    if (this.markingAll()) return;
    this.markingAll.set(true);
    this.actionError.set(null);
    try {
      const result = await firstValueFrom(this.api.markAllRead());
      this.bell.setUnread(result.unread);
      this.resource.reload();
    } catch (failure) {
      this.actionError.set(asApiError(failure));
    } finally {
      this.markingAll.set(false);
    }
  }
}
