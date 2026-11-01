import { ChangeDetectionStrategy, Component } from '@angular/core';
import { EmptyState } from '../../shared/ui/empty-state/empty-state';
import { PageHeader } from '../../shared/ui/page-header/page-header';

/** STUB. Replace this component's contents; keep the page-header (it puts the title in the shell header). */
@Component({
  selector: 'app-stock-page',
  imports: [PageHeader, EmptyState],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-page-header title="Stock" />
    <app-empty-state icon="clock" title="Stock" message="This part is being built next." />
  `,
})
export class StockPage {}
