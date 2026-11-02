import { Routes } from '@angular/router';

/** `/needs-action` (needs `actions.read`, checked where app.routes.ts loads this file): what waits on the signed-in person. */
export const NEEDS_ACTION_ROUTES: Routes = [
  {
    path: '',
    pathMatch: 'full',
    title: 'Needs your action',
    loadComponent: () => import('./needs-action-page').then((m) => m.NeedsActionPage),
  },
];
