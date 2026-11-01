import { ChangeDetectionStrategy, Component } from '@angular/core';
import { EmptyState } from '../../../shared/ui/empty-state/empty-state';

/** Placeholder until this tab is built; its slice replaces the whole file and keeps the class name and selector. */
@Component({
  selector: 'app-project-billing-tab',
  imports: [EmptyState],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<app-empty-state
    title="Billing"
    message="Bills to the client and what has been collected."
    icon="receipt"
  />`,
})
export class ProjectBillingTab {}
