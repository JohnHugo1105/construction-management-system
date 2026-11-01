import { Routes } from '@angular/router';

/**
 * STUB. Everything under `/dashboard` (needs `costs.read`, checked where app.routes.ts loads this file). Replace the routes
 * here and the page component; keep the exported name `DASHBOARD_ROUTES`. When the dashboard is ready, point the `''` redirect in
 * app.routes.ts at 'dashboard' so it becomes the home page.
 */
export const DASHBOARD_ROUTES: Routes = [
  {
    path: '',
    pathMatch: 'full',
    loadComponent: () => import('./dashboard-page').then((m) => m.DashboardPage),
  },
];
