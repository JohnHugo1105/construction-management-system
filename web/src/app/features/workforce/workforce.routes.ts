import { Routes } from '@angular/router';
import { permissionGuard } from '../../core/session/auth-guards';
import { Permission } from '../../core/session/permissions';

/**
 * Everything under `/workers` (needs `workforce.read`, checked where app.routes.ts loads this file). Hiring and editing a profile
 * need `workforce.manage`, so a person who may only look never reaches those pages. The worker page itself shows only what the
 * API sent, so pay is absent for anyone who may not see it.
 */
export const WORKFORCE_ROUTES: Routes = [
  {
    path: '',
    pathMatch: 'full',
    loadComponent: () => import('./workers-page').then((m) => m.WorkersPage),
  },
  {
    path: 'new',
    canActivate: [permissionGuard(Permission.workforceManage)],
    loadComponent: () => import('./worker-form-page').then((m) => m.WorkerFormPage),
  },
  {
    path: ':id/edit',
    canActivate: [permissionGuard(Permission.workforceManage)],
    loadComponent: () => import('./worker-profile-page').then((m) => m.WorkerProfilePage),
  },
  {
    path: ':id',
    loadComponent: () => import('./worker-detail-page').then((m) => m.WorkerDetailPage),
  },
];
