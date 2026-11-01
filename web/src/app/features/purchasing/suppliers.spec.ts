import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ChangeDetectionStrategy, Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { Observable, of } from 'rxjs';
import type { Mock } from 'vitest';
import { Permission } from '../../core/session/permissions';
import { SessionState } from '../../core/session/session-state';
import { Viewport } from '../../shared/ui/viewport/viewport';
import { buttonNamed, settle, text, texts, type } from '../../testing/dom';
import { pageOf, signInForTest } from '../../testing/session-fixture';
import { PurchasingApi } from './purchasing-api';
import { FIRM_ID, supplierFixture } from './purchasing.fixtures';
import { SupplierFormPage } from './supplier-form-page';
import { SuppliersPage } from './suppliers-page';

@Component({ template: '', changeDetection: ChangeDetectionStrategy.OnPush })
class Blank {}

type Call = (...args: unknown[]) => Observable<unknown>;
const stub = (implementation: Call): Mock<Call> => vi.fn<Call>(implementation);

const READER = [Permission.suppliersRead, Permission.purchasingRead, Permission.projectsRead];

async function open(_component: unknown, url: string, api: Record<string, Mock<Call>> = {}, permissions?: readonly string[]) {
  const firm = { id: 'f1', name: 'Visayas Steel Trading', isSupplier: true, isSubcontractor: false, contactPerson: 'Mara Co', phone: '0917 555 0104', email: null, isArchived: false, version: 0 };
  const purchasing = {
    listSuppliers: stub(() => of(pageOf([firm, { ...firm, id: 'f2', name: 'Dela Cruz Masonry', isSupplier: false, isSubcontractor: true, isArchived: true }]))),
    getSupplier: stub(() => of(supplierFixture())),
    createSupplier: stub(() => of(supplierFixture())),
    updateSupplier: stub(() => of(supplierFixture({ version: 1 }))),
    archiveSupplier: stub(() => of(supplierFixture({ isArchived: true, version: 1 }))),
    restoreSupplier: stub(() => of(supplierFixture({ version: 2 }))),
    ...api,
  };
  TestBed.configureTestingModule({
    providers: [
      provideRouter(
        [
          { path: 'purchasing/suppliers', component: SuppliersPage },
          { path: 'purchasing/suppliers/new', component: SupplierFormPage },
          { path: 'purchasing/suppliers/:id', component: SupplierFormPage },
          { path: 'purchasing', component: Blank },
        ],
        withComponentInputBinding(),
      ),
      provideHttpClient(),
      provideHttpClientTesting(),
      { provide: PurchasingApi, useValue: purchasing },
      { provide: Viewport, useValue: { isTablet: signal(true), isDesktop: signal(true) } },
    ],
  });
  await signInForTest(TestBed.inject(SessionState), TestBed.inject(HttpTestingController), permissions ? { permissions } : {});
  const harness = await RouterTestingHarness.create();
  await harness.navigateByUrl(url);
  await settle(harness.fixture);
  return { harness, root: harness.fixture.nativeElement as HTMLElement, purchasing };
}

describe('SuppliersPage', () => {
  it('lists the firms with their kind and contact, archived ones marked', async () => {
    const { root } = await open(SuppliersPage, '/purchasing/suppliers');

    const rows = root.querySelectorAll('tbody tr');
    expect(rows).toHaveLength(2);
    expect(text(rows[0]!, 'th a')).toBe('Visayas Steel Trading');
    expect(texts(rows[0]!, 'td')).toEqual(['Supplier', 'Mara Co · 0917 555 0104']);
    expect(texts(rows[1]!, 'td')[0]).toBe('Subcontractor');
    expect(text(rows[1]!, 'app-status-chip')).toBe('Archived');
  });

  it('asks for active firms only until archived ones are asked for', async () => {
    const { harness, root, purchasing } = await open(SuppliersPage, '/purchasing/suppliers');
    expect(purchasing.listSuppliers.mock.calls[0]![0]).toMatchObject({ includeArchived: false, kind: '' });

    root.querySelector<HTMLInputElement>('.toggle input')!.click();
    await settle(harness.fixture);

    expect(purchasing.listSuppliers.mock.calls.at(-1)![0]).toMatchObject({ includeArchived: true });
  });

  it('gives people who manage firms the next step and nobody else', async () => {
    const manager = await open(SuppliersPage, '/purchasing/suppliers');
    expect(buttonNamed(manager.root.querySelector('.toolbar')!, 'Add a firm').getAttribute('href')).toBe('/purchasing/suppliers/new');
    TestBed.resetTestingModule();

    const reader = await open(SuppliersPage, '/purchasing/suppliers', {}, READER);
    expect(reader.root.textContent).not.toContain('Add a firm');
  });
});

describe('SupplierFormPage', () => {
  it('asks for a name and a kind, and sends nothing until both are given', async () => {
    const { harness, root, purchasing } = await open(SupplierFormPage, '/purchasing/suppliers/new');
    root.querySelector<HTMLInputElement>('[formControlName="isSupplier"]')!.click();
    await settle(harness.fixture);

    buttonNamed(root, 'Add firm').click();
    await settle(harness.fixture);

    expect(purchasing.createSupplier).not.toHaveBeenCalled();
    expect(root.textContent).toContain("Enter the firm's name.");
    expect(root.textContent).toContain('Choose whether this firm is a supplier, a subcontractor, or both.');
  });

  it('adds a firm with the kinds ticked', async () => {
    const { harness, root, purchasing } = await open(SupplierFormPage, '/purchasing/suppliers/new');

    type(root, '[formControlName="name"]', 'Cebu Formworks');
    root.querySelector<HTMLInputElement>('[formControlName="isSubcontractor"]')!.click();
    buttonNamed(root, 'Add firm').click();
    await settle(harness.fixture);

    expect(purchasing.createSupplier).toHaveBeenCalledTimes(1);
    expect(purchasing.createSupplier.mock.calls[0]![0]).toMatchObject({ name: 'Cebu Formworks', isSupplier: true, isSubcontractor: true, email: null, tin: null });
  });

  it('saves a change with the version it opened and archives with a confirmation', async () => {
    const { harness, root, purchasing } = await open(SupplierFormPage, '/purchasing/suppliers/f1');

    type(root, '[formControlName="phone"]', '0917 000 1111');
    buttonNamed(root, 'Save firm').click();
    await settle(harness.fixture);
    expect(purchasing.updateSupplier.mock.calls[0]![2]).toBe(0);

    buttonNamed(root, 'Archive firm').click();
    await settle(harness.fixture);
    expect(document.body.textContent).toContain('Archive this firm?');
    Array.from(document.querySelectorAll<HTMLElement>('app-confirm-dialog button')).find((button) => button.textContent?.trim() === 'Archive firm')!.click();
    await settle(harness.fixture);

    expect(purchasing.archiveSupplier).toHaveBeenCalledWith(FIRM_ID, 1);
    expect(root.textContent).toContain('This firm is archived');
    expect(buttonNamed(root, 'Restore firm')).toBeTruthy();
  });

  it('shows the details to someone who may not change firms, without buttons that change them', async () => {
    const { root } = await open(SupplierFormPage, '/purchasing/suppliers/f1', {}, READER);

    expect(root.querySelector<HTMLInputElement>('[formControlName="name"]')!.disabled).toBe(true);
    expect(root.textContent).not.toContain('Save firm');
    expect(root.textContent).not.toContain('Archive firm');
  });
});
