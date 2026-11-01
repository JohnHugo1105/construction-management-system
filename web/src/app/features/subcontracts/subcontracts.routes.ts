import { Routes } from '@angular/router';

/** Everything under this section. Placeholder: its slice replaces this file and the page it points at. */
export const SUBCONTRACT_ROUTES: Routes = [
  {
    path: '',
    pathMatch: 'full',
    loadComponent: () => import('./subcontracts-page').then((m) => m.SubcontractsPage),
  },
];
