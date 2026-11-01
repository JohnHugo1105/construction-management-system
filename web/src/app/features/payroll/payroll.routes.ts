import { Routes } from '@angular/router';
import { permissionGuard } from '../../core/session/auth-guards';
import { Permission } from '../../core/session/permissions';

/**
 * Everything under `/payroll` (needs `payroll.read`, checked where app.routes.ts loads this file). Creating a run needs
 * `payroll.run`, so a person who may only read never reaches that page. Approving and voiding are buttons on the run page that
 * exist only for people who hold `payroll.approve` or `payroll.run`; the API enforces the same rules.
 */
export const PAYROLL_ROUTES: Routes = [
  {
    path: '',
    pathMatch: 'full',
    loadComponent: () => import('./payroll-page').then((m) => m.PayrollPage),
  },
  {
    path: 'new',
    canActivate: [permissionGuard(Permission.payrollRun)],
    loadComponent: () => import('./payroll-new-page').then((m) => m.PayrollNewPage),
  },
  // The contribution and tax tables: read with payroll.read, added and changed with statutory.manage (the API checks the same).
  {
    path: 'statutory',
    pathMatch: 'full',
    loadComponent: () => import('./statutory-page').then((m) => m.StatutoryPage),
  },
  {
    path: 'statutory/new',
    canActivate: [permissionGuard(Permission.statutoryManage)],
    loadComponent: () => import('./statutory-schedule-page').then((m) => m.StatutorySchedulePage),
  },
  {
    path: 'statutory/:id',
    loadComponent: () => import('./statutory-schedule-page').then((m) => m.StatutorySchedulePage),
  },
  {
    path: ':id',
    loadComponent: () => import('./payroll-run-page').then((m) => m.PayrollRunPage),
  },
];
