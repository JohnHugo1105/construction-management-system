import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  DocumentCategory,
  DocumentDetailsInput,
  DocumentList,
  NewDocumentFileInput,
  NewDocumentLinkInput,
  NewVersionInput,
  ProjectDocument,
} from './documents.models';

/**
 * The project documents endpoints, one method each. A link goes as JSON; a file goes as multipart form data with the text fields
 * first and the file last (the server reads it as a stream and stops at the size limit). Every change answers with the whole document.
 */
@Injectable({ providedIn: 'root' })
export class DocumentsApi {
  private readonly http = inject(HttpClient);

  list(
    projectId: string,
    category: DocumentCategory | '',
    page: number,
    pageSize: number,
  ): Observable<DocumentList> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (category) params = params.set('category', category);
    return this.http.get<DocumentList>(`/api/projects/${projectId}/documents`, { params });
  }

  get(id: string): Observable<ProjectDocument> {
    return this.http.get<ProjectDocument>(`/api/documents/${id}`);
  }

  createLink(
    projectId: string,
    input: NewDocumentLinkInput,
    clientRequestId: string,
  ): Observable<ProjectDocument> {
    return this.http.post<ProjectDocument>(`/api/projects/${projectId}/documents`, {
      ...input,
      clientRequestId,
    });
  }

  createFile(
    projectId: string,
    input: NewDocumentFileInput,
    clientRequestId: string,
  ): Observable<ProjectDocument> {
    const form = fields({
      title: input.title,
      description: input.description,
      category: input.category,
      validUntil: input.validUntil,
      note: input.note,
      clientRequestId,
    });
    form.append('file', input.file, input.file.name);
    return this.http.post<ProjectDocument>(`/api/projects/${projectId}/documents`, form);
  }

  /** A newer version, a file or a link; `version` is the document's version the person opened. */
  addVersion(id: string, input: NewVersionInput, version: number): Observable<ProjectDocument> {
    const url = `/api/documents/${id}/versions`;
    if (input.file) {
      const form = fields({
        note: input.note,
        validUntil: input.validUntil,
        version: String(version),
      });
      form.append('file', input.file, input.file.name);
      return this.http.post<ProjectDocument>(url, form);
    }
    return this.http.post<ProjectDocument>(url, {
      url: input.url,
      note: input.note,
      validUntil: input.validUntil,
      version,
    });
  }

  update(id: string, input: DocumentDetailsInput, version: number): Observable<ProjectDocument> {
    return this.http.put<ProjectDocument>(`/api/documents/${id}`, { ...input, version });
  }

  voidDocument(id: string, reason: string, version: number): Observable<ProjectDocument> {
    return this.http.post<ProjectDocument>(`/api/documents/${id}/void`, { reason, version });
  }

  /** Where a version's file downloads from: a plain link, so the browser streams it to disk with the session cookie. */
  fileUrl(id: string, versionNumber: number): string {
    return `/api/documents/${id}/versions/${versionNumber}/file`;
  }
}

/** The text fields of an upload, empty ones left out, in a fixed order before the file. */
function fields(values: Record<string, string | null>): FormData {
  const form = new FormData();
  for (const [name, value] of Object.entries(values)) if (value) form.append(name, value);
  return form;
}
