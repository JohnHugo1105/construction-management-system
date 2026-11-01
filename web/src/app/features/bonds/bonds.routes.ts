import { Routes } from '@angular/router';

/** Everything under `/bonds` (needs `bonds.read`, checked where app.routes.ts loads this file). Replaced by the bonds slice. */
export const BONDS_ROUTES: Routes = [
  {
    path: '',
    pathMatch: 'full',
    loadComponent: () => import('./bonds-page').then((m) => m.BondsPage),
  },
];
