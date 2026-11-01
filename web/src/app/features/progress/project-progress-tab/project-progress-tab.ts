import { ChangeDetectionStrategy, Component } from '@angular/core';
import { EmptyState } from '../../../shared/ui/empty-state/empty-state';

/** Placeholder until this tab is built; its slice replaces the whole file and keeps the class name and selector. */
@Component({
  selector: 'app-project-progress-tab',
  imports: [EmptyState],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<app-empty-state
    title="Progress"
    message="How far along each part of the project is."
    icon="info"
  />`,
})
export class ProjectProgressTab {}
