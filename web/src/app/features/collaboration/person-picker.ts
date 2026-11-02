import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { Subject, catchError, debounceTime, of, startWith, switchMap } from 'rxjs';
import { SessionState } from '../../core/session/session-state';
import { FieldGroup } from '../../shared/ui/form-field/field-group';
import { CollaborationApi } from './collaboration-api';
import { Colleague } from './collaboration.models';

let nextPickerId = 0;

/**
 * Chooses the colleague a task is for: a name search over the people who can work on tasks (at most 20 shown), as one radio choice per
 * row so long names wrap instead of being cut. The signed-in person is marked "You": giving yourself a task is allowed. The chosen
 * person stays in the list while the search changes. Writes the chosen id to `control`.
 */
@Component({
  selector: 'app-person-picker',
  imports: [ReactiveFormsModule, FieldGroup],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-field-group
      [label]="label()"
      [control]="control()"
      requiredMessage="Choose who the task is for."
    >
      <div class="picker">
        <input
          class="input"
          type="search"
          autocomplete="off"
          placeholder="Search by name"
          [attr.aria-label]="'Search people by name for ' + label().toLowerCase()"
          [attr.aria-controls]="listId"
          (input)="search($event)"
        />
        @if (people().length > 0) {
          <div class="choice-list picker__list" [id]="listId">
            @for (person of people(); track person.id) {
              <label class="choice">
                <input type="radio" [formControl]="control()" [value]="person.id" [name]="listId" />
                <span class="choice__text">
                  <span class="choice__label">{{ person.displayName }}</span>
                  @if (person.id === myId()) {
                    <span class="choice__hint">You</span>
                  }
                </span>
              </label>
            }
          </div>
        } @else if (searched()) {
          <p class="text-small text-muted" [id]="listId">
            Nobody who can work on tasks matches that name.
          </p>
        }
      </div>
    </app-field-group>
  `,
  styles: `
    :host {
      display: block;
    }

    .picker {
      display: grid;
      gap: var(--space-2);
    }

    .picker__list {
      max-height: 18rem;
      overflow-y: auto;
      padding: 2px;
    }
  `,
})
export class PersonPicker {
  readonly control = input.required<FormControl<string>>();
  readonly label = input('Assign to');
  /** Someone not to offer (the person a task is already assigned to). */
  readonly exclude = input<string | null>(null);

  private readonly api = inject(CollaborationApi);
  private readonly session = inject(SessionState);
  private readonly searches = new Subject<string>();

  protected readonly listId = `person-picker-${++nextPickerId}`;
  protected readonly myId = computed(() => this.session.session()?.userId ?? '');
  protected readonly searched = signal(false);

  /** The people last chosen, so the choice stays on screen while another name is searched. */
  private readonly chosen = signal<Colleague | null>(null);

  private readonly found = toSignal(
    this.searches.pipe(
      debounceTime(200),
      startWith(''),
      switchMap((search) =>
        this.api.colleagues(search).pipe(catchError(() => of([] as readonly Colleague[]))),
      ),
    ),
    { initialValue: [] as readonly Colleague[] },
  );

  protected readonly people = computed(() => {
    const exclude = this.exclude();
    const found = this.found().filter((person) => person.id !== exclude);
    const chosen = this.chosen();
    return chosen && !found.some((person) => person.id === chosen.id) ? [chosen, ...found] : found;
  });

  constructor() {
    // Remember the chosen person by name, so they stay listed when the search no longer matches them.
    this.searches.pipe(takeUntilDestroyed()).subscribe(() => {
      const id = this.control().value;
      const person = this.people().find((item) => item.id === id) ?? null;
      if (person) this.chosen.set(person);
    });
  }

  protected search(event: Event): void {
    this.searched.set(true);
    this.searches.next((event.target as HTMLInputElement).value);
  }
}
