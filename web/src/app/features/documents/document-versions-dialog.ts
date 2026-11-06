import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  output,
  viewChild,
} from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { asApiError } from '../../core/api/api-error';
import { SessionState } from '../../core/session/session-state';
import { ApiErrorAlert } from '../../shared/ui/api-error-alert/api-error-alert';
import { Button } from '../../shared/ui/button/button';
import { Dialog } from '../../shared/ui/dialog/dialog';
import { Skeleton } from '../../shared/ui/skeleton/skeleton';
import { DocumentsApi } from './documents-api';
import { openWords, versionLine, voidedWords } from './documents-presentation';
import { DocumentListItem } from './documents.models';

/** Every version of a document, newest first, each with its download or link. A new version never replaces the older ones. */
@Component({
  selector: 'app-document-versions-dialog',
  imports: [Dialog, Button, ApiErrorAlert, Skeleton],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-dialog [title]="'Versions of ' + document().title" (closed)="dismissed.emit()">
      <div class="stack">
        @if (error(); as problem) {
          <app-api-error-alert
            [error]="problem"
            [allowRetry]="true"
            reloadLabel="Try again"
            (reload)="resource.reload()"
          />
        } @else if (detail(); as info) {
          @if (info.status === 'Void') {
            <p class="text-small text-muted">{{ voided() }}</p>
          }
          <ul class="versions">
            @for (line of lines(); track line.number) {
              <li class="versions__item">
                <div class="versions__text">
                  <p>{{ line.words }}</p>
                  @if (line.note) {
                    <p class="text-small text-muted">{{ line.note }}</p>
                  }
                </div>
                <a
                  appButton
                  [href]="line.href"
                  [attr.download]="line.download ? '' : null"
                  [attr.target]="line.download ? null : '_blank'"
                  [attr.rel]="line.download ? null : 'noopener noreferrer'"
                  [attr.aria-label]="line.label"
                  >{{ line.open }}</a
                >
              </li>
            }
          </ul>
        } @else {
          <app-skeleton variant="card" [lines]="3" label="Loading versions" />
        }
      </div>
      <div dialogActions class="dialog-actions">
        <button appButton type="button" (click)="dialog().dismiss()">Close</button>
      </div>
    </app-dialog>
  `,
  styles: `
    .versions {
      display: grid;
      gap: var(--space-3);
      padding: 0;
      margin: 0;
      list-style: none;
    }

    .versions__item {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      justify-content: space-between;
      gap: var(--space-2) var(--space-3);
      padding-block: var(--space-2);
      border-bottom: 1px solid var(--color-border);
    }

    .versions__text {
      flex: 1 1 14rem;
      min-width: 0;
      overflow-wrap: anywhere;
    }
  `,
})
export class DocumentVersionsDialog {
  readonly document = input.required<DocumentListItem>();
  readonly dismissed = output<void>();

  private readonly api = inject(DocumentsApi);
  private readonly session = inject(SessionState);
  protected readonly dialog = viewChild.required(Dialog);

  protected readonly resource = rxResource({
    params: () => this.document().id,
    stream: ({ params }) => this.api.get(params),
  });

  protected readonly detail = computed(() => (this.resource.hasValue() ? this.resource.value() : null));
  protected readonly error = computed(() => {
    const failure = this.resource.error();
    return failure ? asApiError(failure) : null;
  });
  protected readonly voided = computed(() => {
    const detail = this.detail();
    return detail ? voidedWords(detail, this.session.timeZoneId()) : '';
  });
  protected readonly lines = computed(() => {
    const detail = this.detail();
    if (!detail) return [];
    const zone = this.session.timeZoneId();
    return detail.versions.map((version) => {
      const download = version.kind === 'File';
      return {
        number: version.number,
        words: versionLine(version, zone),
        note: version.note,
        open: openWords(version),
        download,
        href: download ? this.api.fileUrl(detail.id, version.number) : (version.url ?? ''),
        label: `${openWords(version)} version ${version.number} of ${detail.title}`,
      };
    });
  });
}
