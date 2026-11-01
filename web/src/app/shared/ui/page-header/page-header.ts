import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  effect,
  inject,
  input,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { PageTitleState } from '../../../core/layout/page-title-state';
import { Icon } from '../icon/icon';

/**
 * The top of a page. The title goes up into the shell header (so the page itself starts with its content); what stays here
 * is an optional way back, one line of description, and the page's single primary action, projected as content:
 *
 *     <app-page-header title="Projects" description="Everything you are building.">
 *       <a appButton variant="primary" routerLink="/projects/new">New project</a>
 *     </app-page-header>
 *
 * Use it once per page, never inside a tab: a tab belongs to its page and keeps the page's title.
 */
@Component({
  selector: 'app-page-header',
  imports: [RouterLink, Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="page-header">
      @if (backLabel() && backLink()) {
        <a class="page-header__back" [routerLink]="backLink()">
          <app-icon name="arrow-left" [size]="18" />
          {{ backLabel() }}
        </a>
      }
      @if (description()) {
        <p class="page-header__description">{{ description() }}</p>
      }
      <div class="page-header__actions"><ng-content /></div>
    </div>
  `,
  styles: `
    :host {
      display: block;
    }

    .page-header {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      justify-content: space-between;
      gap: var(--space-2) var(--space-4);
      margin-bottom: var(--space-4);
    }

    .page-header:not(
      :has(.page-header__back, .page-header__description, .page-header__actions:not(:empty))
    ) {
      display: none;
    }

    .page-header__back {
      display: inline-flex;
      align-items: center;
      gap: var(--space-1);
      min-height: var(--touch-target);
      flex-basis: 100%;
      color: var(--color-brand-text);
      font-weight: 600;
      text-decoration: none;
    }

    .page-header__back:hover {
      text-decoration: underline;
    }

    .page-header__description {
      flex: 1 1 16rem;
      color: var(--color-text-muted);
    }

    .page-header__actions {
      display: flex;
      flex-wrap: wrap;
      gap: var(--space-2);
      margin-left: auto;
    }

    .page-header__actions:empty {
      display: none;
    }
  `,
})
export class PageHeader {
  readonly title = input.required<string>();
  readonly description = input('');
  /** A way back, for pages one level down: `backLabel="All projects" [backLink]="['/projects']"`. */
  readonly backLabel = input('');
  readonly backLink = input<string | readonly unknown[] | null>(null);

  private readonly titleState = inject(PageTitleState);

  constructor() {
    effect(() => this.titleState.set(this, this.title()));
    inject(DestroyRef).onDestroy(() => this.titleState.release(this));
  }
}
