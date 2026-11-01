import { ChangeDetectionStrategy, Component } from '@angular/core';
import { PageHeader } from '../../shared/ui/page-header/page-header';

/** Placeholder until the bonds slice lands. */
@Component({
  selector: 'app-bonds-page',
  imports: [PageHeader],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<app-page-header title="Bonds" description="The bonds posted and received on your projects." />`,
})
export class BondsPage {}
