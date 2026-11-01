import { ChangeDetectionStrategy, Component } from '@angular/core';
import { PageHeader } from '../../shared/ui/page-header/page-header';
import { EmptyState } from '../../shared/ui/empty-state/empty-state';

/** Placeholder until this page is built. */
@Component({
  selector: 'app-purchasing-page',
  imports: [PageHeader, EmptyState],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-page-header title="Purchasing" />
    <app-empty-state
      title="Purchasing"
      message="Purchase orders and the firms you buy from."
      icon="cart"
    />
  `,
})
export class PurchasingPage {}
