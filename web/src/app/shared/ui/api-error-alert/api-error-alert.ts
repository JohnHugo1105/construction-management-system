import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { ApiError } from '../../../core/api/api-error';
import { Alert, AlertTone } from '../alert/alert';
import { Button } from '../button/button';

/**
 * Shows an ApiError the way every screen should, so the wording is the same everywhere:
 * - someone else changed the record: a warning with a Reload button (emits `reload`; the page refetches);
 * - the server failed or could not be reached: a calm message with "Reference: ..." to quote to an administrator;
 * - anything else: the API's own plain-words message.
 * Raw server text and status codes are never shown (ApiError already holds only safe text).
 * `extra` lists field messages that belong to no field on the form.
 */
@Component({
  selector: 'app-api-error-alert',
  imports: [Alert, Button],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (error(); as problem) {
      <app-alert [tone]="tone()" [title]="problem.title">
        {{ problem.detail }}
        @for (message of extra(); track message) {
          <span class="api-error__extra">{{ message }}</span>
        }
        @if (problem.isServerFailure && problem.traceId) {
          <span class="api-error__reference">Reference: {{ problem.traceId }}</span>
        }
        @if (problem.isConflict || allowRetry()) {
          <div alertActions>
            <button appButton type="button" (click)="reload.emit()">{{ reloadLabel() }}</button>
          </div>
        }
      </app-alert>
    }
  `,
  styles: `
    .api-error__extra,
    .api-error__reference {
      display: block;
      margin-top: var(--space-1);
    }

    .api-error__reference {
      font-size: var(--text-sm);
      color: var(--color-text-muted);
    }
  `,
})
export class ApiErrorAlert {
  readonly error = input<ApiError | null>(null);
  /** Messages for fields this form does not have. */
  readonly extra = input<readonly string[]>([]);
  readonly reloadLabel = input('Reload');
  /** Offer the button for any failure (a list that could not load), not only after a conflict. */
  readonly allowRetry = input(false);

  /** The person chose to reload after a conflict, or to try again. */
  readonly reload = output<void>();

  protected readonly tone = computed<AlertTone>(() => {
    const problem = this.error();
    if (!problem) return 'danger';
    return problem.isConflict || problem.isRuleViolation ? 'warning' : 'danger';
  });
}
