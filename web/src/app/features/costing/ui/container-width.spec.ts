import { ChangeDetectionStrategy, Component, ElementRef, inject } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { settle } from '../../../testing/dom';
import { injectMinWidth } from './container-width';

@Component({
  selector: 'app-probe',
  template: '{{ roomy() ? "room" : "tight" }}',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
class Probe {
  protected readonly roomy = injectMinWidth(50);
  readonly host = inject<ElementRef<HTMLElement>>(ElementRef).nativeElement;
}

describe('injectMinWidth', () => {
  let notifiers: Array<() => void>;
  const original = globalThis.ResizeObserver;

  beforeEach(() => {
    notifiers = [];
    globalThis.ResizeObserver = class {
      constructor(callback: () => void) {
        notifiers.push(callback);
      }
      observe(): void {}
      disconnect(): void {}
    } as unknown as typeof ResizeObserver;
  });

  afterEach(() => {
    globalThis.ResizeObserver = original;
  });

  const width = (element: HTMLElement, value: number) =>
    Object.defineProperty(element, 'clientWidth', { value, configurable: true });

  it('says there is room until it has measured, and wherever nothing can be measured', async () => {
    const fixture = TestBed.createComponent(Probe);
    await settle(fixture);

    // jsdom has no layout: a width of zero means "not measured", so the wide layout stays.
    expect((fixture.nativeElement as HTMLElement).textContent).toBe('room');
  });

  it('follows the width as the element is resized (50rem is 800px)', async () => {
    const fixture = TestBed.createComponent(Probe);
    width(fixture.componentInstance.host, 700);
    await settle(fixture);
    notifiers.forEach((notify) => notify());
    await settle(fixture);
    expect((fixture.nativeElement as HTMLElement).textContent).toBe('tight');

    width(fixture.componentInstance.host, 900);
    notifiers.forEach((notify) => notify());
    await settle(fixture);
    expect((fixture.nativeElement as HTMLElement).textContent).toBe('room');
  });
});
