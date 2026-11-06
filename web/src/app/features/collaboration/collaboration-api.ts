import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { PagedResult } from '../../core/api/paged-result';
import {
  AppNotification,
  Colleague,
  CommentPage,
  DiscussionComment,
  DiscussionKind,
  NewCommentInput,
  NewTaskInput,
  NotificationCount,
  ProjectChoice,
  TaskListItem,
  TaskListParams,
  WorkTask,
} from './collaboration.models';

/**
 * The collaboration endpoints, one method each: tasks, the discussion on a record, the signed-in person's own notifications and the
 * colleague lookup. Every task step answers with the whole task; marking a notification read answers with the new unread count.
 */
@Injectable({ providedIn: 'root' })
export class CollaborationApi {
  private readonly http = inject(HttpClient);

  // ---- Tasks ----------------------------------------------------------------------------------------------------------

  listTasks(params: TaskListParams): Observable<PagedResult<TaskListItem>> {
    let query = new HttpParams()
      .set('scope', params.scope)
      .set('page', params.page)
      .set('pageSize', params.pageSize);
    if (params.status) query = query.set('status', params.status);
    if (params.projectId) query = query.set('projectId', params.projectId);
    return this.http.get<PagedResult<TaskListItem>>('/api/tasks', { params: query });
  }

  getTask(id: string): Observable<WorkTask> {
    return this.http.get<WorkTask>(`/api/tasks/${id}`);
  }

  createTask(input: NewTaskInput, clientRequestId: string): Observable<WorkTask> {
    return this.http.post<WorkTask>('/api/tasks', { ...input, clientRequestId });
  }

  completeTask(id: string, version: number): Observable<WorkTask> {
    return this.http.post<WorkTask>(`/api/tasks/${id}/complete`, { version });
  }

  cancelTask(id: string, reason: string | null, version: number): Observable<WorkTask> {
    return this.http.post<WorkTask>(`/api/tasks/${id}/cancel`, { reason, version });
  }

  reopenTask(id: string, version: number): Observable<WorkTask> {
    return this.http.post<WorkTask>(`/api/tasks/${id}/reopen`, { version });
  }

  reassignTask(id: string, assigneeUserId: string, version: number): Observable<WorkTask> {
    return this.http.post<WorkTask>(`/api/tasks/${id}/reassign`, { assigneeUserId, version });
  }

  // ---- Discussion -----------------------------------------------------------------------------------------------------

  comments(
    kind: DiscussionKind,
    targetId: string,
    page: number,
    pageSize: number,
  ): Observable<CommentPage> {
    const params = new HttpParams()
      .set('kind', kind)
      .set('targetId', targetId)
      .set('page', page)
      .set('pageSize', pageSize);
    return this.http.get<CommentPage>('/api/comments', { params });
  }

  addComment(input: NewCommentInput, clientRequestId: string): Observable<DiscussionComment> {
    return this.http.post<DiscussionComment>('/api/comments', { ...input, clientRequestId });
  }

  removeComment(id: string, reason: string, version: number): Observable<DiscussionComment> {
    return this.http.post<DiscussionComment>(`/api/comments/${id}/void`, { reason, version });
  }

  // ---- Notifications --------------------------------------------------------------------------------------------------

  notifications(
    unreadOnly: boolean,
    page: number,
    pageSize: number,
  ): Observable<PagedResult<AppNotification>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (unreadOnly) params = params.set('unread', true);
    return this.http.get<PagedResult<AppNotification>>('/api/notifications', { params });
  }

  /** Only the number unread, for the bell. */
  unreadCount(): Observable<NotificationCount> {
    return this.http.get<NotificationCount>('/api/notifications/count');
  }

  markRead(id: string): Observable<NotificationCount> {
    return this.http.post<NotificationCount>(`/api/notifications/${id}/read`, {});
  }

  markAllRead(): Observable<NotificationCount> {
    return this.http.post<NotificationCount>('/api/notifications/read-all', {});
  }

  // ---- Pickers --------------------------------------------------------------------------------------------------------

  /**
   * Active colleagues by name (at most 20). With no record, the people who can work on tasks; with a record, only the people who could
   * read it, so nobody is offered who would then be refused.
   */
  colleagues(
    search: string,
    target?: { kind: DiscussionKind; id: string },
  ): Observable<readonly Colleague[]> {
    let params = new HttpParams();
    if (search.trim()) params = params.set('search', search.trim());
    if (target) params = params.set('targetKind', target.kind).set('targetId', target.id);
    return this.http.get<readonly Colleague[]>('/api/users/lookup', { params });
  }

  /** Active projects for the task form and the filter. */
  projects(): Observable<readonly ProjectChoice[]> {
    return this.http.get<readonly ProjectChoice[]>('/api/projects/lookup');
  }
}
