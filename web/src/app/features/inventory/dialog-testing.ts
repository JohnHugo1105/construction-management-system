// Helpers for the specs of the action dialogs. Not shipped: only the specs import this file.
import { HttpTestingController } from '@angular/common/http/testing';
import { Provider, Type } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Observable, of } from 'rxjs';
import { SessionState } from '../../core/session/session-state';
import { settle } from '../../testing/dom';
import { signInForTest } from '../../testing/session-fixture';
import { InventoryApi } from './inventory-api';
import { PROJECT_LOOKUP } from './inventory.fixtures';
import { InventoryItem } from './inventory.models';
import { StockActionDialog } from './stock-action-dialog';
import { StockPlaces } from './stock-places';

export type ApiStub = Record<string, (...args: never[]) => Observable<unknown>>;

/**
 * What a dialog needs besides the router and HTTP set-up each spec writes for itself: a stand-in for the API (with the
 * company's projects available unless the spec says otherwise) and the places list the item page provides.
 */
export function dialogProviders(api: ApiStub): Provider[] {
  return [
    StockPlaces,
    { provide: InventoryApi, useValue: { projectLookup: () => of(PROJECT_LOOKUP), ...api } },
  ];
}

/**
 * Opens one action dialog on an item the way the item page does: signed in as an administrator, the item passed in. The spec
 * has configured TestBed (with `dialogProviders`) first. Returns the dialog and what it announced.
 */
export async function openDialog<T extends StockActionDialog>(
  dialogType: Type<T>,
  item: InventoryItem,
  inputs: Record<string, unknown> = {},
) {
  await signInForTest(TestBed.inject(SessionState), TestBed.inject(HttpTestingController));
  const fixture = TestBed.createComponent(dialogType);
  fixture.componentRef.setInput('item', item);
  for (const [name, value] of Object.entries(inputs)) fixture.componentRef.setInput(name, value);
  const done: InventoryItem[] = [];
  const dismissed: number[] = [];
  const reloaded: number[] = [];
  fixture.componentInstance.done.subscribe((updated) => done.push(updated));
  fixture.componentInstance.dismissed.subscribe(() => dismissed.push(1));
  fixture.componentInstance.reloaded.subscribe(() => reloaded.push(1));
  await settle(fixture);
  return {
    fixture,
    root: fixture.nativeElement as HTMLElement,
    done,
    dismissed,
    reloaded,
    settle: () => settle(fixture),
  };
}

/** Chooses an option in a select the way a person does. */
export async function pick(
  fixture: ComponentFixture<unknown>,
  root: ParentNode,
  controlName: string,
  value: string,
): Promise<void> {
  const select = root.querySelector<HTMLSelectElement>(`select[formControlName="${controlName}"]`);
  if (!select) throw new Error(`No select named ${controlName}`);
  select.value = value;
  select.dispatchEvent(new Event('change', { bubbles: true }));
  await settle(fixture);
}

/** The error under a field, by the name of its control. */
export function errorUnder(root: ParentNode, controlName: string): string {
  const control = root.querySelector(`[formControlName="${controlName}"]`);
  return (
    control?.closest('app-form-field')?.querySelector('.field__error')?.textContent?.trim() ?? ''
  );
}

/** The button that sends the dialog's form. */
export function submitButton(root: ParentNode): HTMLButtonElement {
  const button = root.querySelector<HTMLButtonElement>('.dialog-actions button[type="submit"]');
  if (!button) throw new Error('No submit button');
  return button;
}
