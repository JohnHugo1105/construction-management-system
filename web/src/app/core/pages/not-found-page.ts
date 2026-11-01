import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Button } from '../../shared/ui/button/button';
import { EmptyState } from '../../shared/ui/empty-state/empty-state';
import { PageHeader } from '../../shared/ui/page-header/page-header';

@Component({
  selector: 'app-not-found-page',
  imports: [PageHeader, EmptyState, Button, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-page-header title="Page not found" />
    <app-empty-state
      icon="search"
      title="We can't find that page"
      message="The address may be mistyped, or the page may have moved."
    >
      <a appButton variant="primary" routerLink="/projects">Go to projects</a>
    </app-empty-state>
  `,
})
export class NotFoundPage {}
