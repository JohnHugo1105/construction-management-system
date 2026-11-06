import { Routes } from '@angular/router';
import { Permission } from '../../core/session/permissions';
import { permissionGuard } from '../../core/session/auth-guards';

/**
 * Everything under `/time-clock`. The area opens for people who review scans or issue badges (checked where app.routes.ts loads this file); each
 * page then checks its own permission. The kiosk (`/kiosk`) is outside the signed-in app and is not here.
 */
export const TIME_CLOCK_ROUTES: Routes = [
  {
    path: 'badges',
    title: 'Badges',
    canActivate: [permissionGuard(Permission.timeClockManage)],
    loadComponent: () => import('./badges-page').then((m) => m.BadgesPage),
  },
  {
    path: '',
    pathMatch: 'full',
    title: 'Time clock',
    canActivate: [permissionGuard(Permission.timeClockReview)],
    loadComponent: () => import('./time-clock-review-page').then((m) => m.TimeClockReviewPage),
  },
  {
    path: ':projectId',
    title: 'Time clock',
    canActivate: [permissionGuard(Permission.timeClockReview)],
    loadComponent: () => import('./time-clock-review-page').then((m) => m.TimeClockReviewPage),
  },
];
