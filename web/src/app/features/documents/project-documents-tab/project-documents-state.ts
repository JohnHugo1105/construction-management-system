import { Injectable, computed, inject, linkedSignal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { asApiError } from '../../../core/api/api-error';
import { DEFAULT_PAGE_SIZE } from '../../../core/api/paged-result';
import { injectListQuery, listField } from '../../../shared/list-query/list-query';
import { ProjectContext } from '../../projects/project-context';
import { DocumentsApi } from '../documents-api';
import { DocumentCategory, DocumentList } from '../documents.models';

const KINDS: readonly ('' | DocumentCategory)[] = [
  '',
  'Contract',
  'Drawing',
  'Permit',
  'Insurance',
  'Certificate',
  'Photo',
  'Report',
  'Other',
];

/**
 * The open project's documents, one page at a time, with the kind and the page in the address (`?kind=Permit&page=2`). It also carries
 * what the server says about uploads (whether file storage works now), which the add and new-version dialogs read. Provide it on the
 * Documents tab.
 */
@Injectable()
export class ProjectDocumentsState {
  private readonly api = inject(DocumentsApi);
  private readonly context = inject(ProjectContext);

  readonly query = injectListQuery({ kind: listField.choice(KINDS, '') });
  readonly pageSize = DEFAULT_PAGE_SIZE;

  private readonly resource = rxResource({
    params: () => {
      const projectId = this.context.projectId();
      const { kind, page } = this.query.values();
      return projectId ? { projectId, kind, page } : undefined;
    },
    stream: ({ params }) =>
      this.api.list(params.projectId, params.kind, params.page, DEFAULT_PAGE_SIZE),
  });

  /** The latest page; the one before it while the next loads. */
  readonly result = linkedSignal<DocumentList | undefined, DocumentList | undefined>({
    source: () => (this.resource.hasValue() ? this.resource.value() : undefined),
    computation: (value, previous) => value ?? previous?.value,
  });

  readonly items = computed(() => this.result()?.items ?? []);
  readonly total = computed(() => this.result()?.total ?? 0);
  readonly uploads = computed(() => this.result()?.uploads ?? null);
  readonly loadingFirst = computed(() => this.resource.isLoading() && this.result() === undefined);
  readonly refreshing = computed(() => this.resource.isLoading() && this.result() !== undefined);
  readonly error = computed(() => {
    const failure = this.resource.error();
    return failure ? asApiError(failure) : null;
  });

  reload(): void {
    this.resource.reload();
  }
}
