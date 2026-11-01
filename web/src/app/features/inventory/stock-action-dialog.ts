import {
  DestroyRef,
  Directive,
  ElementRef,
  Injector,
  OnInit,
  afterNextRender,
  inject,
  input,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormGroup } from '@angular/forms';
import { Observable, firstValueFrom } from 'rxjs';
import { ApiError, asApiError } from '../../core/api/api-error';
import { SessionState } from '../../core/session/session-state';
import { applyServerErrors, injectFocusFirstInvalid } from '../../shared/forms/server-errors';
import { todayIn } from '../../shared/presentation/business-time';
import { Dialog } from '../../shared/ui/dialog/dialog';
import { InventoryApi } from './inventory-api';
import { InventoryItem } from './inventory.models';
import { RequestId } from './request-id';

/**
 * What every action on an item shares (move, receive, count, write off, reverse, retire): the item it acts on, one request id for
 * the whole form session, and one way of answering the server. A dialog supplies its `form` and what it sends in `send`.
 *
 * - The answer is the item as it now stands; the page takes it as the new truth (`done`).
 * - A field the server refuses is shown under that field. A rule ("Only 14 pc are there, but you asked for 15") or a conflict
 *   ("Someone else changed this", with a Reload button) is shown above the form in the server's own plain words.
 * - The request id and the typed values are kept after a failure and the id is renewed only after a success, so a retry or a
 *   double press can never do the action twice.
 */
@Directive()
export abstract class StockActionDialog implements OnInit {
  /** The item as last loaded. Its version goes back with the request, read at the moment of sending. */
  readonly item = input.required<InventoryItem>();

  /** The server answered: the item as it now stands. */
  readonly done = output<InventoryItem>();
  /** The dialog was dismissed without doing anything. */
  readonly dismissed = output<void>();
  /** The person chose to reload after someone else changed the item. */
  readonly reloaded = output<void>();

  protected readonly api = inject(InventoryApi);
  protected readonly session = inject(SessionState);
  protected readonly dialog = viewChild.required(Dialog);
  protected readonly formElement = viewChild<ElementRef<HTMLFormElement>>('formElement');

  protected readonly saving = signal(false);
  protected readonly error = signal<ApiError | null>(null);
  protected readonly extraMessages = signal<readonly string[]>([]);
  protected readonly requestId = new RequestId();

  /** Today where the company works, for date limits. */
  protected readonly todayDate = todayIn(this.session.timeZoneId());

  private readonly destroyRef = inject(DestroyRef);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly injector = inject(Injector);
  private readonly focusInvalid = injectFocusFirstInvalid();

  protected abstract readonly form: FormGroup;

  /** Names the form gives a field when the API calls it something else (`{ countedQuantity: 'counted' }`). */
  protected readonly serverFieldNames: Readonly<Record<string, string>> = {};

  /** The call this dialog makes. Use the version of the item passed in and the request id given. */
  protected abstract send(item: InventoryItem, requestId: string): Observable<InventoryItem>;

  ngOnInit(): void {
    this.prepare(this.item());
    // A rule such as "Only 14 pc are there" is about what was typed: once the person changes it, the message is out of date.
    // A conflict stays, because only Reload resolves it.
    this.form.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => {
      if (this.error()?.isRuleViolation) this.error.set(null);
    });
  }

  /** Called once the item is known: set defaults and anything that depends on the item. */
  protected prepare(_item: InventoryItem): void {
    // Most dialogs have nothing to set up.
  }

  protected cancel(): void {
    this.dialog().dismiss();
  }

  /** The person chose Reload after a conflict: ask the page for the fresh item, and let them send again. */
  protected reload(): void {
    this.error.set(null);
    this.reloaded.emit();
  }

  protected async submit(): Promise<void> {
    if (this.saving()) return;
    this.form.markAllAsTouched();
    if (this.form.invalid) {
      this.focusInvalid(this.formElement()?.nativeElement);
      return;
    }

    this.saving.set(true);
    this.error.set(null);
    this.extraMessages.set([]);
    try {
      const updated = await firstValueFrom(this.send(this.item(), this.requestId.current()));
      this.requestId.renew();
      // Done: the page takes the answer and removes the dialog. It stays busy until then, so a second press cannot send it again.
      this.done.emit(updated);
    } catch (failure) {
      const problem = asApiError(failure);
      const unmatched = applyServerErrors(this.form, problem, this.serverFieldNames);
      // A message under its field is enough for a field problem; a rule, a conflict or a failure goes above the form.
      if (
        problem.isConflict ||
        problem.isRuleViolation ||
        problem.isServerFailure ||
        unmatched.length > 0 ||
        !problem.isValidation
      ) {
        this.error.set(problem);
        this.extraMessages.set(unmatched);
        this.revealError();
      }
      this.focusInvalid(this.formElement()?.nativeElement);
      this.saving.set(false);
    }
  }

  /** The message sits above the form and the buttons below it; make sure it is on screen after a refusal. */
  private revealError(): void {
    this.reveal('.alert');
  }

  /** Scrolls something the dialog just drew into view once it exists (the dialog's body scrolls, its buttons stay put). */
  protected reveal(selector: string): void {
    afterNextRender(
      () => {
        const target = this.host.nativeElement.querySelector<HTMLElement>(selector);
        // Not every browser or test environment can scroll; the content is still there.
        if (typeof target?.scrollIntoView === 'function')
          target.scrollIntoView({ block: 'nearest' });
      },
      { injector: this.injector },
    );
  }
}
