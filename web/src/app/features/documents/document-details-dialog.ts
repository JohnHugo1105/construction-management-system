import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  OnInit,
  computed,
  inject,
  input,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { ApiError, asApiError } from '../../core/api/api-error';
import { Permission } from '../../core/session/permissions';
import { SessionState } from '../../core/session/session-state';
import { injectFocusFirstInvalid } from '../../shared/forms/server-errors';
import { ApiErrorAlert } from '../../shared/ui/api-error-alert/api-error-alert';
import { Button } from '../../shared/ui/button/button';
import { Dialog } from '../../shared/ui/dialog/dialog';
import { Control } from '../../shared/ui/form-field/control';
import { FormField } from '../../shared/ui/form-field/form-field';
import { describeFormFailure } from '../workforce/form-failure';
import { DocumentsApi } from './documents-api';
import { categoryChoices, isDated } from './documents-presentation';
import { DocumentCategory, DocumentListItem, ProjectDocument } from './documents.models';

/**
 * Changes a document's title, kind, description and valid-until date. Its files and links stay as they are: a newer file is added as a
 * new version. Sends the version the person opened; a refusal shows in the server's words with a way to reload.
 */
@Component({
  selector: 'app-document-details-dialog',
  imports: [ReactiveFormsModule, Dialog, Button, FormField, Control, ApiErrorAlert],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-dialog title="Change document details" [locked]="saving()" (closed)="dismissed.emit()">
      <div class="stack">
        @if (error(); as problem) {
          <app-api-error-alert
            [error]="problem"
            [extra]="extra()"
            [allowRetry]="problem.status === 409"
            reloadLabel="Reload documents"
            (reload)="reloaded.emit()"
          />
        }
        <form id="document-details-form" class="form" [formGroup]="form" (ngSubmit)="submit()" #formElement>
          <app-form-field
            label="Title"
            [control]="form.controls.title"
            requiredMessage="Enter the document's title."
          >
            <input appControl formControlName="title" maxlength="120" autocomplete="off" data-autofocus />
          </app-form-field>
          <app-form-field label="Kind of document" [control]="form.controls.category">
            <select appControl formControlName="category">
              @for (choice of categories(); track choice.value) {
                <option [value]="choice.value">{{ choice.label }}</option>
              }
            </select>
          </app-form-field>
          @if (dated()) {
            <app-form-field
              label="Valid until"
              [optional]="true"
              hint="The last day it is valid. It is listed under Needs your action 30 days before."
              [control]="form.controls.validUntil"
            >
              <input appControl type="date" formControlName="validUntil" />
            </app-form-field>
          }
          <app-form-field label="Description" [optional]="true" [control]="form.controls.description">
            <textarea appControl formControlName="description" rows="2" maxlength="300"></textarea>
          </app-form-field>
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
        <button appButton variant="primary" type="submit" form="document-details-form" [busy]="saving()">
          Save changes
        </button>
      </div>
    </app-dialog>
  `,
})
export class DocumentDetailsDialog implements OnInit {
  readonly document = input.required<DocumentListItem>();

  readonly saved = output<ProjectDocument>();
  readonly dismissed = output<void>();
  readonly reloaded = output<void>();

  private readonly api = inject(DocumentsApi);
  private readonly session = inject(SessionState);
  protected readonly dialog = viewChild.required(Dialog);
  private readonly formElement = viewChild<ElementRef<HTMLFormElement>>('formElement');
  private readonly focusInvalid = injectFocusFirstInvalid();

  protected readonly categories = computed(() =>
    categoryChoices(this.session.can(Permission.costsRead)),
  );

  protected readonly form = new FormGroup({
    title: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(120)],
    }),
    category: new FormControl<DocumentCategory>('Other', { nonNullable: true }),
    validUntil: new FormControl('', { nonNullable: true }),
    description: new FormControl('', {
      nonNullable: true,
      validators: [Validators.maxLength(300)],
    }),
  });

  private readonly category = toSignal(this.form.controls.category.valueChanges, {
    initialValue: null,
  });
  protected readonly dated = computed(() => isDated(this.category() ?? this.document().category));

  protected readonly saving = signal(false);
  protected readonly error = signal<ApiError | null>(null);
  protected readonly extra = signal<readonly string[]>([]);

  ngOnInit(): void {
    const document = this.document();
    this.form.setValue({
      title: document.title,
      category: document.category,
      validUntil: document.validUntil ?? '',
      description: document.description ?? '',
    });
  }

  protected async submit(): Promise<void> {
    if (this.saving()) return;
    this.form.markAllAsTouched();
    if (this.form.invalid) {
      this.focusInvalid(this.formElement()?.nativeElement);
      return;
    }
    const value = this.form.getRawValue();
    const document = this.document();
    this.saving.set(true);
    this.error.set(null);
    this.extra.set([]);
    try {
      this.saved.emit(
        await firstValueFrom(
          this.api.update(
            document.id,
            {
              title: value.title.trim(),
              description: value.description.trim() || null,
              category: value.category,
              // A kind without a date drops the date: the server keeps it only for permits, policies and certificates.
              validUntil: this.dated() && value.validUntil ? value.validUntil : null,
            },
            document.version,
          ),
        ),
      );
    } catch (failure) {
      const outcome = describeFormFailure(this.form, asApiError(failure));
      this.error.set(outcome.alert);
      this.extra.set(outcome.extra);
      this.focusInvalid(this.formElement()?.nativeElement);
    } finally {
      this.saving.set(false);
    }
  }
}
