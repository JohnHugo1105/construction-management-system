import { ChangeDetectionStrategy, Component } from '@angular/core';
import { Icon } from '../icon/icon';
import { FieldBase } from './field-base';

/**
 * A labelled field: label above, the control in the middle, a hint below, then the error. Put one input in it and mark the
 * input `appControl`; the label, hint and error are then tied to it for screen readers.
 *
 *     <app-form-field label="Project name" [control]="form.controls.name">
 *       <input appControl formControlName="name" autocomplete="off" />
 *     </app-form-field>
 */
@Component({
  selector: 'app-form-field',
  imports: [Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="field">
      <label class="field__label" [attr.for]="id">
        {{ label() }}
        @if (optional()) {
          <span class="field__optional">(optional)</span>
        }
      </label>
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
    </div>
  `,
  styles: `
    :host {
      display: block;
      min-width: 0;
    }
  `,
})
export class FormField extends FieldBase {}
