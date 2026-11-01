import { ChangeDetectionStrategy, Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { settle } from '../../testing/dom';
import { HoursField } from './hours-field';

@Component({
  imports: [HoursField],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<app-hours-field
    label="Regular hours for Ana Bautista"
    [value]="hours()"
    [max]="8"
    [stepBy]="step()"
    (valueChange)="hours.set($event)"
  />`,
})
class Host {
  readonly hours = signal<number | null>(8);
  readonly step = signal(1);
}

async function open() {
  const fixture = TestBed.createComponent(Host);
  await settle(fixture);
  const root = fixture.nativeElement as HTMLElement;
  return {
    fixture,
    host: fixture.componentInstance,
    input: root.querySelector<HTMLInputElement>('input')!,
    less: root.querySelector<HTMLButtonElement>('button[aria-label^="Less"]')!,
    more: root.querySelector<HTMLButtonElement>('button[aria-label^="More"]')!,
  };
}

describe('HoursField', () => {
  it('names whose hours these are for screen readers, on the box and on both buttons', async () => {
    const { input, less, more } = await open();

    expect(input.getAttribute('aria-label')).toBe('Regular hours for Ana Bautista');
    expect(less.getAttribute('aria-label')).toBe('Less: Regular hours for Ana Bautista');
    expect(more.getAttribute('aria-label')).toBe('More: Regular hours for Ana Bautista');
    expect(input.getAttribute('inputmode')).toBe('decimal');
  });

  it('steps down and up, never below zero and never above the most it accepts', async () => {
    const { fixture, host, input, less, more } = await open();

    more.click();
    await settle(fixture);
    expect(host.hours()).toBe(8);

    less.click();
    await settle(fixture);
    less.click();
    await settle(fixture);
    expect(host.hours()).toBe(6);
    expect(input.value).toBe('6');

    host.hours.set(0.5);
    await settle(fixture);
    less.click();
    await settle(fixture);
    expect(host.hours()).toBe(0);
  });

  it('steps by half hours when asked to', async () => {
    const { fixture, host, more } = await open();
    host.step.set(0.5);
    host.hours.set(2);
    await settle(fixture);

    more.click();
    await settle(fixture);

    expect(host.hours()).toBe(2.5);
  });

  it('reports what is typed as a number, or null while the box is blank or unreadable', async () => {
    const { fixture, host, input } = await open();

    input.value = '7.5';
    input.dispatchEvent(new Event('input', { bubbles: true }));
    expect(host.hours()).toBe(7.5);

    input.value = '';
    input.dispatchEvent(new Event('input', { bubbles: true }));
    expect(host.hours()).toBeNull();

    input.value = 'abc';
    input.dispatchEvent(new Event('input', { bubbles: true }));
    await settle(fixture);
    expect(host.hours()).toBeNull();
  });

  it('writes the box tidily when the person leaves it', async () => {
    const { fixture, input } = await open();

    input.value = '7.50';
    input.dispatchEvent(new Event('input', { bubbles: true }));
    input.dispatchEvent(new Event('blur'));
    await settle(fixture);

    expect(input.value).toBe('7.5');
  });
});
