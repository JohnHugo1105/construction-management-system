import { Routes } from '@angular/router';

/**
 * STUB. Everything under `/payroll` (needs `payroll.read`, checked where app.routes.ts loads this file). Replace the routes
 * here and the page component; keep the exported name `PAYROLL_ROUTES`. Add finer guards (`payroll.run`, `payroll.approve`) on the
 * routes you add.
 */
export const PAYROLL_ROUTES: Routes = [
  {
    path: '',
    pathMatch: 'full',
    loadComponent: () => import('./payroll-page').then((m) => m.PayrollPage),
  },
];
