import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { Icon, IconName } from '../icon/icon';

export type AlertTone = 'info' | 'success' | 'warning' | 'danger';

const TONE_ICONS: Record<AlertTone, IconName> = {
  info: 'info',
  success: 'check-circle',
  warning: 'warning',
  danger: 'error',
};

/**
 * A message that stays on the page. Colour is never the only signal: every tone has its own icon and the title says what
 * happened. Put buttons in a child marked `alertActions`. Danger alerts are announced at once; others politely.
 */
@Component({
  selector: 'app-alert',
  imports: [Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div [class]="'alert alert--' + tone()" [attr.role]="role()">
      <app-icon [name]="icon()" />
      <div class="alert__body">
        @if (title()) {
          <p class="alert__title">{{ title() }}</p>
        }
        <div class="alert__text"><ng-content /></div>
        <div class="alert__actions"><ng-content select="[alertActions]" /></div>
      </div>
    </div>
  `,
  styles: `
    :host {
      display: block;
    }

    .alert__text:empty,
    .alert__actions:empty {
      display: none;
    }
  `,
})
export class Alert {
  readonly tone = input<AlertTone>('info');
  readonly title = input('');

  protected readonly icon = computed(() => TONE_ICONS[this.tone()]);
  protected readonly role = computed(() => (this.tone() === 'danger' ? 'alert' : 'status'));
}
