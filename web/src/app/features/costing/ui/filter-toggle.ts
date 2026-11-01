import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { Button } from '../../../shared/ui/button/button';
import { Icon } from '../../../shared/ui/icon/icon';

/**
 * The one button that opens and closes a list's filters, so they stay out of the way until wanted. It says how many filters are
 * in use ("Filters (2)") only when there are any, and tells screen readers whether the panel it controls is open.
 */
@Component({
  selector: 'app-filter-toggle',
  imports: [Button, Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <button
      appButton
      type="button"
      [attr.aria-expanded]="open()"
      [attr.aria-controls]="controls()"
      (click)="toggled.emit()"
    >
      <app-icon name="sliders" [size]="18" />
      {{ activeCount() > 0 ? 'Filters (' + activeCount() + ')' : 'Filters' }}
    </button>
  `,
  styles: `
    :host {
      display: block;
    }
  `,
})
export class FilterToggle {
  readonly open = input.required<boolean>();
  readonly activeCount = input(0);
  /** The id of the panel this button opens. */
  readonly controls = input.required<string>();

  readonly toggled = output<void>();
}
