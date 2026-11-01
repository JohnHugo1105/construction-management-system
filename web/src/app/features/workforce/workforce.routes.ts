import { Routes } from '@angular/router';

/**
 * STUB. Everything under `/workers` (needs `workforce.read`, checked where app.routes.ts loads this file). Replace the routes
 * here and the page component; keep the exported name `WORKFORCE_ROUTES`. Add finer guards (for example
 * `permissionGuard(Permission.workforceManage)` on a "new worker" route) on the routes you add.
 */
export const WORKFORCE_ROUTES: Routes = [
  {
    path: '',
    pathMatch: 'full',
    loadComponent: () => import('./workers-page').then((m) => m.WorkersPage),
  },
];
