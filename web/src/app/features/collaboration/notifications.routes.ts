import { Routes } from '@angular/router';

/** Everything under `/notifications` (needs `notifications.read`): the signed-in person's own notifications, in the app only. */
export const NOTIFICATION_ROUTES: Routes = [
  {
    path: '',
    pathMatch: 'full',
    loadComponent: () => import('./notifications-page').then((m) => m.NotificationsPage),
  },
];
