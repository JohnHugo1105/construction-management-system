import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { Icon, IconName } from '../icon/icon';

export type ChipTone = 'neutral' | 'success' | 'warning' | 'danger' | 'info' | 'unbudgeted';

/** What a budget line's health looks like, as one place both the list and the page read: tone, icon and words together. */
export const BUDGET_HEALTH = {
  'on-track': { tone: 'success', icon: 'check-circle', label: 'On track' },
  'near-limit': { tone: 'warning', icon: 'warning', label: 'Near the limit' },
  'over-budget': { tone: 'danger', icon: 'error', label: 'Over budget' },
  unbudgeted: { tone: 'unbudgeted', icon: 'minus-circle', label: 'No budget' },
} as const satisfies Record<string, { tone: ChipTone; icon: IconName; label: string }>;

/**
 * A state shown as an icon plus words, never colour alone: `<app-status-chip label="Active" tone="success" icon="play" />`.
 * For budget health use the values in BUDGET_HEALTH so the dashboard and a project page always agree.
 */
@Component({
  selector: 'app-status-chip',
  imports: [Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <span [class]="'chip chip--' + tone()">
      <app-icon [name]="icon()" [size]="14" />
      {{ label() }}
    </span>
  `,
})
export class StatusChip {
  readonly label = input.required<string>();
  readonly tone = input<ChipTone>('neutral');
  readonly icon = input.required<IconName>();
}
