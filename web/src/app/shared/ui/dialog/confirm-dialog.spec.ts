import { ChangeDetectionStrategy, Component, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ConfirmDialog } from './confirm-dialog';

@Component({
  imports: [ConfirmDialog],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <button id="opener" type="button" (click)="asking.set(true)">Archive project</button>
    @if (asking()) {
      <app-confirm-dialog
        title="Archive this project?"
        message="It leaves the project list and becomes read-only."
        confirmLabel="Archive project"
        [busy]="busy()"
        (confirmed)="confirmed.set(confirmed() + 1)"
        (cancelled)="asking.set(false)"
      />
    }
  `,
})
class Host {
  readonly asking = signal(false);
  readonly busy = signal(false);
  readonly confirmed = signal(0);
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
  const opener = root.querySelector<HTMLButtonElement>('#opener')!;
  opener.focus();
  opener.click();
  await settle(fixture);
  return { fixture, root, opener, host: fixture.componentInstance };
}

const dialogOf = (root: HTMLElement) => root.querySelector<HTMLDialogElement>('dialog');
const buttonNamed = (root: HTMLElement, name: string) =>
  Array.from(root.querySelectorAll<HTMLButtonElement>('dialog button')).find(
    (button) => button.textContent?.trim() === name || button.getAttribute('aria-label') === name,
  )!;

describe('ConfirmDialog focus behaviour', () => {
  it('opens as a modal dialog named by its title, with the question stated as a pending action', async () => {
    const { root } = await open();

    const dialog = dialogOf(root)!;
    expect(dialog.hasAttribute('open')).toBe(true);
    const heading = dialog.querySelector('h2')!;
    expect(heading.textContent).toBe('Archive this project?');
    expect(dialog.getAttribute('aria-labelledby')).toBe(heading.id);
  });

  it('moves focus to Cancel, the safe choice, not to the destructive button', async () => {
    const { root } = await open();

    expect(document.activeElement).toBe(buttonNamed(root, 'Cancel'));
  });

  it('closes on Escape and gives focus back to the button that opened it', async () => {
    const { fixture, root, opener, host } = await open();

    dialogOf(root)!.dispatchEvent(new Event('cancel', { cancelable: true }));
    await settle(fixture);

    expect(host.asking()).toBe(false);
    expect(dialogOf(root)).toBeNull();
    expect(document.activeElement).toBe(opener);
  });

  it('closes from Cancel and restores focus the same way', async () => {
    const { fixture, root, opener, host } = await open();

    buttonNamed(root, 'Cancel').click();
    await settle(fixture);

    expect(host.asking()).toBe(false);
    expect(document.activeElement).toBe(opener);
  });

  it('closes when the backdrop is clicked, but not when the panel is', async () => {
    const { fixture, root, host } = await open();
    const dialog = dialogOf(root)!;

    dialog.querySelector<HTMLElement>('.dialog__panel')!.click();
    await settle(fixture);
    expect(host.asking()).toBe(true);

    dialog.click();
    await settle(fixture);
    expect(host.asking()).toBe(false);
  });

  it('keeps Tab inside the dialog, wrapping from the last control to the first', async () => {
    const { root } = await open();
    const dialog = dialogOf(root)!;
    const confirm = buttonNamed(root, 'Archive project');
    const firstControl = buttonNamed(root, 'Close');

    confirm.focus();
    const forward = new KeyboardEvent('keydown', { key: 'Tab', bubbles: true, cancelable: true });
    confirm.dispatchEvent(forward);
    expect(forward.defaultPrevented).toBe(true);
    expect(document.activeElement).toBe(firstControl);

    const backward = new KeyboardEvent('keydown', {
      key: 'Tab',
      shiftKey: true,
      bubbles: true,
      cancelable: true,
    });
    firstControl.dispatchEvent(backward);
    expect(backward.defaultPrevented).toBe(true);
    expect(document.activeElement).toBe(confirm);
    expect(dialog.contains(document.activeElement)).toBe(true);
  });

  it('reports a confirmation without closing by itself, so the page decides when to remove it', async () => {
    const { fixture, root, host } = await open();

    buttonNamed(root, 'Archive project').click();
    await settle(fixture);

    expect(host.confirmed()).toBe(1);
    expect(host.asking()).toBe(true);
  });

  it('cannot be dismissed or confirmed again while the action is running', async () => {
    const { fixture, root, host } = await open();
    host.busy.set(true);
    await settle(fixture);

    dialogOf(root)!.dispatchEvent(new Event('cancel', { cancelable: true }));
    buttonNamed(root, 'Close').click();
    buttonNamed(root, 'Archive project').click();
    await settle(fixture);

    expect(host.asking()).toBe(true);
    expect(host.confirmed()).toBe(0);
    expect(buttonNamed(root, 'Archive project').getAttribute('aria-busy')).toBe('true');
  });
});
