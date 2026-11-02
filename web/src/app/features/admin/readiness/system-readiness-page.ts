import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { asApiError } from '../../../core/api/api-error';
import { ApiErrorAlert } from '../../../shared/ui/api-error-alert/api-error-alert';
import { PageHeader } from '../../../shared/ui/page-header/page-header';
import { Skeleton } from '../../../shared/ui/skeleton/skeleton';
import { ChipTone, StatusChip } from '../../../shared/ui/status-chip/status-chip';
import { IconName } from '../../../shared/ui/icon/icon';
import { CapabilitiesApi } from './capabilities-api';
import { CapabilityState } from './capabilities.models';

export interface StateLook {
  readonly label: string;
  readonly tone: ChipTone;
  readonly icon: IconName;
}

/** Each state in words, with its own icon so colour is never the only signal. */
export const STATE_LOOKS: Readonly<Record<CapabilityState, StateLook>> = {
  Available: { label: 'Working', tone: 'success', icon: 'check-circle' },
  NeedsSetup: { label: 'Not set up', tone: 'warning', icon: 'sliders' },
  Unavailable: { label: 'Switched off', tone: 'neutral', icon: 'minus-circle' },
  Failing: { label: 'Not working', tone: 'danger', icon: 'error' },
};

/**
 * Administration > System readiness: what the system depends on that may not be set up (file storage, virus scanning, email), each
 * with whether it works, what that means, and what people can do meanwhile. Read-only: the settings live on the server.
 */
@Component({
  selector: 'app-system-readiness-page',
  imports: [PageHeader, ApiErrorAlert, Skeleton, StatusChip],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-page-header
      title="System readiness"
      description="What still needs setting up on the server, and what people can do meanwhile."
    />
    @if (error(); as problem) {
      <app-api-error-alert
        [error]="problem"
        [allowRetry]="true"
        reloadLabel="Try again"
        (reload)="resource.reload()"
      />
    } @else if (items(); as list) {
      <ul class="card-list">
        @for (item of list; track item.key) {
          <li class="row-card">
            <div class="row-card__head">
              <h2 class="row-card__title">{{ item.name }}</h2>
              <app-status-chip [label]="item.look.label" [tone]="item.look.tone" [icon]="item.look.icon" />
            </div>
            <p>{{ item.message }}</p>
            @if (item.fallback) {
              <p class="text-small text-muted">Meanwhile: {{ item.fallback }}</p>
            }
          </li>
        }
      </ul>
    } @else {
      <app-skeleton variant="card" [lines]="3" label="Loading system readiness" />
    }
  `,
  styles: `
    :host {
      display: grid;
      gap: var(--space-4);
    }
  `,
})
export class SystemReadinessPage {
  private readonly api = inject(CapabilitiesApi);

  protected readonly resource = rxResource({ stream: () => this.api.list() });

  protected readonly items = computed(() =>
    this.resource.hasValue()
      ? this.resource.value().items.map((item) => ({ ...item, look: STATE_LOOKS[item.state] }))
      : null,
  );
  protected readonly error = computed(() => {
    const failure = this.resource.error();
    return failure ? asApiError(failure) : null;
  });
}
