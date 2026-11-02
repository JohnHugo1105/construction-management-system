import { Routes } from '@angular/router';

/** Everything under `/tasks` (needs `tasks.use`, checked where app.routes.ts loads this file). Replaced by the collaboration slice. */
export const TASK_ROUTES: Routes = [
  {
    path: '',
    pathMatch: 'full',
    loadComponent: () => import('./tasks-page').then((m) => m.TasksPage),
  },
];
