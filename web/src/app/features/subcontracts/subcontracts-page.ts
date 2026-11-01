import { ChangeDetectionStrategy, Component } from '@angular/core';
import { PageHeader } from '../../shared/ui/page-header/page-header';
import { EmptyState } from '../../shared/ui/empty-state/empty-state';

/** Placeholder until this page is built. */
@Component({
  selector: 'app-subcontracts-page',
  imports: [PageHeader, EmptyState],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-page-header title="Subcontracts" />
    <app-empty-state
      title="Subcontracts"
      message="Subcontracts, payment certificates and payments."
      icon="contract"
    />
  `,
})
export class SubcontractsPage {}
