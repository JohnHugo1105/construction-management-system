import { Directive, input } from '@angular/core';

export type ButtonVariant = 'primary' | 'secondary' | 'danger' | 'ghost';

/**
 * The shared button look for `<button>` and `<a>`: `<button appButton variant="primary">Save project</button>`.
 * One primary button per page. `busy` shows a spinner on the button itself; the handler must still ignore a second click.
 */
@Directive({
  selector: 'button[appButton], a[appButton]',
  host: {
    class: 'btn',
    '[class.btn--primary]': "variant() === 'primary'",
    '[class.btn--danger]': "variant() === 'danger'",
    '[class.btn--ghost]': "variant() === 'ghost'",
    '[class.btn--icon]': 'icon()',
    '[class.btn--block]': 'block()',
    '[attr.aria-busy]': "busy() ? 'true' : null",
  },
})
export class Button {
  readonly variant = input<ButtonVariant>('secondary');
  readonly busy = input(false);
  /** A square button holding only an icon (give it an aria-label). */
  readonly icon = input(false);
  readonly block = input(false);
}
