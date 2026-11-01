import { Routes } from '@angular/router';
import { permissionGuard } from '../../core/session/auth-guards';
import { Permission } from '../../core/session/permissions';

/**
 * Everything under `/subcontracts` (needs `subcontracts.read`, checked where app.routes.ts loads this file). Drafting and editing a
 * draft need `subcontracts.manage`; the steps that commit money are buttons on the subcontract page that only approvers see. The
 * API enforces the same rules.
 */
export const SUBCONTRACT_ROUTES: Routes = [
  {
    path: '',
    pathMatch: 'full',
    loadComponent: () => import('./subcontracts-page').then((m) => m.SubcontractsPage),
  },
  {
    path: 'new',
    title: 'New subcontract',
    canActivate: [permissionGuard(Permission.subcontractsManage)],
    loadComponent: () => import('./subcontract-form-page').then((m) => m.SubcontractFormPage),
  },
  {
    path: ':id/edit',
    title: 'Edit draft subcontract',
    canActivate: [permissionGuard(Permission.subcontractsManage)],
    loadComponent: () => import('./subcontract-form-page').then((m) => m.SubcontractFormPage),
  },
  {
    path: ':id',
    loadComponent: () => import('./subcontract-page').then((m) => m.SubcontractPage),
  },
];
