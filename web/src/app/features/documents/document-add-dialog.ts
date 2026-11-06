import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  OnInit,
  computed,
  inject,
  input,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import {
  AbstractControl,
  FormControl,
  FormGroup,
  ReactiveFormsModule,
  ValidationErrors,
  Validators,
} from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { ApiError, asApiError } from '../../core/api/api-error';
import { Permission } from '../../core/session/permissions';
import { SessionState } from '../../core/session/session-state';
import { injectFocusFirstInvalid } from '../../shared/forms/server-errors';
import { formatDate } from '../../shared/presentation/business-time';
import { ApiErrorAlert } from '../../shared/ui/api-error-alert/api-error-alert';
import { Button } from '../../shared/ui/button/button';
import { Dialog } from '../../shared/ui/dialog/dialog';
import { Control } from '../../shared/ui/form-field/control';
import { FieldGroup } from '../../shared/ui/form-field/field-group';
import { FormField } from '../../shared/ui/form-field/form-field';
import { ClientRequestId } from '../costing/client-request-id';
import { describeFormFailure } from '../workforce/form-failure';
import { DocumentsApi } from './documents-api';
import {
  allowedTypesWords,
  categoryChoices,
  fileProblem,
  isDated,
  sizeWords,
} from './documents-presentation';
import {
  DocumentCategory,
  DocumentListItem,
  DocumentUploads,
  ProjectDocument,
} from './documents.models';

type Method = 'file' | 'link';

const HTTPS = /^https:\/\/\S+$/i;

/**
 * Adds a document to a project, or a newer version of one (`document` set). The person uploads a file or gives a link to where it is
 * kept. When file storage is not working the file choice is switched off with the reason, and the link choice is chosen for them, so
 * nothing they typed is lost. A permit, an insurance policy or a certificate asks for its valid-until date; a new version of one can
 * carry the renewed date. The server checks every file again and looks inside it; this dialog only warns early about a wrong type or
 * a file that is too big.
 */
