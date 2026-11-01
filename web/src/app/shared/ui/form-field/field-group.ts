import { ChangeDetectionStrategy, Component } from '@angular/core';
import { Icon } from '../icon/icon';
import { FieldBase } from './field-base';

/**
 * A labelled group of choices (radio buttons or checkboxes) as a real fieldset, so a screen reader announces the group
 * name with each choice. Fill it with `.choice-list` rows.
 *
 *     <app-field-group label="Roles" [error]="rolesError()">
 *       <div class="choice-list"> <label class="choice"><input type="checkbox" ... /> ... </label> </div>
 *     </app-field-group>
 */
@Component({
  selector: 'app-field-group',
  imports: [Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <fieldset
      class="field-group"
      [attr.aria-describedby]="describedBy()"
      [attr.aria-invalid]="invalid() ? 'true' : null"
    >
      <legend class="field__legend">
        {{ label() }}
        @if (optional()) {
          <span class="field__optional">(optional)</span>
        }
      </legend>
      <ng-content />
      @if (hint()) {
        <p class="field__hint" [id]="hintId">{{ hint() }}</p>
      }
      @if (message(); as text) {
        <p class="field__error" [id]="errorId" role="alert">
          <app-icon name="error" [size]="16" />
          <span>{{ text }}</span>
        </p>
      }
    </fieldset>
  `,
  styles: `
    :host {
      display: block;
      min-width: 0;
    }

    legend {
      padding: 0;
      margin-bottom: var(--space-2);
    }
  `,
})
export class FieldGroup extends FieldBase {}
