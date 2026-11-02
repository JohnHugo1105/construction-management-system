import { ChangeDetectionStrategy, Component } from '@angular/core';
import { PageHeader } from '../../shared/ui/page-header/page-header';

/** Placeholder until the collaboration slice lands. */
@Component({
  selector: 'app-notifications-page',
  imports: [PageHeader],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<app-page-header title="Notifications" description="What happened on the work you are part of." />`,
})
export class NotificationsPage {}
