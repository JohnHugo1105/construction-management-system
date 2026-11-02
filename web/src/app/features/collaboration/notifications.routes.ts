import { Routes } from '@angular/router';

/** Everything under `/notifications` (needs `notifications.read`). Replaced by the collaboration slice. */
export const NOTIFICATION_ROUTES: Routes = [
  {
    path: '',
    pathMatch: 'full',
    loadComponent: () => import('./notifications-page').then((m) => m.NotificationsPage),
  },
];
