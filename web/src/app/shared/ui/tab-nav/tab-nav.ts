import { ChangeDetectionStrategy, Component, ElementRef, input, viewChild } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';

export interface TabLink {
  readonly label: string;
  /** An absolute link such as `['/projects', id, 'budget']`. */
  readonly link: readonly unknown[];
  /** Active only on exactly this link (use for the first tab, whose link is a prefix of the others). */
  readonly exact?: boolean;
}

/**
 * Tabs that are links to child routes, so each tab has its own address and the back button works. On a narrow screen the row
 * scrolls sideways rather than squeezing the labels. Pass only the tabs the person may use.
 */
@Component({
  selector: 'app-tab-nav',
  imports: [RouterLink, RouterLinkActive],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <nav class="tabs" #row [attr.aria-label]="label()">
      <ul class="tabs__list">
        @for (tab of tabs(); track tab.label) {
          <li>
            <a
              class="tab"
              [routerLink]="tab.link"
              routerLinkActive="tab--active"
              ariaCurrentWhenActive="page"
              [routerLinkActiveOptions]="{ exact: tab.exact ?? false }"
              (isActiveChange)="showWhenActive($event, anchor)"
              #anchor
            >
              {{ tab.label }}
            </a>
          </li>
        }
      </ul>
    </nav>
  `,
  styles: `
    :host {
      display: block;
    }

    .tabs {
      border-bottom: 1px solid var(--color-border);
      overflow-x: auto;
      scrollbar-width: none;
    }

    .tabs::-webkit-scrollbar {
      display: none;
    }

    .tabs__list {
      display: flex;
      margin: 0;
      padding: 0;
      list-style: none;
    }

    /* Narrow on a phone so five tabs fit without sideways scrolling; the row still scrolls if they ever do not. */
    .tab {
      display: flex;
      align-items: center;
      min-height: var(--touch-target);
      padding: 0 10px;
      border-bottom: 3px solid transparent;
      color: var(--color-text-muted);
      font-size: var(--text-sm);
      font-weight: 600;
      text-decoration: none;
      white-space: nowrap;
    }

    .tab:hover {
      color: var(--color-text);
    }

    .tab--active {
      border-bottom-color: var(--color-brand);
      color: var(--color-brand-text);
    }

    @media (min-width: 48rem) {
      .tab {
        padding: 0 var(--space-4);
      }
    }
  `,
})
export class TabNav {
  readonly label = input.required<string>();
  readonly tabs = input.required<readonly TabLink[]>();

  private readonly row = viewChild.required<ElementRef<HTMLElement>>('row');

  /**
   * With many tabs the row scrolls sideways on a phone, and the open tab could be off screen, leaving the person with no sign of where
   * they are. Bring the open tab to the middle of the row (the row only: the page does not move).
   */
  protected showWhenActive(active: boolean, anchor: HTMLElement): void {
    if (!active) return;
    const row = this.row().nativeElement;
    if (row.scrollWidth <= row.clientWidth) return;
    row.scrollLeft = Math.max(0, anchor.offsetLeft - (row.clientWidth - anchor.offsetWidth) / 2);
  }
}
