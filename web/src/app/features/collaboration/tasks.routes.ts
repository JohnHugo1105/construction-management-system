import { Routes } from '@angular/router';

/**
 * Everything under `/tasks` (needs `tasks.use`, checked where app.routes.ts loads this file). A task is seen only by the person who created
 * it, the person it is for and Administrators; the API answers "not found" to anyone else.
 */
export const TASK_ROUTES: Routes = [
  {
    path: '',
    pathMatch: 'full',
    loadComponent: () => import('./tasks-page').then((m) => m.TasksPage),
  },
  {
    path: ':id',
    title: 'Task',
    loadComponent: () => import('./task-page').then((m) => m.TaskPage),
  },
];
