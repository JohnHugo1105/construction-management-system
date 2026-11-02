import { ChangeDetectionStrategy, Component } from '@angular/core';

/** Placeholder until the documents slice lands. */
@Component({
  selector: 'app-project-documents-tab',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<p class="text-muted">Project documents are coming here.</p>`,
})
export class ProjectDocumentsTab {}
