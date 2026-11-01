import { Routes } from '@angular/router';

/**
 * Everything under `/billing` (needs `billing.read`, checked where app.routes.ts loads this file): what clients owe, and one bill.
 * Bills are prepared from a project's Billing tab. The actions on a bill exist only for people who hold `billing.manage` or
 * `billing.approve`; the API enforces the same rules.
 */
export const BILLING_ROUTES: Routes = [
  {
    path: '',
    pathMatch: 'full',
    loadComponent: () => import('./billing-page').then((m) => m.BillingPage),
  },
  {
    path: 'bills/:id',
    loadComponent: () => import('./bill-page').then((m) => m.BillPage),
  },
];
