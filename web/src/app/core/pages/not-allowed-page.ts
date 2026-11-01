import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Button } from '../../shared/ui/button/button';
import { EmptyState } from '../../shared/ui/empty-state/empty-state';
import { PageHeader } from '../../shared/ui/page-header/page-header';

@Component({
  selector: 'app-not-allowed-page',
  imports: [PageHeader, EmptyState, Button, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-page-header title="Not allowed" />
    <app-empty-state
      icon="lock"
      title="You don't have access to this page"
      message="If you need it, ask an administrator to change your role."
    >
      <a appButton variant="primary" routerLink="/projects">Go to projects</a>
    </app-empty-state>
  `,
})
export class NotAllowedPage {}
