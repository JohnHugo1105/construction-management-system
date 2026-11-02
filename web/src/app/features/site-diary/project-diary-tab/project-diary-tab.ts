import { ChangeDetectionStrategy, Component } from '@angular/core';

/** Placeholder until the site diary slice lands. */
@Component({
  selector: 'app-project-diary-tab',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<p class="text-muted">The site diary is coming here.</p>`,
})
export class ProjectDiaryTab {}
