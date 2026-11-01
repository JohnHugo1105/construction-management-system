import { Routes } from '@angular/router';
import { Shell } from './core/layout/shell';
import { Permission } from './core/session/permissions';
import {
  adminGuard,
  authGuard,
  guestGuard,
  permissionGuard,
  setupGuard,
} from './core/session/auth-guards';

/**
 * The route map. Every signed-in page lives under the Shell. Each section is a lazy child route file that owns its own paths:
 * to add a section, add a line here, a nav entry in core/layout/navigation.ts, and a `*.routes.ts` file in the feature folder.
 * A section's guard sits on the route that loads it, so the code is not even downloaded by someone who may not use it.
 */
export const routes: Routes = [
  {
    path: 'sign-in',
    title: 'Sign in',
    canActivate: [guestGuard],
    loadComponent: () => import('./features/auth/sign-in-page').then((m) => m.SignInPage),
  },
  {
    path: 'setup',
    title: 'Set up your company',
    canActivate: [setupGuard],
    loadComponent: () => import('./features/auth/setup-page').then((m) => m.SetupPage),
  },
  {
    path: '',
    component: Shell,
    canActivate: [authGuard],
    canActivateChild: [authGuard],
    children: [
      // The dashboard replaces this redirect when it is built: point '' at 'dashboard' instead.
      { path: '', pathMatch: 'full', redirectTo: 'projects' },
      {
        path: 'change-password',
        loadComponent: () =>
          import('./features/auth/change-password-page').then((m) => m.ChangePasswordPage),
      },
      {
        path: 'dashboard',
        canActivate: [permissionGuard(Permission.costsRead)],
        loadChildren: () =>
          import('./features/dashboard/dashboard.routes').then((m) => m.DASHBOARD_ROUTES),
      },
      {
        path: 'projects',
        canActivate: [permissionGuard(Permission.projectsRead)],
        loadChildren: () =>
          import('./features/projects/projects.routes').then((m) => m.PROJECT_ROUTES),
      },
      {
        path: 'workers',
        canActivate: [permissionGuard(Permission.workforceRead)],
        loadChildren: () =>
          import('./features/workforce/workforce.routes').then((m) => m.WORKFORCE_ROUTES),
      },
      {
        path: 'payroll',
        canActivate: [permissionGuard(Permission.payrollRead)],
        loadChildren: () =>
          import('./features/payroll/payroll.routes').then((m) => m.PAYROLL_ROUTES),
      },
      {
        path: 'stock',
        canActivate: [permissionGuard(Permission.inventoryRead)],
        loadChildren: () =>
          import('./features/inventory/inventory.routes').then((m) => m.INVENTORY_ROUTES),
      },
      {
        path: 'admin',
        canActivate: [adminGuard],
        loadChildren: () => import('./features/admin/admin.routes').then((m) => m.ADMIN_ROUTES),
      },
      {
        path: 'not-allowed',
        loadComponent: () => import('./core/pages/not-allowed-page').then((m) => m.NotAllowedPage),
      },
      {
        path: '**',
        loadComponent: () => import('./core/pages/not-found-page').then((m) => m.NotFoundPage),
      },
    ],
  },
];
