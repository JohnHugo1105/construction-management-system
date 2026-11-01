import { Routes } from '@angular/router';
import { Permission } from '../../core/session/permissions';
import { permissionGuard } from '../../core/session/auth-guards';

/**
 * Everything under /projects. The detail page (`:id`) provides the ProjectContext and owns the tab links; the tabs for other
 * areas are child routes that load the components other features own (budget, costs, team, stock), each behind the permission to
 * read it. To replace a tab, change the component in its own file: the route here stays as it is.
 */
export const PROJECT_ROUTES: Routes = [
  {
    path: '',
    pathMatch: 'full',
    loadComponent: () => import('./project-list-page').then((m) => m.ProjectListPage),
  },
  {
    path: 'new',
    canActivate: [permissionGuard(Permission.projectsManage)],
    loadComponent: () => import('./project-form-page').then((m) => m.ProjectFormPage),
  },
  {
    path: ':id/edit',
    canActivate: [permissionGuard(Permission.projectsManage)],
    loadComponent: () => import('./project-form-page').then((m) => m.ProjectFormPage),
  },
  {
    path: ':id',
    loadComponent: () => import('./project-detail-page').then((m) => m.ProjectDetailPage),
    children: [
      {
        path: '',
        pathMatch: 'full',
        loadComponent: () => import('./project-overview-tab').then((m) => m.ProjectOverviewTab),
      },
      {
        path: 'budget',
        canActivate: [permissionGuard(Permission.costsRead)],
        loadComponent: () =>
          import('../costing/project-budget-tab/project-budget-tab').then(
            (m) => m.ProjectBudgetTab,
          ),
      },
      {
        path: 'progress',
        canActivate: [permissionGuard(Permission.progressRead)],
        loadComponent: () =>
          import('../progress/project-progress-tab/project-progress-tab').then(
            (m) => m.ProjectProgressTab,
          ),
      },
      {
        path: 'changes',
        canActivate: [permissionGuard(Permission.changeOrdersRead)],
        loadComponent: () =>
          import('../change-orders/project-changes-tab/project-changes-tab').then(
            (m) => m.ProjectChangesTab,
          ),
      },
      {
        path: 'purchases',
        canActivate: [permissionGuard(Permission.purchasingRead)],
        loadComponent: () =>
          import('../purchasing/project-purchases-tab/project-purchases-tab').then(
            (m) => m.ProjectPurchasesTab,
          ),
      },
      {
        path: 'subcontracts',
        canActivate: [permissionGuard(Permission.subcontractsRead)],
        loadComponent: () =>
          import('../subcontracts/project-subcontracts-tab/project-subcontracts-tab').then(
            (m) => m.ProjectSubcontractsTab,
          ),
      },
      {
        path: 'billing',
        canActivate: [permissionGuard(Permission.billingRead)],
        loadComponent: () =>
          import('../billing/project-billing-tab/project-billing-tab').then(
            (m) => m.ProjectBillingTab,
          ),
      },
      {
        path: 'costs',
        canActivate: [permissionGuard(Permission.costsRead)],
        loadComponent: () =>
          import('../costing/project-costs-tab/project-costs-tab').then((m) => m.ProjectCostsTab),
      },
      {
        path: 'team',
        canActivate: [permissionGuard(Permission.workforceRead)],
        loadComponent: () =>
          import('../workforce/project-team-tab/project-team-tab').then((m) => m.ProjectTeamTab),
      },
      {
        path: 'stock',
        canActivate: [permissionGuard(Permission.inventoryRead)],
        loadComponent: () =>
          import('../inventory/project-stock-tab/project-stock-tab').then((m) => m.ProjectStockTab),
      },
    ],
  },
];
