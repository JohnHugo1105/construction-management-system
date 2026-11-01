import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { PagedResult } from '../../core/api/paged-result';
import {
  ArchiveRequest,
  Project,
  ProjectDetails,
  ProjectListItem,
  ProjectListParams,
  ProjectTransition,
  SubProjectDetails,
} from './projects.models';

/** The project endpoints, one method each. */
@Injectable({ providedIn: 'root' })
export class ProjectsApi {
  private readonly http = inject(HttpClient);

  list(params: ProjectListParams): Observable<PagedResult<ProjectListItem>> {
    let query = new HttpParams().set('page', params.page).set('pageSize', params.pageSize);
    if (params.search) query = query.set('search', params.search);
    if (params.status) query = query.set('status', params.status);
    return this.http.get<PagedResult<ProjectListItem>>('/api/projects', { params: query });
  }

  get(id: string): Observable<Project> {
    return this.http.get<Project>(`/api/projects/${id}`);
  }

  create(details: ProjectDetails): Observable<Project> {
    return this.http.post<Project>('/api/projects', details);
  }

  update(id: string, details: ProjectDetails, version: number): Observable<Project> {
    return this.http.put<Project>(`/api/projects/${id}`, { ...details, version });
  }

  transition(id: string, step: ProjectTransition, version: number): Observable<Project> {
    return this.http.post<Project>(`/api/projects/${id}/${step}`, { version });
  }

  archive(id: string, request: ArchiveRequest): Observable<Project> {
    return this.http.post<Project>(`/api/projects/${id}/archive`, request);
  }

  addSubProject(
    projectId: string,
    details: SubProjectDetails,
    version: number,
  ): Observable<Project> {
    return this.http.post<Project>(`/api/projects/${projectId}/sub-projects`, {
      ...details,
      version,
    });
  }

  updateSubProject(
    projectId: string,
    subProjectId: string,
    details: SubProjectDetails,
    version: number,
  ): Observable<Project> {
    return this.http.put<Project>(`/api/projects/${projectId}/sub-projects/${subProjectId}`, {
      ...details,
      version,
    });
  }

  archiveSubProject(projectId: string, subProjectId: string, version: number): Observable<Project> {
    return this.http.post<Project>(
      `/api/projects/${projectId}/sub-projects/${subProjectId}/archive`,
      { version },
    );
  }

  restoreSubProject(projectId: string, subProjectId: string, version: number): Observable<Project> {
    return this.http.post<Project>(
      `/api/projects/${projectId}/sub-projects/${subProjectId}/restore`,
      { version },
    );
  }
}
