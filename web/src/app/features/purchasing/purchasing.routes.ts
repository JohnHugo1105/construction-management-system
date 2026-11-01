import { Routes } from '@angular/router';

/** Everything under this section. Placeholder: its slice replaces this file and the page it points at. */
export const PURCHASING_ROUTES: Routes = [
  {
    path: '',
    pathMatch: 'full',
    loadComponent: () => import('./purchasing-page').then((m) => m.PurchasingPage),
  },
];
