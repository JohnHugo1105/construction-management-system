import { Routes } from '@angular/router';

/**
 * STUB. Everything under `/stock` (needs `inventory.read`, checked where app.routes.ts loads this file). Replace the routes
 * here and the page component; keep the exported name `INVENTORY_ROUTES`. Add finer guards (`inventory.manage`) on the routes you add.
 */
export const INVENTORY_ROUTES: Routes = [
  {
    path: '',
    pathMatch: 'full',
    loadComponent: () => import('./stock-page').then((m) => m.StockPage),
  },
];
