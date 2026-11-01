import { ChangeDetectionStrategy, Component } from '@angular/core';
import { EmptyState } from '../../../shared/ui/empty-state/empty-state';

/** Placeholder until this tab is built; its slice replaces the whole file and keeps the class name and selector. */
@Component({
  selector: 'app-project-changes-tab',
  imports: [EmptyState],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<app-empty-state
    title="Changes"
    message="Change orders and the budget baseline."
    icon="info"
  />`,
})
export class ProjectChangesTab {}
