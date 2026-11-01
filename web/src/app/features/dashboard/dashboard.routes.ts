import { Routes } from '@angular/router';

/**
 * Everything under `/dashboard` (it needs `costs.read`, checked where app.routes.ts loads this file). The dashboard is one page:
 * the company's budget against spent cost, with a row per project that opens that project's Budget tab.
 */
export const DASHBOARD_ROUTES: Routes = [
  {
    path: '',
    pathMatch: 'full',
    loadComponent: () => import('./dashboard-page').then((m) => m.DashboardPage),
  },
];
