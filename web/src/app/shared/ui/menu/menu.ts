import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  Injector,
  afterNextRender,
  inject,
  input,
  signal,
  viewChild,
} from '@angular/core';
import { Button, ButtonVariant } from '../button/button';
import { Icon } from '../icon/icon';

let nextMenuId = 0;

/** The space kept between the list and the screen edge, in pixels. */
const EDGE = 8;

/**
 * A button that opens a short list of actions, built as a disclosure (not an ARIA menu): the button reports whether the list
 * is open, the actions are ordinary buttons, Escape closes it and returns focus to the button, and a click elsewhere closes it.
 * Arrow keys move between the actions. Fill it with `<button type="button" class="menu__item">` rows. Choosing an action
 * closes the menu first, so a dialog the action opens remembers the menu button as the place to return to.
 *
 * The list is positioned against the screen (not its parent), so a table or card that scrolls never clips it, and it is kept
 * inside the screen: it opens upward when there is no room below. It follows its button when the page scrolls or resizes.
 *
 *     <app-menu label="More" align="end">
 *       <button type="button" class="menu__item" (click)="archive()">Archive project</button>
 *     </app-menu>
 */
@Component({
  selector: 'app-menu',
  imports: [Button, Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    '(document:click)': 'handleDocumentClick($event)',
    '(window:resize)': 'follow()',
    '(window:scroll)': 'follow()',
    '(keydown.escape)': 'handleEscape()',
    '(focusout)': 'handleFocusOut($event)',
  },
  template: `
    <div class="menu">
      <button
        appButton
        #trigger
        type="button"
        [variant]="variant()"
        [icon]="iconOnly()"
        [attr.aria-expanded]="open()"
        [attr.aria-controls]="open() ? panelId : null"
        [attr.aria-label]="ariaLabel() || null"
        (click)="toggle()"
        (keydown.arrowdown)="openFromKeyboard($event)"
      >
        <ng-content select="[menuTrigger]">
          <span>{{ label() }}</span>
        </ng-content>
        @if (!iconOnly()) {
          <app-icon name="chevron-down" [size]="16" />
        }
      </button>
      @if (open()) {
        <div
          class="menu__panel"
          [id]="panelId"
          [style.top.px]="position()?.top ?? 0"
          [style.left.px]="position()?.left ?? 0"
          [style.visibility]="position() ? 'visible' : 'hidden'"
          (click)="handlePanelClick($event)"
          (keydown)="handlePanelKeydown($event)"
        >
          <ng-content />
        </div>
      }
    </div>
  `,
  styles: `
    :host {
      display: inline-block;
    }
  `,
})
export class Menu {
  readonly label = input('More');
  /** Needed when the trigger shows only an icon. */
  readonly ariaLabel = input('');
  readonly variant = input<ButtonVariant>('secondary');
  readonly iconOnly = input(false);
  /** Which edge of the button the list lines up with: "end" is the right edge. */
  readonly align = input<'start' | 'end'>('end');

  protected readonly open = signal(false);
  protected readonly position = signal<{ top: number; left: number } | null>(null);
  protected readonly panelId = `menu-${++nextMenuId}`;

  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly injector = inject(Injector);
  private readonly trigger = viewChild.required<ElementRef<HTMLButtonElement>>('trigger');

  protected toggle(): void {
    if (this.open()) this.close(false);
    else this.show();
  }

  protected openFromKeyboard(event: Event): void {
    event.preventDefault();
    this.show();
    afterNextRender(() => this.items()[0]?.focus(), { injector: this.injector });
  }

  protected handleEscape(): void {
    if (this.open()) this.close(true);
  }

  /** The screen changed under the list (scrolling, the browser bar, rotation): keep it next to its button, or close it if the button left the screen. */
  protected follow(): void {
    if (!this.open()) return;
    const anchor = this.trigger().nativeElement.getBoundingClientRect();
    if (anchor.bottom < 0 || anchor.top > window.innerHeight) this.close(false);
    else this.place();
  }

  protected handleFocusOut(event: FocusEvent): void {
    const next = event.relatedTarget;
    if (this.open() && next instanceof Node && !this.host.nativeElement.contains(next))
      this.close(false);
  }

  protected handleDocumentClick(event: Event): void {
    if (
      this.open() &&
      event.target instanceof Node &&
      !this.host.nativeElement.contains(event.target)
    )
      this.close(false);
  }

  protected handlePanelClick(event: Event): void {
    const target = event.target;
    if (target instanceof Element && target.closest('button, a')) this.close(true);
  }

  protected handlePanelKeydown(event: KeyboardEvent): void {
    const items = this.items();
    if (items.length === 0) return;
    const index = items.indexOf(document.activeElement as HTMLElement);
    let next = -1;
    if (event.key === 'ArrowDown') next = (index + 1) % items.length;
    else if (event.key === 'ArrowUp') next = (index - 1 + items.length) % items.length;
    else if (event.key === 'Home') next = 0;
    else if (event.key === 'End') next = items.length - 1;
    if (next >= 0) {
      event.preventDefault();
      items[next]!.focus();
    }
  }

  private show(): void {
    this.position.set(null);
    this.open.set(true);
    afterNextRender(() => this.place(), { injector: this.injector });
  }

  /** Puts the list under (or over) the button, inside the screen. It stays hidden until this has run. */
  private place(): void {
    const panel = this.host.nativeElement.querySelector<HTMLElement>('.menu__panel');
    if (!panel) return;
    const anchor = this.trigger().nativeElement.getBoundingClientRect();
    const width = panel.offsetWidth;
    const height = panel.offsetHeight;

    const wantedLeft = this.align() === 'end' ? anchor.right - width : anchor.left;
    const left = Math.min(
      Math.max(EDGE, wantedLeft),
      Math.max(EDGE, window.innerWidth - width - EDGE),
    );
    const below = anchor.bottom + 4;
    const top =
      below + height > window.innerHeight - EDGE ? Math.max(EDGE, anchor.top - height - 4) : below;
    this.position.set({ top, left });
  }

  private items(): HTMLElement[] {
    return Array.from(
      this.host.nativeElement.querySelectorAll<HTMLElement>('.menu__panel .menu__item'),
    );
  }

  private close(restoreFocus: boolean): void {
    this.open.set(false);
    if (restoreFocus) this.trigger().nativeElement.focus();
  }
}
