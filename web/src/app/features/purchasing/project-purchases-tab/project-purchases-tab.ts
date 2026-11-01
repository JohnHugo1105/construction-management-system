import { ChangeDetectionStrategy, Component } from '@angular/core';
import { EmptyState } from '../../../shared/ui/empty-state/empty-state';

/** Placeholder until this tab is built; its slice replaces the whole file and keeps the class name and selector. */
@Component({
  selector: 'app-project-purchases-tab',
  imports: [EmptyState],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<app-empty-state
    title="Purchases"
    message="Purchase orders for this project."
    icon="cart"
  />`,
})
export class ProjectPurchasesTab {}
