import { Injectable, effect, inject, signal } from '@angular/core';
import { Title } from '@angular/platform-browser';
import { SessionState } from '../session/session-state';

/**
 * The title of the page being shown. The shell header displays it, and it becomes the browser tab title ("Projects - Acme
 * Builders"). A page sets it by rendering `<app-page-header title="...">`; nothing else needs to touch it.
 */
@Injectable({ providedIn: 'root' })
export class PageTitleState {
  private readonly browserTitle = inject(Title);
  private readonly session = inject(SessionState);
  private readonly current = signal('');
  private owner: object | null = null;

  readonly title = this.current.asReadonly();

  constructor() {
    effect(() => {
      const page = this.current();
      const company = this.session.organizationName();
      this.browserTitle.setTitle(
        [page, company || 'Construction Management'].filter(Boolean).join(' - '),
      );
    });
  }

  /** The most recent page header wins. `owner` lets that header (and only that one) clear it again when it goes away. */
  set(owner: object, title: string): void {
    this.owner = owner;
    this.current.set(title);
  }

  release(owner: object): void {
    if (this.owner !== owner) return;
    this.owner = null;
    this.current.set('');
  }
}
