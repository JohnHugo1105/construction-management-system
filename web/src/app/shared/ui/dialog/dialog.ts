import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  afterNextRender,
  inject,
  input,
  output,
  viewChild,
} from '@angular/core';
import { Button } from '../button/button';
import { Icon } from '../icon/icon';

const FOCUSABLE =
  'a[href], button:not([disabled]), input:not([disabled]):not([type="hidden"]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])';

let nextDialogId = 0;

/**
 * A modal dialog on the native `<dialog>` element, which makes the rest of the page inert and handles the top layer. On top of
 * that it moves focus into the dialog when it opens (to the element marked `data-autofocus`, else the first control), keeps Tab
 * inside, closes on Escape or a click outside, and puts focus back where it was when it closes.
 *
 * Create it with `@if` when it is needed; it opens itself and emits `closed` however it is dismissed, so the parent turns its
 * flag off:
 *
 *     @if (confirming()) {
 *       <app-dialog title="Archive project" (closed)="confirming.set(false)">
 *         ...body...
 *         <div dialogActions class="dialog-actions"> ...buttons... </div>
 *       </app-dialog>
 *     }
 *
 * The body scrolls; the title and the actions stay in view. Set `locked` while a request is running so it cannot be dismissed.
 */
@Component({
  selector: 'app-dialog',
  imports: [Button, Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <dialog
      #dialog
      class="dialog"
      [class.dialog--sheet]="variant() === 'sheet'"
      [attr.aria-labelledby]="titleId"
      (cancel)="handleCancel($event)"
      (close)="handleClose()"
      (click)="handleClick($event)"
      (keydown)="handleKeydown($event)"
    >
      <div class="dialog__panel">
        <header class="dialog__header">
          <h2 class="dialog__title" [id]="titleId">{{ title() }}</h2>
          <button
            appButton
            variant="ghost"
            [icon]="true"
            type="button"
            aria-label="Close"
            (click)="dismiss()"
          >
            <app-icon name="close" />
          </button>
        </header>
        <div class="dialog__body"><ng-content /></div>
        <footer class="dialog__footer"><ng-content select="[dialogActions]" /></footer>
      </div>
    </dialog>
  `,
  styles: `
    :host {
      display: contents;
    }

    .dialog {
      width: min(34rem, calc(100vw - 2 * var(--space-4)));
      max-width: none;
      padding: 0;
      border: 0;
      border-radius: var(--radius-lg);
      background: var(--color-surface);
      color: var(--color-text);
      box-shadow: var(--shadow-3);
      overflow: clip;
    }

    .dialog::backdrop {
      background: var(--color-overlay);
    }

    .dialog__panel {
      display: flex;
      flex-direction: column;
      max-height: min(88dvh, 44rem);
    }

    .dialog__header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: var(--space-3);
      padding: var(--space-3) var(--space-3) 0 var(--space-6);
    }

    .dialog__title {
      font-size: var(--text-lg);
    }

    .dialog__body {
      flex: 1 1 auto;
      padding: var(--space-3) var(--space-6) var(--space-4);
      overflow-y: auto;
      overscroll-behavior: contain;
    }

    .dialog__footer {
      padding: var(--space-4) var(--space-6);
      border-top: 1px solid var(--color-border);
    }

    .dialog__footer:empty {
      display: none;
    }

    .dialog--sheet {
      width: 100%;
      margin: auto 0 0;
      border-radius: var(--radius-lg) var(--radius-lg) 0 0;
    }

    .dialog--sheet .dialog__panel {
      padding-bottom: var(--safe-bottom);
    }
  `,
})
export class Dialog {
  readonly title = input.required<string>();
  /** "sheet" slides up from the bottom edge, for menus on a phone. */
  readonly variant = input<'dialog' | 'sheet'>('dialog');
  /** While true the dialog cannot be dismissed (a request is running). */
  readonly locked = input(false);
  /** Emitted once, however the dialog was dismissed. */
  readonly closed = output<void>();

  protected readonly titleId = `dialog-title-${++nextDialogId}`;

  private readonly dialogElement = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');
  private opener: HTMLElement | null = null;
  private element: HTMLDialogElement | null = null;
  private announcedClose = false;
  private destroyed = false;

  constructor() {
    afterNextRender(() => this.open());
    inject(DestroyRef).onDestroy(() => this.teardown());
  }

  /** Closes the dialog the way Escape does. Does nothing while locked. */
  dismiss(): void {
    if (this.locked()) return;
    this.dialogElement().nativeElement.close();
  }

  protected handleCancel(event: Event): void {
    // Escape: close it ourselves so every browser behaves the same.
    event.preventDefault();
    this.dismiss();
  }

  protected handleClose(): void {
    if (this.announcedClose || this.destroyed) return;
    this.announcedClose = true;
    this.closed.emit();
  }

  protected handleClick(event: MouseEvent): void {
    // The panel fills the dialog, so a click that lands on the dialog itself is a click on the backdrop.
    if (event.target === this.dialogElement().nativeElement) this.dismiss();
  }

  protected handleKeydown(event: KeyboardEvent): void {
    if (event.key !== 'Tab') return;
    const dialog = this.dialogElement().nativeElement;
    const focusable = Array.from(dialog.querySelectorAll<HTMLElement>(FOCUSABLE));
    if (focusable.length === 0) {
      event.preventDefault();
      return;
    }
    const first = focusable[0]!;
    const last = focusable[focusable.length - 1]!;
    const active = document.activeElement;
    if (event.shiftKey && (active === first || active === dialog)) {
      event.preventDefault();
      last.focus();
    } else if (!event.shiftKey && active === last) {
      event.preventDefault();
      first.focus();
    }
  }

  private open(): void {
    const dialog = this.dialogElement().nativeElement;
    this.element = dialog;
    const active = document.activeElement;
    this.opener = active instanceof HTMLElement && active !== document.body ? active : null;
    if (!dialog.open) dialog.showModal();
    this.focusFirst(dialog);
  }

  private focusFirst(dialog: HTMLDialogElement): void {
    const preferred = dialog.querySelector<HTMLElement>('[data-autofocus]');
    if (preferred) {
      preferred.focus();
      return;
    }
    const inBody = dialog
      .querySelector<HTMLElement>('.dialog__body')
      ?.querySelector<HTMLElement>(FOCUSABLE);
    const inFooter = dialog
      .querySelector<HTMLElement>('.dialog__footer')
      ?.querySelector<HTMLElement>(FOCUSABLE);
    (inBody ?? inFooter ?? dialog.querySelector<HTMLElement>(FOCUSABLE))?.focus();
  }

  private teardown(): void {
    this.destroyed = true;
    if (this.element?.open) this.element.close();
    if (this.opener?.isConnected) this.opener.focus();
  }
}
