import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ChangeDetectionStrategy, Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter, withComponentInputBinding } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { of } from 'rxjs';
import { permissionGuard } from '../../core/session/auth-guards';
import { Permission } from '../../core/session/permissions';
import { SessionState } from '../../core/session/session-state';
import { settle } from '../../testing/dom';
import { VIEWER_PERMISSIONS, pageOf, signInForTest } from '../../testing/session-fixture';
import { InventoryApi } from './inventory-api';
import { hammers } from './inventory.fixtures';
import { INVENTORY_ROUTES } from './inventory.routes';

@Component({ template: 'not allowed', changeDetection: ChangeDetectionStrategy.OnPush })
class NotAllowed {}

/** The stock routes mounted as the app mounts them: `/stock` behind `inventory.read`. */
async function openAs(permissions: readonly string[], url: string) {
  TestBed.configureTestingModule({
    providers: [
      provideRouter(
        [
          {
            path: 'stock',
            canActivate: [permissionGuard(Permission.inventoryRead)],
            loadChildren: () => INVENTORY_ROUTES,
          },
          { path: 'not-allowed', component: NotAllowed },
        ],
        withComponentInputBinding(),
      ),
      provideHttpClient(),
      provideHttpClientTesting(),
      {
        provide: InventoryApi,
        useValue: {
          list: () => of(pageOf([])),
          get: () => of(hammers()),
          movements: () => of(pageOf([])),
          projectLookup: () => of([]),
        },
      },
    ],
  });
  await signInForTest(TestBed.inject(SessionState), TestBed.inject(HttpTestingController), {
    roles: ['Custom'],
    permissions,
  });
  const harness = await RouterTestingHarness.create();
  await harness.navigateByUrl(url);
  await settle(harness.fixture);
  return { router: TestBed.inject(Router), root: harness.fixture.nativeElement as HTMLElement };
}

describe('INVENTORY_ROUTES', () => {
  it('opens the stock list for anyone who may read stock', async () => {
    const { router, root } = await openAs(VIEWER_PERMISSIONS, '/stock');

    expect(router.url).toBe('/stock');
    expect(root.querySelector('app-stock-page')).not.toBeNull();
  });

  it('opens an item for anyone who may read stock', async () => {
    const { router, root } = await openAs(VIEWER_PERMISSIONS, '/stock/item-hammer');

    expect(router.url).toBe('/stock/item-hammer');
    expect(root.querySelector('app-item-detail-page')).not.toBeNull();
  });

  it('shows the not-allowed page to someone who may not read stock, such as the HR officer', async () => {
    const { router, root } = await openAs(
      [Permission.projectsRead, Permission.workforceRead, Permission.payrollRead],
      '/stock',
    );

    expect(router.url).toBe('/not-allowed');
    expect(root.textContent).toContain('not allowed');
  });

  it.each(['/stock/new', '/stock/item-hammer/edit'])(
    'keeps %s for people who manage stock: a reader is sent to the not-allowed page',
    async (url) => {
      const { router } = await openAs(VIEWER_PERMISSIONS, url);

      expect(router.url).toBe('/not-allowed');
    },
  );

  it('lets people who manage stock register and edit', async () => {
    const register = await openAs(
      [Permission.inventoryRead, Permission.inventoryManage],
      '/stock/new',
    );
    expect(register.router.url).toBe('/stock/new');
    expect(register.root.querySelector('app-item-form-page')).not.toBeNull();
    TestBed.resetTestingModule();

    const edit = await openAs(
      [Permission.inventoryRead, Permission.inventoryManage],
      '/stock/item-hammer/edit',
    );
    expect(edit.router.url).toBe('/stock/item-hammer/edit');
    expect(edit.root.querySelector('app-item-form-page')).not.toBeNull();
  });
});
