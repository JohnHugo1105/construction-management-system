import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { ChangeDetectionStrategy, Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { leave, settle, text, type } from '../../../testing/dom';
import { FormField } from '../form-field/form-field';
import { MoneyInput } from './money-input';
import { NumberInput } from './number-input';
import { PercentInput } from './percent-input';

@Component({
  imports: [ReactiveFormsModule, FormField, MoneyInput, PercentInput, NumberInput],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-form-field label="Contract price" [control]="money"
      ><app-money-input [formControl]="money"
    /></app-form-field>
    <app-form-field label="VAT rate" [control]="percent"
      ><app-percent-input [formControl]="percent"
    /></app-form-field>
    <app-form-field label="Paid hours" [control]="hours"
      ><app-number-input [formControl]="hours" [decimals]="2" unit="hours"
    /></app-form-field>
  `,
})
class Host {
  readonly money = new FormControl<number | null>(null, { validators: [Validators.required] });
  readonly percent = new FormControl<number | null>(null);
  readonly hours = new FormControl<number | null>(null);
}

async function open() {
  TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
  const fixture = TestBed.createComponent(Host);
  await settle(fixture);
  const root = fixture.nativeElement as HTMLElement;
  return { fixture, root, host: fixture.componentInstance };
}

const money = (root: HTMLElement) => root.querySelector<HTMLInputElement>('app-money-input input')!;
const percent = (root: HTMLElement) =>
  root.querySelector<HTMLInputElement>('app-percent-input input')!;
const hours = (root: HTMLElement) =>
  root.querySelector<HTMLInputElement>('app-number-input input')!;

describe('MoneyInput', () => {
  it('shows the company currency symbol beside the box and uses the decimal keypad on a phone', async () => {
    const { root } = await open();

    expect(text(root, 'app-money-input .input-group__affix')).toBe('₱');
    expect(money(root).getAttribute('inputmode')).toBe('decimal');
    expect(money(root).getAttribute('autocomplete')).toBe('off');
  });

  it('turns what is typed into centavos as it is typed', async () => {
    const { host, root } = await open();

    type(root, 'app-money-input input', '1,250.5');
    expect(host.money.value).toBe(125050);
    type(root, 'app-money-input input', '0.1');
    expect(host.money.value).toBe(10);
    type(root, 'app-money-input input', '');
    expect(host.money.value).toBeNull();
  });

  it('writes the amount tidily when the person leaves the box', async () => {
    const { fixture, root } = await open();

    type(root, 'app-money-input input', '1250.5');
    leave(root, 'app-money-input input');
    await settle(fixture);

    expect(money(root).value).toBe('1,250.50');
  });

  it('shows a stored value the same way', async () => {
    const { fixture, host, root } = await open();

    host.money.setValue(125000000);
    await settle(fixture);

    expect(money(root).value).toBe('1,250,000.00');
  });

  it('says what is wrong with text that is not an amount, once the field was touched', async () => {
    const { fixture, host, root } = await open();

    type(root, 'app-money-input input', 'abc');
    expect(host.money.value).toBeNull();
    expect(host.money.invalid).toBe(true);
    await settle(fixture);
    expect(root.querySelectorAll('.field__error')).toHaveLength(0);

    leave(root, 'app-money-input input');
    await settle(fixture);
    expect(text(root, 'app-money-input + .field__error, app-form-field .field__error')).toBe(
      'Enter an amount like 1,250.50.',
    );
    expect(money(root).getAttribute('aria-invalid')).toBe('true');
  });

  it('explains a third decimal place instead of rounding it away', async () => {
    const { fixture, host, root } = await open();

    type(root, 'app-money-input input', '1.234');
    leave(root, 'app-money-input input');
    await settle(fixture);

    expect(host.money.value).toBeNull();
    expect(text(root, '.field__error')).toBe('Use at most two decimal places, like 1,250.50.');
  });

  it('reports an empty required box as required, not as a bad amount', async () => {
    const { fixture, root } = await open();

    leave(root, 'app-money-input input');
    await settle(fixture);

    expect(text(root, '.field__error')).toBe('Enter contract price.');
  });

  it('ties the label to the box inside', async () => {
    const { root } = await open();

    const label = root.querySelector<HTMLLabelElement>('label.field__label')!;
    expect(label.getAttribute('for')).toBe(money(root).id);
  });

  it('can be switched off with the form', async () => {
    const { fixture, host, root } = await open();

    host.money.disable();
    await settle(fixture);

    expect(money(root).disabled).toBe(true);
  });
});

describe('PercentInput', () => {
  it('turns a typed percentage into basis points', async () => {
    const { host, root } = await open();

    type(root, 'app-percent-input input', '12');
    expect(host.percent.value).toBe(1200);
    type(root, 'app-percent-input input', '12.5');
    expect(host.percent.value).toBe(1250);
  });

  it('shows basis points as a percentage with a % sign beside the box', async () => {
    const { fixture, host, root } = await open();

    host.percent.setValue(1200);
    await settle(fixture);

    expect(percent(root).value).toBe('12');
    expect(text(root, 'app-percent-input .input-group__affix')).toBe('%');
  });

  it('rejects what is not a percentage', async () => {
    const { fixture, host, root } = await open();

    type(root, 'app-percent-input input', 'twelve');
    leave(root, 'app-percent-input input');
    await settle(fixture);

    expect(host.percent.value).toBeNull();
    expect(host.percent.errors).toEqual({ message: 'Enter a percentage like 12 or 12.5.' });
  });
});

describe('NumberInput', () => {
  it('reads decimals and shows the unit', async () => {
    const { fixture, host, root } = await open();

    type(root, 'app-number-input input', '8.5');
    expect(host.hours.value).toBe(8.5);
    host.hours.setValue(8);
    await settle(fixture);

    expect(hours(root).value).toBe('8');
    expect(text(root, 'app-number-input .input-group__affix')).toBe('hours');
  });

  it('allows only the decimal places it was given', async () => {
    const { host, root } = await open();

    type(root, 'app-number-input input', '8.555');

    expect(host.hours.value).toBeNull();
    expect(host.hours.errors).toEqual({ message: 'Enter a number with at most 2 decimal places.' });
  });
});
