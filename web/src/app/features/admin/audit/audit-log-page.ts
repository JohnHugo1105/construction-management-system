import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { SessionState } from '../../../core/session/session-state';
import { formatDate, formatDateTime } from '../../../shared/presentation/business-time';
import { ApiErrorAlert } from '../../../shared/ui/api-error-alert/api-error-alert';
import { Button } from '../../../shared/ui/button/button';
import { EmptyState } from '../../../shared/ui/empty-state/empty-state';
import { Control } from '../../../shared/ui/form-field/control';
import { FormField } from '../../../shared/ui/form-field/form-field';
import { Icon } from '../../../shared/ui/icon/icon';
import { PageHeader } from '../../../shared/ui/page-header/page-header';
import { Pager } from '../../../shared/ui/pager/pager';
import { SearchBox } from '../../../shared/ui/search-box/search-box';
import { Skeleton } from '../../../shared/ui/skeleton/skeleton';
import { Viewport } from '../../../shared/ui/viewport/viewport';
import { ALL_AREAS, AuditLogState } from './audit-log-state';
import { AUDIT_ENTITY_TYPES, entityTypeLabel } from './audit.models';

/** The activity log: when, who and what, newest first. Search is always there; the area and date filters fold away. */
@Component({
  selector: 'app-audit-log-page',
  imports: [
    PageHeader,
    SearchBox,
    Button,
    Icon,
    FormField,
    Control,
    Pager,
    EmptyState,
    Skeleton,
    ApiErrorAlert,
  ],
  providers: [AuditLogState],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './audit-log-page.html',
  styleUrl: './audit-log-page.scss',
})
export class AuditLogPage {
  protected readonly state = inject(AuditLogState);
  protected readonly viewport = inject(Viewport);
  private readonly session = inject(SessionState);

  protected readonly allAreas = ALL_AREAS;
  protected readonly areas = AUDIT_ENTITY_TYPES;

  /** Filters stay folded unless a shared link arrives with some already set. */
  protected readonly filtersOpen = signal(this.activeFilters().length > 0);

  protected readonly filterSummary = computed(() => this.activeFilters().join(', '));

  protected readonly rangeError = computed(() => {
    const { from, to } = this.state.query.values();
    return from && to && to < from ? "The end date can't be before the start date." : null;
  });

  protected readonly rows = computed(() => {
    const zone = this.session.timeZoneId();
    return this.state.items().map((entry) => ({
      id: entry.id,
      when: formatDateTime(entry.occurredAtUtc, zone),
      who: entry.userName,
      what: entry.summary,
      area: entityTypeLabel(entry.entityType),
    }));
  });

  protected setArea(event: Event): void {
    this.state.query.set({ area: (event.target as HTMLSelectElement).value });
  }

  protected setFrom(event: Event): void {
    this.state.query.set({ from: (event.target as HTMLInputElement).value });
  }

  protected setTo(event: Event): void {
    this.state.query.set({ to: (event.target as HTMLInputElement).value });
  }

  protected setSignIns(event: Event): void {
    this.state.query.set({ signIns: (event.target as HTMLInputElement).checked });
  }

  protected clearFilters(): void {
    this.state.query.set({ area: ALL_AREAS, from: '', to: '', signIns: false });
  }

  /** The filters in use, as short phrases: "Users", "from 1 Nov 2026", "to 3 Nov 2026". */
  private activeFilters(): string[] {
    const { area, from, to, signIns } = this.state.query.values();
    const phrases: string[] = [];
    if (area !== ALL_AREAS) phrases.push(entityTypeLabel(area));
    if (from) phrases.push(`from ${formatDate(from)}`);
    if (to) phrases.push(`to ${formatDate(to)}`);
    if (signIns) phrases.push('including sign-ins');
    return phrases;
  }
}
