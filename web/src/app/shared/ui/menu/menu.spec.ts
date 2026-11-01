import { ChangeDetectionStrategy, Component, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Menu } from './menu';

@Component({
  imports: [Menu],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <button id="outside" type="button">Somewhere else</button>
    <app-menu label="More" ariaLabel="More project actions">
      <button type="button" class="menu__item" (click)="chosen.set('hold')">Put on hold</button>
      <button type="button" class="menu__item" (click)="chosen.set('archive')">
        Archive project
      </button>
      <button type="button" class="menu__item" (click)="chosen.set('restore')">
        Restore project
      </button>
    </app-menu>
  `,
})
class Host {
  readonly chosen = signal('');
}

async function settle(fixture: ComponentFixture<unknown>): Promise<void> {
  fixture.detectChanges();
  await fixture.whenStable();
  fixture.detectChanges();
}

async function open() {
  const fixture = TestBed.createComponent(Host);
  await settle(fixture);
  const root = fixture.nativeElement as HTMLElement;
  const trigger = root.querySelector<HTMLButtonElement>('app-menu button')!;
  return { fixture, root, trigger, host: fixture.componentInstance };
}

const items = (root: HTMLElement) => Array.from(root.querySelectorAll<HTMLElement>('.menu__item'));
const key = (target: HTMLElement, name: string) =>
  target.dispatchEvent(
    new KeyboardEvent('keydown', { key: name, bubbles: true, cancelable: true }),
  );

describe('Menu', () => {
  it('starts closed, and says so to assistive technology', async () => {
    const { root, trigger } = await open();

    expect(root.querySelector('.menu__panel')).toBeNull();
    expect(trigger.getAttribute('aria-expanded')).toBe('false');
    expect(trigger.getAttribute('aria-controls')).toBeNull();
    expect(trigger.getAttribute('aria-label')).toBe('More project actions');
  });

  it('opens on click and reports it, tying the button to the list', async () => {
    const { fixture, root, trigger } = await open();

    trigger.click();
    await settle(fixture);

    const panel = root.querySelector('.menu__panel')!;
    expect(trigger.getAttribute('aria-expanded')).toBe('true');
    expect(trigger.getAttribute('aria-controls')).toBe(panel.id);
    expect(items(root).map((item) => item.textContent?.trim())).toEqual([
      'Put on hold',
      'Archive project',
      'Restore project',
    ]);
  });

  it('closes on a second click of the button', async () => {
    const { fixture, root, trigger } = await open();

    trigger.click();
    await settle(fixture);
    trigger.click();
    await settle(fixture);

    expect(root.querySelector('.menu__panel')).toBeNull();
  });

  it('closes on Escape and puts focus back on the button', async () => {
    const { fixture, root, trigger } = await open();
    trigger.click();
    await settle(fixture);
    items(root)[1]!.focus();

    key(items(root)[1]!, 'Escape');
    await settle(fixture);

    expect(root.querySelector('.menu__panel')).toBeNull();
    expect(document.activeElement).toBe(trigger);
  });

  it('runs the chosen action, closes, and returns focus to the button', async () => {
    const { fixture, root, trigger, host } = await open();
    trigger.click();
    await settle(fixture);

    items(root)[1]!.click();
    await settle(fixture);

    expect(host.chosen()).toBe('archive');
    expect(root.querySelector('.menu__panel')).toBeNull();
    expect(document.activeElement).toBe(trigger);
  });

  it('closes when the person clicks elsewhere, without stealing focus', async () => {
    const { fixture, root, trigger } = await open();
    trigger.click();
    await settle(fixture);
    const outside = root.querySelector<HTMLButtonElement>('#outside')!;
    outside.focus();

    outside.click();
    await settle(fixture);

    expect(root.querySelector('.menu__panel')).toBeNull();
    expect(document.activeElement).toBe(outside);
  });

  it('opens from the keyboard with the down arrow and lands on the first action', async () => {
    const { fixture, root, trigger } = await open();
    trigger.focus();

    key(trigger, 'ArrowDown');
    await settle(fixture);

    expect(root.querySelector('.menu__panel')).not.toBeNull();
    expect(document.activeElement).toBe(items(root)[0]);
  });

  it('moves between actions with the arrow keys, wrapping at the ends, and Home and End jump', async () => {
    const { fixture, root, trigger } = await open();
    trigger.click();
    await settle(fixture);
    const [first, second, third] = items(root);
    first!.focus();

    key(first!, 'ArrowDown');
    expect(document.activeElement).toBe(second);
    key(second!, 'ArrowDown');
    expect(document.activeElement).toBe(third);
    key(third!, 'ArrowDown');
    expect(document.activeElement).toBe(first);
    key(first!, 'ArrowUp');
    expect(document.activeElement).toBe(third);
    key(third!, 'Home');
    expect(document.activeElement).toBe(first);
    key(first!, 'End');
    expect(document.activeElement).toBe(third);
  });

  it('keeps the list hidden until it has been placed, so it never flashes in the wrong spot', async () => {
    const { fixture, root, trigger } = await open();

    trigger.click();
    await settle(fixture);

    const panel = root.querySelector<HTMLElement>('.menu__panel')!;
    expect(panel.style.visibility).toBe('visible');
    expect(panel.style.top).toMatch(/px$/);
    expect(panel.style.left).toMatch(/px$/);
  });
});
