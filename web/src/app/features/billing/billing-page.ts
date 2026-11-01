import { ChangeDetectionStrategy, Component } from '@angular/core';
import { PageHeader } from '../../shared/ui/page-header/page-header';
import { EmptyState } from '../../shared/ui/empty-state/empty-state';

/** Placeholder until this page is built. */
@Component({
  selector: 'app-billing-page',
  imports: [PageHeader, EmptyState],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-page-header title="Billing" />
    <app-empty-state
      title="Billing"
      message="What you have billed clients and what is still to collect."
      icon="receipt"
    />
  `,
})
export class BillingPage {}
