import { Routes } from '@angular/router';
import { permissionGuard } from '../../core/session/auth-guards';
import { Permission } from '../../core/session/permissions';

/**
 * Everything under `/purchasing` (needs `purchasing.read`, checked where app.routes.ts loads this file). Drafting and changing an
 * order need `purchasing.manage`; the order page shows Approve, Cancel and Close only to people who hold `purchasing.approve`. The
 * firms the orders are placed with live beside them: reading needs `suppliers.read`, adding and changing `suppliers.manage`.
 */
export const PURCHASING_ROUTES: Routes = [
  {
    path: '',
    pathMatch: 'full',
    loadComponent: () => import('./purchasing-page').then((m) => m.PurchasingPage),
  },
  {
    path: 'orders/new',
    canActivate: [permissionGuard(Permission.purchasingManage)],
    loadComponent: () => import('./order-form-page').then((m) => m.OrderFormPage),
  },
  {
    path: 'orders/:id/edit',
    canActivate: [permissionGuard(Permission.purchasingManage)],
    loadComponent: () => import('./order-form-page').then((m) => m.OrderFormPage),
  },
  {
    path: 'orders/:id',
    loadComponent: () => import('./order-page').then((m) => m.OrderPage),
  },
  {
    path: 'suppliers',
    pathMatch: 'full',
    canActivate: [permissionGuard(Permission.suppliersRead)],
    loadComponent: () => import('./suppliers-page').then((m) => m.SuppliersPage),
  },
  {
    path: 'suppliers/new',
    canActivate: [permissionGuard(Permission.suppliersManage)],
    loadComponent: () => import('./supplier-form-page').then((m) => m.SupplierFormPage),
  },
  {
    path: 'suppliers/:id',
    canActivate: [permissionGuard(Permission.suppliersRead)],
    loadComponent: () => import('./supplier-form-page').then((m) => m.SupplierFormPage),
  },
];
