import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  computed,
  effect,
  inject,
  input,
  signal,
  untracked,
  viewChild,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Subject, catchError, debounceTime, firstValueFrom, of, switchMap } from 'rxjs';
import { ApiError, asApiError } from '../../core/api/api-error';
import { SessionState } from '../../core/session/session-state';
import { formatDateTime } from '../../shared/presentation/business-time';
import { ApiErrorAlert } from '../../shared/ui/api-error-alert/api-error-alert';
import { Button } from '../../shared/ui/button/button';
import { Control } from '../../shared/ui/form-field/control';
import { FormField } from '../../shared/ui/form-field/form-field';
import { Skeleton } from '../../shared/ui/skeleton/skeleton';
import { ClientRequestId } from '../costing/client-request-id';
import { describeFormFailure } from '../workforce/form-failure';
import { CollaborationApi } from './collaboration-api';
import {
  MentionQuery,
  insertMention,
  mentionQueryAt,
  mentionsStillIn,
  splitMentions,
} from './collaboration-presentation';
import { Colleague, DiscussionComment, DiscussionKind } from './collaboration.models';
import { RemoveCommentDialog } from './remove-comment-dialog';

/** How many comments one read brings. Older ones come with "Show older comments". */
export const DISCUSSION_PAGE_SIZE = 20;

let nextDiscussionId = 0;

/**
 * The discussion on one record: a purchase order, a bill, a change order, a subcontract, a project or a task. The record's own rule
 * decides who reads it (the server checks it), and only people who work on tasks may write; on an archived project it is read-only and
 * says why. Typing "@" offers the colleagues who can see the record; each one mentioned is told in the app. A comment is never edited:
 * its author or an Administrator removes it with a reason, and "Removed by ..." stays in its place. Nothing here carries an amount.
 *
 *     <app-discussion kind="PurchaseOrder" [targetId]="order.id" />
 */