@Component({
  selector: 'app-document-add-dialog',
  imports: [ReactiveFormsModule, Dialog, Button, FormField, FieldGroup, Control, ApiErrorAlert],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-dialog [title]="title()" [locked]="saving()" (closed)="dismissed.emit()">
      <div class="stack">
        @if (error(); as problem) {
          <app-api-error-alert
            [error]="problem"
            [extra]="extra()"
            [allowRetry]="problem.isConflict"
            reloadLabel="Reload documents"
            (reload)="reloaded.emit()"
          />
        }
        <form id="document-add-form" class="form" [formGroup]="form" (ngSubmit)="submit()" #formElement>
          @if (!document()) {
            <app-form-field
              label="Kind of document"
              [control]="form.controls.category"
              requiredMessage="Choose what kind of document it is."
            >
              <select appControl formControlName="category" data-autofocus>
                @for (choice of categories(); track choice.value) {
                  <option [value]="choice.value">{{ choice.label }}</option>
                }
              </select>
            </app-form-field>
            <app-form-field
              label="Title"
              hint="For example, Ground floor plan or Building permit."
              [control]="form.controls.title"
              requiredMessage="Enter the document's title."
            >
              <input appControl formControlName="title" maxlength="120" autocomplete="off" />
            </app-form-field>
          }

          <app-field-group label="How do you want to add it?" [control]="form.controls.method">
            <div class="choice-list">
              <label class="choice" [class.choice--off]="!uploads().available">
                @if (uploads().available) {
                  <input type="radio" formControlName="method" value="file" />
                } @else {
                  <!-- Not tied to the form: it can't be chosen while file storage isn't working. -->
                  <input type="radio" name="document-method-off" disabled />
                }
                <span class="choice__text">
                  <span class="choice__label">Upload a file</span>
                  <span class="choice__hint">{{ fileChoiceHint() }}</span>
                </span>
              </label>
              <label class="choice">
                <input type="radio" formControlName="method" value="link" />
                <span class="choice__text">
                  <span class="choice__label">Add a link instead</span>
                  <span class="choice__hint">To where it is kept, for example a shared drive.</span>
                </span>
              </label>
            </div>
          </app-field-group>

          @if (method() === 'file') {
            <app-form-field
              label="File"
              [hint]="fileHint()"
              [control]="form.controls.file"
            >
              <input
                appControl
                class="file-input"
                type="file"
                [accept]="accept()"
                (change)="choose($event)"
              />
            </app-form-field>
          } @else {
            <app-form-field
              label="Link"
              hint="The full address, starting with https://"
              [control]="form.controls.url"
            >
              <input
                appControl
                type="url"
                inputmode="url"
                formControlName="url"
                maxlength="500"
                autocomplete="off"
                placeholder="https://"
              />
            </app-form-field>
          }

          @if (dated()) {
            <app-form-field
              [label]="document() ? 'New valid-until date' : 'Valid until'"
              [optional]="true"
              [hint]="validUntilHint()"
              [control]="form.controls.validUntil"
            >
              <input appControl type="date" formControlName="validUntil" />
            </app-form-field>
          }

          @if (document()) {
            <app-form-field label="What changed?" [optional]="true" [control]="form.controls.note">
              <textarea appControl formControlName="note" rows="2" maxlength="300"></textarea>
            </app-form-field>
          } @else {
            <app-form-field
              label="Description"
              [optional]="true"
              [control]="form.controls.description"
            >
              <textarea appControl formControlName="description" rows="2" maxlength="300"></textarea>
            </app-form-field>
          }
        </form>
      </div>
      <div dialogActions class="dialog-actions">
        <button
          appButton
          type="button"
          [attr.disabled]="saving() ? '' : null"
          (click)="dialog().dismiss()"
        >
          Cancel
        </button>
        <button appButton variant="primary" type="submit" form="document-add-form" [busy]="saving()">
          {{ confirmWords() }}
        </button>
      </div>
    </app-dialog>
  `,
  styles: `
    .choice--off {
      cursor: not-allowed;
      background: var(--color-surface-2);
    }

    .file-input {
      padding-block: 8px;
    }
  `,
})
export class DocumentAddDialog implements OnInit {
  /** The document a new version is for; null adds a new document. */
  readonly document = input<DocumentListItem | null>(null);
  /** The project a new document goes on. */
  readonly projectId = input.required<string>();
  /** What the server said about uploads with the list: whether file storage works now, the size limit and the types. */
  readonly uploads = input.required<DocumentUploads>();

  readonly saved = output<ProjectDocument>();
  readonly dismissed = output<void>();
  readonly reloaded = output<void>();

  private readonly api = inject(DocumentsApi);
  private readonly session = inject(SessionState);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly dialog = viewChild.required(Dialog);
  private readonly formElement = viewChild<ElementRef<HTMLFormElement>>('formElement');
  private readonly focusInvalid = injectFocusFirstInvalid();
  private readonly requestId = new ClientRequestId();

  protected readonly categories = computed(() =>
    categoryChoices(this.session.can(Permission.costsRead)),
  );

  protected readonly form = new FormGroup({
    category: new FormControl<DocumentCategory>('Drawing', { nonNullable: true }),
    title: new FormControl('', { nonNullable: true }),
    method: new FormControl<Method>('file', { nonNullable: true }),
    file: new FormControl<File | null>(null, {
      validators: [(control) => this.checkFile(control)],
    }),
    url: new FormControl('', {
      nonNullable: true,
      validators: [(control) => this.checkUrl(control)],
    }),
    validUntil: new FormControl('', { nonNullable: true }),
    description: new FormControl('', {
      nonNullable: true,
      validators: [Validators.maxLength(300)],
    }),
    note: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(300)] }),
  });

  protected readonly method = toSignal(this.form.controls.method.valueChanges, {
    initialValue: 'file' as Method,
  });
  private readonly category = toSignal(this.form.controls.category.valueChanges, {
    initialValue: 'Drawing' as DocumentCategory,
  });

  protected readonly saving = signal(false);
  protected readonly error = signal<ApiError | null>(null);
  protected readonly extra = signal<readonly string[]>([]);

  protected readonly title = computed(() => {
    const document = this.document();
    return document ? `Add a new version of ${document.title}` : 'Add a document';
  });
  protected readonly confirmWords = computed(() => {
    if (this.document()) return 'Add version';
    return this.method() === 'file' ? 'Upload document' : 'Add document';
  });
  protected readonly dated = computed(() => isDated(this.document()?.category ?? this.category()));
  protected readonly accept = computed(() =>
    this.uploads()
      .allowedExtensions.map((extension) => `.${extension}`)
      .join(','),
  );
  protected readonly fileChoiceHint = computed(() => {
    const uploads = this.uploads();
    return uploads.available
      ? `Up to ${sizeWords(uploads.maximumBytes)}. It is offered to others only as a download.`
      : uploads.message;
  });
  protected readonly fileHint = computed(
    () => `${allowedTypesWords(this.uploads().allowedExtensions)}.`,
  );
  protected readonly validUntilHint = computed(() => {
    const current = this.document()?.validUntil;
    if (this.document())
      return current
        ? `Leave it empty to keep ${formatDate(current)}.`
        : 'If the renewed document has a date it stops being valid.';
    return 'The last day it is valid. It is listed under Needs your action 30 days before.';
  });

  ngOnInit(): void {
    const document = this.document();
    if (!document) {
      this.form.controls.title.addValidators([Validators.required, Validators.maxLength(120)]);
      this.form.controls.title.updateValueAndValidity();
    }
    if (!this.uploads().available) this.form.controls.method.setValue('link');
    // The file and link checks read the method, which the form did not have yet when they first ran.
    this.form.controls.file.updateValueAndValidity();
    this.form.controls.url.updateValueAndValidity();
    // Changing the method or the kind re-checks the fields that depend on them; what was typed stays.
    this.form.controls.method.valueChanges
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => {
        this.form.controls.file.updateValueAndValidity();
        this.form.controls.url.updateValueAndValidity();
      });
  }

  protected choose(event: Event): void {
    const file = (event.target as HTMLInputElement).files?.[0] ?? null;
    const control = this.form.controls.file;
    control.setValue(file);
    control.markAsTouched();
    // The file's name is a good first title; the person can change it.
    const title = this.form.controls.title;
    if (file && !this.document() && !title.value.trim())
      title.setValue(file.name.replace(/\.[^.]+$/, '').slice(0, 120));
  }

  protected async submit(): Promise<void> {
    if (this.saving()) return;
    this.form.markAllAsTouched();
    if (this.form.invalid) {
      this.focusInvalid(this.formElement()?.nativeElement);
      return;
    }
    const value = this.form.getRawValue();
    const dated = this.dated();
    const validUntil = dated && value.validUntil ? value.validUntil : null;
    const note = value.note.trim() || null;
    const byFile = value.method === 'file' && value.file !== null;
    const document = this.document();

    this.saving.set(true);
    this.error.set(null);
    this.extra.set([]);
    try {
      let saved: ProjectDocument;
      if (document) {
        saved = await firstValueFrom(
          this.api.addVersion(
            document.id,
            {
              file: byFile ? value.file : null,
              url: byFile ? null : value.url.trim(),
              note,
              validUntil,
            },
            document.version,
          ),
        );
      } else {
        const details = {
          title: value.title.trim(),
          description: value.description.trim() || null,
          category: value.category,
          validUntil,
          note: null,
        };
        saved = byFile
          ? await firstValueFrom(
              this.api.createFile(this.projectId(), { ...details, file: value.file! }, this.requestId.current),
            )
          : await firstValueFrom(
              this.api.createLink(
                this.projectId(),
                { ...details, url: value.url.trim() },
                this.requestId.current,
              ),
            );
        this.requestId.renew();
      }
      this.saved.emit(saved);
    } catch (failure) {
      const outcome = describeFormFailure(this.form, asApiError(failure));
      this.error.set(outcome.alert);
      this.extra.set(outcome.extra);
      this.focusInvalid(this.formElement()?.nativeElement);
    } finally {
      this.saving.set(false);
    }
  }

  private checkFile(control: AbstractControl): ValidationErrors | null {
    if (this.form?.controls.method.value !== 'file') return null;
    const file = control.value as File | null;
    if (!file) return { message: 'Choose the file to upload.' };
    const uploads = this.uploads();
    const problem = fileProblem(file, uploads.maximumBytes, uploads.allowedExtensions);
    return problem ? { message: problem } : null;
  }

  private checkUrl(control: AbstractControl): ValidationErrors | null {
    if (this.form?.controls.method.value !== 'link') return null;
    const url = String(control.value ?? '').trim();
    if (!url) return { message: 'Enter the link to the document.' };
    return HTTPS.test(url)
      ? null
      : { message: 'Enter a full link that starts with https://, for example https://drive.example.com/plans.' };
  }
}
