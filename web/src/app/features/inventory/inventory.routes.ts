import { Routes } from '@angular/router';
import { permissionGuard } from '../../core/session/auth-guards';
import { Permission } from '../../core/session/permissions';

/**
 * Everything under `/stock` (needs `inventory.read`, checked where app.routes.ts loads this file). Registering and editing need
 * `inventory.manage`; the list and an item's page are for everyone who may read stock, and show action buttons only to those
 * who may manage it.
 */
export const INVENTORY_ROUTES: Routes = [
  {
    path: '',
    pathMatch: 'full',
    loadComponent: () => import('./stock-page').then((m) => m.StockPage),
  },
  {
    path: 'new',
    canActivate: [permissionGuard(Permission.inventoryManage)],
    loadComponent: () => import('./item-form-page').then((m) => m.ItemFormPage),
  },
  {
    path: ':id/edit',
    canActivate: [permissionGuard(Permission.inventoryManage)],
    loadComponent: () => import('./item-form-page').then((m) => m.ItemFormPage),
  },
  {
    path: ':id',
    loadComponent: () => import('./item-detail-page').then((m) => m.ItemDetailPage),
  },
];
