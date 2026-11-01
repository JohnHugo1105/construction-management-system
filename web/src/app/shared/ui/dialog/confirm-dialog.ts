import { ChangeDetectionStrategy, Component, input, output, viewChild } from '@angular/core';
import { Button } from '../button/button';
import { Dialog } from './dialog';

/**
 * A question that must be answered before going on. State the pending action as a question ("Archive this project?"), say
 * what will happen in the message, and name the confirm button after the result ("Archive project"). Focus starts on Cancel,
 * the safe choice. Create it with `@if`; it emits `confirmed` or `cancelled` and the parent then removes it.
 */
@Component({
  selector: 'app-confirm-dialog',
  imports: [Dialog, Button],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-dialog [title]="title()" [locked]="busy()" (closed)="cancelled.emit()">
      <p class="confirm-message">{{ message() }}</p>
      <ng-content />
      <div dialogActions class="dialog-actions">
        <button
          appButton
          type="button"
          data-autofocus
          [attr.disabled]="busy() ? '' : null"
          (click)="cancel()"
        >
          {{ cancelLabel() }}
        </button>
        <button
          appButton
          type="button"
          [variant]="tone() === 'danger' ? 'danger' : 'primary'"
          [busy]="busy()"
          (click)="confirm()"
        >
          {{ confirmLabel() }}
        </button>
      </div>
    </app-dialog>
  `,
})
export class ConfirmDialog {
  readonly title = input.required<string>();
  readonly message = input.required<string>();
  readonly confirmLabel = input.required<string>();
  readonly cancelLabel = input('Cancel');
  readonly tone = input<'primary' | 'danger'>('primary');
  /** True while the confirmed action is running: the dialog stays open and cannot be dismissed. */
  readonly busy = input(false);

  readonly confirmed = output<void>();
  readonly cancelled = output<void>();

  private readonly dialog = viewChild.required(Dialog);

  protected cancel(): void {
    this.dialog().dismiss();
  }

  protected confirm(): void {
    if (this.busy()) return;
    this.confirmed.emit();
  }
}
