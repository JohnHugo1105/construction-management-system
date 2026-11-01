import { ComponentFixture } from '@angular/core/testing';

/** Lets pending signals, effects and promises finish, then draws the result. */
export async function settle(fixture: ComponentFixture<unknown>): Promise<void> {
  await fixture.whenStable();
  fixture.detectChanges();
  await fixture.whenStable();
  fixture.detectChanges();
}

/** A few turns of the event loop, for work that finishes with a timer or a resolved promise. */
export async function flushMicrotasks(turns = 3): Promise<void> {
  for (let turn = 0; turn < turns; turn++) await Promise.resolve();
}

/** Types into a field the way a person does: sets the value and tells the form. */
export function type(root: ParentNode, selector: string, value: string): void {
  const field = root.querySelector<HTMLInputElement | HTMLTextAreaElement>(selector);
  if (!field) throw new Error(`No field matches ${selector}`);
  field.value = value;
  field.dispatchEvent(new Event('input', { bubbles: true }));
}

/** Leaves a field, so its inline validation shows. */
export function leave(root: ParentNode, selector: string): void {
  const field = root.querySelector<HTMLElement>(selector);
  if (!field) throw new Error(`No field matches ${selector}`);
  field.dispatchEvent(new Event('blur', { bubbles: true }));
}

export function text(root: ParentNode, selector: string): string {
  return root.querySelector(selector)?.textContent?.replace(/\s+/g, ' ').trim() ?? '';
}

export function texts(root: ParentNode, selector: string): string[] {
  return Array.from(root.querySelectorAll(selector)).map(
    (node) => node.textContent?.replace(/\s+/g, ' ').trim() ?? '',
  );
}

/** The button or link whose visible words are exactly these. */
export function buttonNamed(root: ParentNode, name: string): HTMLElement {
  const match = Array.from(root.querySelectorAll<HTMLElement>('button, a')).find(
    (element) => element.textContent?.replace(/\s+/g, ' ').trim() === name,
  );
  if (!match) throw new Error(`No button or link named "${name}"`);
  return match;
}
