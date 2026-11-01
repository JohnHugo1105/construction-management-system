import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { Permission } from '../../core/session/permissions';
import { SessionState } from '../../core/session/session-state';
import { TabLink, TabNav } from '../../shared/ui/tab-nav/tab-nav';

/**
 * The two lists of the Purchasing section: orders, and the firms they are placed with. The firms tab is shown only to people who
 * may read firms; a person with nothing to choose between sees no tabs at all.
 */
@Component({
  selector: 'app-purchasing-tabs',
  imports: [TabNav],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `@if (tabs().length > 1) {
    <app-tab-nav label="Purchasing" [tabs]="tabs()" />
  }`,
})
export class PurchasingTabs {
  private readonly session = inject(SessionState);

  protected readonly tabs = computed<readonly TabLink[]>(() => {
    const tabs: TabLink[] = [{ label: 'Purchase orders', link: ['/purchasing'], exact: true }];
    if (this.session.can(Permission.suppliersRead))
      tabs.push({ label: 'Suppliers', link: ['/purchasing', 'suppliers'] });
    return tabs;
  });
}
