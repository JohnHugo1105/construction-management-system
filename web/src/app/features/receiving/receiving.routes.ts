import { Routes } from '@angular/router';

/**
 * Everything under `/receiving` (needs `receiving.record`, checked where app.routes.ts loads this file): the deliveries still to
 * come, and one order to record a delivery against. Nothing here reaches the purchasing screens or their prices.
 */
export const RECEIVING_ROUTES: Routes = [
  {
    path: '',
    pathMatch: 'full',
    title: 'Receiving',
    loadComponent: () => import('./receiving-page').then((m) => m.ReceivingPage),
  },
  {
    path: ':id',
    title: 'Record delivery',
    loadComponent: () => import('./receiving-order-page').then((m) => m.ReceivingOrderPage),
  },
];
