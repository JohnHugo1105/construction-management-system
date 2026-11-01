import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  inject,
  input,
  linkedSignal,
  output,
} from '@angular/core';
import { Icon } from '../icon/icon';

/**
 * A search field that reports what was typed after a short pause, so a list does not reload on every key. Enter searches at
 * once. `value` is the current search (from the address bar); the box follows it when it changes from outside.
 */
@Component({
  selector: 'app-search-box',
  imports: [Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="search" role="search">
      <app-icon class="search__icon" name="search" [size]="18" />
      <input
        class="input search__input"
        type="search"
        autocomplete="off"
        enterkeyhint="search"
        [attr.aria-label]="label()"
        [attr.placeholder]="placeholder() || null"
        [value]="text()"
        (input)="handleInput($event)"
        (keydown.enter)="flush()"
      />
    </div>
  `,
  styles: `
    :host {
      display: block;
    }

    .search {
      position: relative;
    }

    .search__icon {
      position: absolute;
      top: 50%;
      left: var(--space-3);
      transform: translateY(-50%);
      color: var(--color-text-muted);
      pointer-events: none;
    }

    .search__input {
      padding-left: 2.5rem;
      padding-right: var(--space-2);
    }
  `,
})
export class SearchBox {
  readonly label = input.required<string>();
  readonly placeholder = input('');
  readonly value = input('');
  /** How long to wait after the last key, in milliseconds. */
  readonly delay = input(300);

  readonly searched = output<string>();

  protected readonly text = linkedSignal(() => this.value());
  private timer: ReturnType<typeof setTimeout> | undefined;

  constructor() {
    inject(DestroyRef).onDestroy(() => clearTimeout(this.timer));
  }

  protected handleInput(event: Event): void {
    this.text.set((event.target as HTMLInputElement).value);
    clearTimeout(this.timer);
    this.timer = setTimeout(() => this.flush(), this.delay());
  }

  protected flush(): void {
    clearTimeout(this.timer);
    const text = this.text().trim();
    if (text !== this.value().trim()) this.searched.emit(text);
  }
}
