import { inject } from '@angular/core';
import { Routes } from '@angular/router';
import { Permission } from '../../core/session/permissions';
import { permissionGuard } from '../../core/session/auth-guards';
import { SessionState } from '../../core/session/session-state';

/**
 * Everything under /admin. The area opens for anyone who may use at least one page in it (checked where app.routes.ts loads
 * this file); each page then checks its own permission, and `/admin` itself goes to the first page the person may use.
 */
export const ADMIN_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./admin-layout').then((m) => m.AdminLayout),
    children: [
      {
        path: '',
        pathMatch: 'full',
        redirectTo: () => {
          const session = inject(SessionState);
          if (session.can(Permission.usersManage)) return 'users';
          if (session.can(Permission.settingsManage)) return 'settings';
          return 'audit';
        },
      },
      {
        path: 'users',
        canActivate: [permissionGuard(Permission.usersManage)],
        loadComponent: () => import('./users/user-list-page').then((m) => m.UserListPage),
      },
      {
        path: 'users/new',
        canActivate: [permissionGuard(Permission.usersManage)],
        loadComponent: () => import('./users/user-form-page').then((m) => m.UserFormPage),
      },
      {
        path: 'users/:id/edit',
        canActivate: [permissionGuard(Permission.usersManage)],
        loadComponent: () => import('./users/user-form-page').then((m) => m.UserFormPage),
      },
      {
        path: 'settings',
        canActivate: [permissionGuard(Permission.settingsManage)],
        loadComponent: () =>
          import('./settings/company-settings-page').then((m) => m.CompanySettingsPage),
      },
      {
        path: 'time-clocks',
        title: 'Time clocks',
        canActivate: [permissionGuard(Permission.settingsManage)],
        loadComponent: () =>
          import('../timeclock/time-clock-devices-page').then((m) => m.TimeClockDevicesPage),
      },
      {
        path: 'audit',
        canActivate: [permissionGuard(Permission.auditRead)],
        loadComponent: () => import('./audit/audit-log-page').then((m) => m.AuditLogPage),
      },
    ],
  },
];
