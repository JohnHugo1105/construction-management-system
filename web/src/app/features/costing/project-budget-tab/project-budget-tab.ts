import { ChangeDetectionStrategy, Component } from '@angular/core';
import { EmptyState } from '../../../shared/ui/empty-state/empty-state';

/**
 * STUB. The Budget tab of a project (route `/projects/:id/budget`, needs `costs.read`). Replace this component's contents in
 * place and keep the class name and path. Inject `ProjectContext` (features/projects/project-context) for the project's id, name,
 * sub-projects and `isArchived()`; do not render a page-header (the project page owns the title).
 */
@Component({
  selector: 'app-project-budget-tab',
  imports: [EmptyState],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<app-empty-state
    icon="clock"
    title="Budget"
    message="This part is being built next."
  />`,
})
export class ProjectBudgetTab {}
