import { Routes } from '@angular/router';

/** Everything under this section. Placeholder: its slice replaces this file and the page it points at. */
export const BILLING_ROUTES: Routes = [
  {
    path: '',
    pathMatch: 'full',
    loadComponent: () => import('./billing-page').then((m) => m.BillingPage),
  },
];
