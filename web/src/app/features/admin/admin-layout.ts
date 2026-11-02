import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { Permission } from '../../core/session/permissions';
import { SessionState } from '../../core/session/session-state';
import { TabLink, TabNav } from '../../shared/ui/tab-nav/tab-nav';

/** The frame of the Administration pages: links between the ones the person may use. With only one, there is nothing to switch. */
@Component({
  selector: 'app-admin-layout',
  imports: [RouterOutlet, TabNav],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="stack">
      @if (tabs().length > 1) {
        <app-tab-nav label="Administration" [tabs]="tabs()" />
      }
      <router-outlet />
    </div>
  `,
})
export class AdminLayout {
  private readonly session = inject(SessionState);

  protected readonly tabs = computed<TabLink[]>(() => {
    const tabs: TabLink[] = [];
    if (this.session.can(Permission.usersManage))
      tabs.push({ label: 'Users', link: ['/admin', 'users'] });
    if (this.session.can(Permission.settingsManage))
      tabs.push({ label: 'Company settings', link: ['/admin', 'settings'] });
    if (this.session.can(Permission.settingsManage))
      tabs.push({ label: 'Time clocks', link: ['/admin', 'time-clocks'] });
    if (this.session.can(Permission.auditRead))
      tabs.push({ label: 'Activity log', link: ['/admin', 'audit'] });
    return tabs;
  });
}
