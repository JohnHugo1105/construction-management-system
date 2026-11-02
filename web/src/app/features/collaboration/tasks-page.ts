import { ChangeDetectionStrategy, Component } from '@angular/core';
import { PageHeader } from '../../shared/ui/page-header/page-header';

/** Placeholder until the collaboration slice lands. */
@Component({
  selector: 'app-tasks-page',
  imports: [PageHeader],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<app-page-header title="Tasks" description="Work assigned to you and by you." />`,
})
export class TasksPage {}
