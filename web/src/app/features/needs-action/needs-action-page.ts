import { ChangeDetectionStrategy, Component, DestroyRef, computed, inject } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { tap } from 'rxjs';
import { asApiError } from '../../core/api/api-error';
import { SessionState } from '../../core/session/session-state';
import { ApiErrorAlert } from '../../shared/ui/api-error-alert/api-error-alert';
import { Button } from '../../shared/ui/button/button';
import { EmptyState } from '../../shared/ui/empty-state/empty-state';
import { PageHeader } from '../../shared/ui/page-header/page-header';
import { Skeleton } from '../../shared/ui/skeleton/skeleton';
import { StatusChip } from '../../shared/ui/status-chip/status-chip';
import { NeedsActionApi } from './needs-action-api';
import { presentGroups, totalOf } from './needs-action-presentation';
import { NeedsActionState } from './needs-action-state';

/**
 * What waits on the signed-in person: one card per kind of waiting work (only the kinds their role acts on, and only when something
 * waits), most urgent first. Each row says what it is, why it waits and leads to one place, the record's own page, where the decision is
 * made: nothing is approved or issued from here. Reading the page also sets the navigation badge, so the two always agree.
 */
@Component({
  selector: 'app-needs-action-page',
  imports: [RouterLink, PageHeader, Button, StatusChip, EmptyState, Skeleton, ApiErrorAlert],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './needs-action-page.html',
  styleUrl: './needs-action-page.scss',
})
export class NeedsActionPage {
  private readonly api = inject(NeedsActionApi);
  private readonly badge = inject(NeedsActionState);
  private readonly session = inject(SessionState);

  private readonly resource = rxResource({
    stream: () => this.api.get().pipe(tap((result) => this.badge.setTotal(totalOf(result.groups)))),
  });

  constructor() {
    // A tab that comes back to the front reloads this page, which also sets the badge: the page and the badge never disagree.
    this.badge.reloadPageWhenVisible(() => this.reload(), inject(DestroyRef));
  }

  protected readonly loading = computed(
    () => this.resource.isLoading() && !this.resource.hasValue(),
  );
  protected readonly error = computed(() => {
    const failure = this.resource.error();
    return failure ? asApiError(failure) : null;
  });

  protected readonly groups = computed(() =>
    this.resource.hasValue()
      ? presentGroups(this.resource.value().groups, {
          can: (permission) => this.session.can(permission),
          currencyCode: this.session.currencyCode(),
        })
      : [],
  );

  protected readonly empty = computed(
    () => this.resource.hasValue() && !this.error() && this.groups().length === 0,
  );

  protected reload(): void {
    this.resource.reload();
  }
}
