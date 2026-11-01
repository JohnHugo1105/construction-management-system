import { Routes } from '@angular/router';

/**
 * Everything under `/bonds` (needs `bonds.read`, checked where app.routes.ts loads this file). Adding and changing a bond are dialogs
 * on these pages that only people who hold `bonds.manage` are offered; the API enforces the same rule.
 */
export const BONDS_ROUTES: Routes = [
  {
    path: '',
    pathMatch: 'full',
    loadComponent: () => import('./bonds-page').then((m) => m.BondsPage),
  },
  {
    path: ':id',
    title: 'Bond',
    loadComponent: () => import('./bond-page').then((m) => m.BondPage),
  },
];