@Component({
  selector: 'app-discussion',
  imports: [
    ReactiveFormsModule,
    ApiErrorAlert,
    Button,
    Control,
    FormField,
    Skeleton,
    RemoveCommentDialog,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="discussion" [attr.aria-labelledby]="headingId">
      <h2 class="discussion__title" [id]="headingId">Discussion</h2>

      @if (loading()) {
        <app-skeleton [lines]="2" label="Loading the discussion" />
      } @else if (loadError(); as problem) {
        <app-api-error-alert
          [error]="problem"
          [allowRetry]="!problem.isNotFound && !problem.isForbidden"
          reloadLabel="Try again"
          (reload)="load()"
        />
      } @else {
        @if (canWrite()) {
          <form class="composer" [formGroup]="form" (ngSubmit)="post()">
            @if (postError(); as problem) {
              <app-api-error-alert [error]="problem" [extra]="postExtra()" />
            }
            <app-form-field
              label="Add a comment"
              hint="Type @ to mention someone who can see this. They are told in the app."
              [control]="form.controls.text"
              requiredMessage="Write your comment first."
            >
              <textarea
                appControl
                #box
                formControlName="text"
                rows="3"
                maxlength="1000"
                (input)="followCaret()"
                (click)="followCaret()"
                (keydown)="onBoxKey($event)"
              ></textarea>
            </app-form-field>
            @if (mentionQuery() && suggestions().length > 0) {
              <ul class="suggestions" aria-label="People you can mention">
                @for (person of suggestions(); track person.id) {
                  <li>
                    <button
                      type="button"
                      class="suggestions__item"
                      (click)="choose(person)"
                      (keydown)="onSuggestionKey($event, $index)"
                    >
                      {{ person.displayName }}
                    </button>
                  </li>
                }
              </ul>
            }
            <p class="visually-hidden" aria-live="polite">{{ suggestionNote() }}</p>
            @if (mentioned().length > 0) {
              <p class="text-small text-muted">Telling {{ mentionedNames() }} in the app.</p>
            }
            <div class="composer__actions">
              <button appButton type="submit" [busy]="posting()">Post comment</button>
            </div>
          </form>
        } @else if (readOnlyReason(); as reason) {
          <p class="text-small text-muted">{{ reason }}</p>
        }

        @if (comments().length > 0) {
          <ul class="thread">
            @for (comment of shown(); track comment.id) {
              <li class="thread__item" [class.thread__item--removed]="comment.isVoided">
                <p class="thread__meta">
                  <span class="thread__author">{{ comment.author }}</span>
                  <span class="text-muted">{{ comment.when }}</span>
                </p>
                @if (comment.isVoided) {
                  <p class="text-small text-muted">{{ comment.removedNote }}</p>
                } @else {
                  <p class="thread__text">
                    @for (part of comment.parts; track $index) {
                      @if (part.mention) {
                        <strong>{{ part.text }}</strong>
                      } @else {
                        {{ part.text }}
                      }
                    }
                  </p>
                  @if (comment.source.canRemove) {
                    <button
                      appButton
                      variant="ghost"
                      type="button"
                      class="thread__remove"
                      [attr.aria-label]="'Remove comment by ' + comment.author"
                      (click)="removing.set(comment.source)"
                    >
                      Remove
                    </button>
                  }
                }
              </li>
            }
          </ul>
          @if (hasOlder()) {
            <button appButton type="button" [busy]="loadingOlder()" (click)="loadOlder()">
              Show older comments
            </button>
          }
        } @else {
          <p class="text-small text-muted">
            {{
              canWrite()
                ? 'No comments yet. Ask a question or leave a note for the team.'
                : 'No comments yet.'
            }}
          </p>
        }
      }
    </section>

    @if (removing(); as comment) {
      <app-remove-comment-dialog
        [comment]="comment"
        (removed)="removed($event)"
        (dismissed)="removing.set(null)"
        (reloaded)="removing.set(null); load()"
      />
    }
  `,
  styles: `
    :host {
      display: block;
    }

    .discussion {
      display: grid;
      gap: var(--space-3);
    }

    .discussion__title {
      font-size: var(--text-lg);
    }

    .composer {
      display: grid;
      gap: var(--space-2);
    }

    .composer__actions {
      display: flex;
      justify-content: flex-end;
    }

    @media (max-width: 47.99rem) {
      .composer__actions .btn {
        flex: 1 1 100%;
      }
    }

    .suggestions {
      display: grid;
      margin: 0;
      padding: var(--space-1);
      list-style: none;
      border: 1px solid var(--color-border-strong);
      border-radius: var(--radius-md);
      background: var(--color-surface);
      box-shadow: var(--shadow-1);
    }

    .suggestions__item {
      width: 100%;
      min-height: var(--touch-target);
      padding: 0 var(--space-3);
      border: 0;
      border-radius: var(--radius-md);
      background: transparent;
      color: var(--color-text);
      font: inherit;
      text-align: left;
      cursor: pointer;
    }

    .suggestions__item:hover,
    .suggestions__item:focus-visible {
      background: var(--color-surface-2);
    }

    .suggestions__item:focus-visible {
      outline: var(--focus-ring-width) solid var(--color-focus);
      outline-offset: -2px;
    }

    .thread {
      display: grid;
      margin: 0;
      padding: 0;
      list-style: none;
    }

    .thread__item {
      display: grid;
      gap: var(--space-1);
      padding: var(--space-3) 0;
      border-bottom: 1px solid var(--color-border);
      overflow-wrap: anywhere;
    }

    .thread__meta {
      display: flex;
      flex-wrap: wrap;
      gap: 0 var(--space-2);
      font-size: var(--text-sm);
    }

    .thread__author {
      font-weight: 650;
    }

    .thread__text {
      white-space: pre-line;
    }

    .thread__remove {
      justify-self: start;
    }
  `,
})
export class Discussion {
  readonly kind = input.required<DiscussionKind>();
  readonly targetId = input.required<string>();

  private readonly api = inject(CollaborationApi);
  private readonly session = inject(SessionState);
  private readonly box = viewChild<ElementRef<HTMLTextAreaElement>>('box');
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);

  protected readonly headingId = `discussion-${++nextDiscussionId}`;

  protected readonly form = new FormGroup({
    text: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(1000)],
    }),
  });

  protected readonly loading = signal(true);
  protected readonly loadError = signal<ApiError | null>(null);
  protected readonly comments = signal<readonly DiscussionComment[]>([]);
  private readonly total = signal(0);
  private readonly pagesRead = signal(0);
  protected readonly canWrite = signal(false);
  protected readonly readOnlyReason = signal<string | null>(null);
  protected readonly loadingOlder = signal(false);

  protected readonly posting = signal(false);
  protected readonly postError = signal<ApiError | null>(null);
  protected readonly postExtra = signal<readonly string[]>([]);
  private readonly requestId = new ClientRequestId();

  protected readonly removing = signal<DiscussionComment | null>(null);

  protected readonly mentionQuery = signal<MentionQuery | null>(null);
  protected readonly suggestions = signal<readonly Colleague[]>([]);
  private readonly picked = signal<readonly Colleague[]>([]);
  private readonly text = signal('');
  private readonly searches = new Subject<string>();

  /** The people picked whose "@Name" is still in the words: they are the ones who will be told. */
  protected readonly mentioned = computed(() => mentionsStillIn(this.text(), this.picked()));
  protected readonly mentionedNames = computed(() =>
    this.mentioned()
      .map((person) => person.displayName)
      .join(', '),
  );

  protected readonly suggestionNote = computed(() => {
    if (!this.mentionQuery()) return '';
    const count = this.suggestions().length;
    if (count === 0) return 'Nobody who can see this matches.';
    return count === 1
      ? '1 person matches. Press the down arrow to choose.'
      : `${count} people match. Press the down arrow to choose.`;
  });

  protected readonly hasOlder = computed(() => this.comments().length < this.total());

  protected readonly shown = computed(() => {
    const zone = this.session.timeZoneId();
    return this.comments().map((comment) => {
      const author = comment.authorName ?? 'Someone';
      return {
        id: comment.id,
        source: comment,
        author,
        when: formatDateTime(comment.createdAtUtc, zone),
        isVoided: comment.isVoided,
        removedNote: `Removed by ${comment.voidedByName ?? 'someone'}${comment.voidedAtUtc ? `, ${formatDateTime(comment.voidedAtUtc, zone)}` : ''}.`,
        parts: splitMentions(comment.text ?? '', comment.mentions),
      };
    });
  });

  constructor() {
    effect(() => {
      const kind = this.kind();
      const targetId = this.targetId();
      if (kind && targetId) untracked(() => void this.load());
    });

    this.form.controls.text.valueChanges
      .pipe(takeUntilDestroyed())
      .subscribe((value) => this.text.set(value));

    this.searches
      .pipe(
        debounceTime(200),
        switchMap((search) =>
          this.api
            .colleagues(search, { kind: this.kind(), id: this.targetId() })
            .pipe(catchError(() => of([] as readonly Colleague[]))),
        ),
        takeUntilDestroyed(),
      )
      .subscribe((people) => {
        const me = this.session.session()?.userId;
        this.suggestions.set(people.filter((person) => person.id !== me).slice(0, 8));
      });
  }

  /** Reads the newest comments again, from the start. */
  async load(): Promise<void> {
    const kind = this.kind();
    const targetId = this.targetId();
    this.loading.set(true);
    this.loadError.set(null);
    try {
      const page = await firstValueFrom(this.api.comments(kind, targetId, 1, DISCUSSION_PAGE_SIZE));
      if (kind !== this.kind() || targetId !== this.targetId()) return;
      this.comments.set(page.items);
      this.total.set(page.total);
      this.pagesRead.set(1);
      this.canWrite.set(page.canWrite);
      this.readOnlyReason.set(page.readOnlyReason);
    } catch (failure) {
      this.loadError.set(asApiError(failure));
    } finally {
      this.loading.set(false);
    }
  }

  protected async loadOlder(): Promise<void> {
    if (this.loadingOlder()) return;
    this.loadingOlder.set(true);
    try {
      const next = this.pagesRead() + 1;
      const page = await firstValueFrom(
        this.api.comments(this.kind(), this.targetId(), next, DISCUSSION_PAGE_SIZE),
      );
      const known = new Set(this.comments().map((comment) => comment.id));
      this.comments.update((list) => [
        ...list,
        ...page.items.filter((item) => !known.has(item.id)),
      ]);
      this.total.set(page.total);
      this.pagesRead.set(next);
    } catch (failure) {
      this.loadError.set(asApiError(failure));
    } finally {
      this.loadingOlder.set(false);
    }
  }

  protected async post(): Promise<void> {
    if (this.posting()) return;
    const control = this.form.controls.text;
    const text = control.value.trim();
    if (!text) control.setValue('');
    control.markAsTouched();
    if (this.form.invalid) {
      this.box()?.nativeElement.focus();
      return;
    }
    this.posting.set(true);
    this.postError.set(null);
    this.postExtra.set([]);
    try {
      const comment = await firstValueFrom(
        this.api.addComment(
          {
            kind: this.kind(),
            targetId: this.targetId(),
            text,
            mentionUserIds: mentionsStillIn(text, this.picked()).map((person) => person.id),
          },
          this.requestId.current,
        ),
      );
      this.requestId.renew();
      this.comments.update((list) => [comment, ...list.filter((item) => item.id !== comment.id)]);
      this.total.update((count) => count + 1);
      this.form.reset({ text: '' });
      this.picked.set([]);
      this.closeSuggestions();
    } catch (failure) {
      const problem = asApiError(failure);
      const outcome = describeFormFailure(this.form, problem);
      this.postError.set(outcome.alert);
      this.postExtra.set(outcome.extra);
      // The discussion became read-only (the project was archived) or the record went away: show the server's answer.
      if (problem.isRuleViolation || problem.isNotFound) void this.load();
    } finally {
      this.posting.set(false);
    }
  }

  protected removed(comment: DiscussionComment): void {
    this.removing.set(null);
    this.comments.update((list) => list.map((item) => (item.id === comment.id ? comment : item)));
  }

  /** Offers colleagues while the caret sits in an "@" word. */
  protected followCaret(): void {
    const box = this.box()?.nativeElement;
    if (!box) return;
    const query = mentionQueryAt(box.value, box.selectionStart ?? box.value.length);
    this.mentionQuery.set(query);
    if (query) this.searches.next(query.search);
    else this.suggestions.set([]);
  }

  protected choose(person: Colleague): void {
    const box = this.box()?.nativeElement;
    const query = this.mentionQuery();
    if (!box || !query) return;
    // The query was read with the caret at its end; a click on the list may have moved focus but not the words.
    const end = query.start + 1 + query.search.length;
    const next = insertMention(box.value, query, end, person.displayName);
    this.form.controls.text.setValue(next.text);
    this.picked.update((list) => [...list, person]);
    this.closeSuggestions();
    box.focus();
    box.setSelectionRange(next.caret, next.caret);
  }

  protected onBoxKey(event: KeyboardEvent): void {
    if (!this.mentionQuery() || this.suggestions().length === 0) return;
    if (event.key === 'ArrowDown') {
      event.preventDefault();
      this.focusSuggestion(0);
    } else if (event.key === 'Escape') {
      event.preventDefault();
      this.closeSuggestions();
    }
  }

  protected onSuggestionKey(event: KeyboardEvent, index: number): void {
    if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
      event.preventDefault();
      const count = this.suggestions().length;
      this.focusSuggestion((index + (event.key === 'ArrowDown' ? 1 : count - 1)) % count);
    } else if (event.key === 'Escape') {
      event.preventDefault();
      this.closeSuggestions();
      this.box()?.nativeElement.focus();
    }
  }

  private focusSuggestion(index: number): void {
    this.host.nativeElement
      .querySelectorAll<HTMLButtonElement>('.suggestions__item')
      [index]?.focus();
  }

  private closeSuggestions(): void {
    this.mentionQuery.set(null);
    this.suggestions.set([]);
  }
}
