import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  computed,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { ApiError, asApiError } from '../../core/api/api-error';
import { ToastService } from '../../core/notifications/toast-service';
import { SessionState } from '../../core/session/session-state';
import { applyServerErrors, injectFocusFirstInvalid } from '../../shared/forms/server-errors';
import { formatDateTime } from '../../shared/presentation/business-time';
import { Alert } from '../../shared/ui/alert/alert';
import { ApiErrorAlert } from '../../shared/ui/api-error-alert/api-error-alert';
import { Button } from '../../shared/ui/button/button';
import { Dialog } from '../../shared/ui/dialog/dialog';
import { Control } from '../../shared/ui/form-field/control';
import { FieldGroup } from '../../shared/ui/form-field/field-group';
import { FormField } from '../../shared/ui/form-field/form-field';
import { PageHeader } from '../../shared/ui/page-header/page-header';
import { Skeleton } from '../../shared/ui/skeleton/skeleton';
import { StatusChip } from '../../shared/ui/status-chip/status-chip';
import { TimeClockApi } from './timeclock-api';
import { newId } from './timeclock-presentation';
import { DeviceKind, DeviceWithKey, TimeClockDevice } from './timeclock.models';

/**
 * Administration, Time clocks (settings.manage): the machines allowed to send scans. Registering one shows its key once, with a copy button; a key
 * can be replaced (the old one stops at once) and a machine revoked. The capability line says whether the time clock is in use yet; until a device
 * connects, attendance is recorded by hand on the project's Team tab, as before.
 */
@Component({
  selector: 'app-time-clock-devices-page',
  imports: [
    ReactiveFormsModule,
    PageHeader,
    Alert,
    ApiErrorAlert,
    Button,
    Dialog,
    FormField,
    FieldGroup,
    Control,
    Skeleton,
    StatusChip,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-page-header title="Time clocks">
      <button appButton variant="primary" type="button" (click)="registering.set(true)">
        Add a time clock
      </button>
    </app-page-header>

    <div class="stack">
      @if (list.error(); as failure) {
        <app-api-error-alert
          [error]="asError(failure)"
          [allowRetry]="true"
          reloadLabel="Try again"
          (reload)="list.reload()"
        />
      } @else if (!list.hasValue()) {
        <app-skeleton variant="card" [lines]="3" label="Loading time clocks" />
      } @else {
        <app-alert
          [tone]="list.value().capability.status === 'Available' ? 'success' : 'info'"
          [title]="capabilityTitle()"
        >
          {{ capabilityText() }}
        </app-alert>

        @if (shownKey(); as shown) {
          <app-alert tone="warning" [title]="'Key for ' + shown.device.name + ': shown only now'">
            Enter it on the machine (on a kiosk tablet, open /kiosk). It won't be shown again; if it
            is lost, give the time clock a new key.
            <p class="device-key">
              <code>{{ shown.key }}</code>
            </p>
            <div alertActions>
              <button appButton type="button" (click)="copy(shown.key)">
                {{ copied() ? 'Copied' : 'Copy key' }}
              </button>
              <button appButton variant="ghost" type="button" (click)="shownKey.set(null)">
                I've entered it
              </button>
            </div>
          </app-alert>
        }

        @if (actionError(); as problem) {
          <app-api-error-alert
            [error]="problem"
            reloadLabel="Reload the list"
            (reload)="list.reload()"
          />
        }

        @if (list.value().items.length > 0) {
          <ul class="devices">
            @for (device of list.value().items; track device.id) {
              <li class="card devices__item">
                <div class="devices__head">
                  <h2 class="card__title">{{ device.name }}</h2>
                  @if (device.status === 'Revoked') {
                    <app-status-chip label="Revoked" tone="neutral" icon="lock" />
                  } @else if (device.lastSeenAtUtc) {
                    <app-status-chip label="Connected" tone="success" icon="check-circle" />
                  } @else {
                    <app-status-chip label="Not connected yet" tone="warning" icon="clock" />
                  }
                </div>
                <p>
                  {{ device.kind === 'Kiosk' ? 'Kiosk tablet' : 'Time clock machine' }} for
                  {{ device.projectName }}
                </p>
                <p class="devices__muted">
                  {{
                    device.lastSeenAtUtc
                      ? 'Last sent scans ' + when(device.lastSeenAtUtc)
                      : 'Has not sent any scans yet'
                  }}
                </p>
                @if (device.status === 'Active') {
                  <div class="devices__actions">
                    <button
                      appButton
                      type="button"
                      [busy]="busy() === device.id + ':rotate'"
                      (click)="rotate(device)"
                    >
                      Give it a new key
                    </button>
                    <button appButton variant="ghost" type="button" (click)="revoking.set(device)">
                      Revoke
                    </button>
                  </div>
                }
              </li>
            }
          </ul>
        }
      }
    </div>

    @if (registering()) {
      <app-dialog title="Add a time clock" [locked]="saving()" (closed)="registering.set(false)">
        <div class="stack">
          @if (registerError(); as problem) {
            <app-api-error-alert
              [error]="problem"
              [extra]="extra()"
              reloadLabel="Reload the list"
              (reload)="list.reload()"
            />
          }
          <form
            id="register-device-form"
            class="form"
            [formGroup]="form"
            (ngSubmit)="register()"
            #formElement
          >
            <app-form-field
              label="Name"
              hint="Where it stands, for example Main gate."
              [control]="form.controls.name"
              requiredMessage="Enter a name for the time clock."
            >
              <input appControl formControlName="name" maxlength="80" autocomplete="off" />
            </app-form-field>
            <app-form-field
              label="Project"
              hint="Its scans count for this project."
              [control]="form.controls.projectId"
              requiredMessage="Choose the project."
            >
              <select appControl formControlName="projectId">
                <option value="">Choose a project</option>
                @for (project of projects(); track project.id) {
                  <option [value]="project.id">{{ project.name }}</option>
                }
              </select>
            </app-form-field>
            <app-field-group
              label="What is it?"
              [control]="form.controls.kind"
              requiredMessage="Choose a kind."
            >
              <div class="choice-list">
                <label class="choice">
                  <input type="radio" formControlName="kind" value="Kiosk" />
                  <span class="choice__text">
                    <span class="choice__label">Kiosk tablet</span>
                    <span class="choice__hint"
                      >A tablet or PC at the gate with a barcode or QR scanner, running the kiosk
                      page.</span
                    >
                  </span>
                </label>
                <label class="choice">
                  <input type="radio" formControlName="kind" value="Terminal" />
                  <span class="choice__text">
                    <span class="choice__label">Time clock machine</span>
                    <span class="choice__hint"
                      >A machine or its gateway that sends scans by itself.</span
                    >
                  </span>
                </label>
              </div>
            </app-field-group>
          </form>
        </div>
        <div dialogActions class="dialog-actions">
          <button
            appButton
            type="button"
            [attr.disabled]="saving() ? '' : null"
            (click)="registering.set(false)"
          >
            Cancel
          </button>
          <button
            appButton
            variant="primary"
            type="submit"
            form="register-device-form"
            [busy]="saving()"
          >
            Add time clock
          </button>
        </div>
      </app-dialog>
    }

    @if (revoking(); as device) {
      <app-dialog
        title="Revoke this time clock?"
        [locked]="busy() !== null"
        (closed)="revoking.set(null)"
      >
        <p>
          {{ device.name }} stops sending scans at once, for good. Scans it already sent are kept.
        </p>
        <div dialogActions class="dialog-actions">
          <button appButton type="button" (click)="revoking.set(null)">Keep it</button>
          <button
            appButton
            variant="danger"
            type="button"
            [busy]="busy() === device.id + ':revoke'"
            (click)="revoke(device)"
          >
            Revoke time clock
          </button>
        </div>
      </app-dialog>
    }
  `,
  styles: `
    .devices {
      display: grid;
      gap: var(--space-3);
      padding: 0;
      list-style: none;
    }

    .devices__item {
      display: grid;
      gap: var(--space-2);
    }

    .devices__head,
    .devices__actions {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: var(--space-2);
    }

    .devices__head .card__title {
      margin-right: auto;
    }

    .devices__muted {
      color: var(--color-text-muted);
    }

    .device-key code {
      display: block;
      padding: var(--space-2);
      overflow-wrap: anywhere;
      font-size: var(--text-lg);
    }
  `,
})
export class TimeClockDevicesPage {
  private readonly api = inject(TimeClockApi);
  private readonly session = inject(SessionState);
  private readonly toast = inject(ToastService);
  private readonly formElement = viewChild<ElementRef<HTMLFormElement>>('formElement');
  private readonly focusInvalid = injectFocusFirstInvalid();

  protected readonly list = rxResource({ stream: () => this.api.devices() });
  private readonly projectList = rxResource({ stream: () => this.api.projects() });
  protected readonly projects = computed(() =>
    this.projectList.hasValue() ? this.projectList.value() : [],
  );

  protected readonly registering = signal(false);
  protected readonly revoking = signal<TimeClockDevice | null>(null);
  protected readonly saving = signal(false);
  protected readonly busy = signal<string | null>(null);
  protected readonly shownKey = signal<DeviceWithKey | null>(null);
  protected readonly copied = signal(false);
  protected readonly registerError = signal<ApiError | null>(null);
  protected readonly actionError = signal<ApiError | null>(null);
  protected readonly extra = signal<readonly string[]>([]);
  private requestId = newId();

  protected readonly form = new FormGroup({
    name: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(80)],
    }),
    projectId: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    kind: new FormControl<DeviceKind>('Kiosk', {
      nonNullable: true,
      validators: [Validators.required],
    }),
  });

  protected readonly capabilityTitle = computed(() =>
    this.list.hasValue() && this.list.value().capability.status === 'Available'
      ? 'Time clock: Available'
      : 'Time clock: Needs setup',
  );

  /** The server's capability line without its leading "Time clock: ..." words, which the title already says. */
  protected readonly capabilityText = computed(() =>
    this.list.hasValue()
      ? this.list.value().capability.message.replace(/^Time clock: [^.]*\.\s*/, '')
      : '',
  );

  protected asError(failure: unknown): ApiError {
    return asApiError(failure);
  }

  protected when(instant: string): string {
    return formatDateTime(instant, this.session.timeZoneId());
  }

  protected async copy(key: string): Promise<void> {
    try {
      await globalThis.navigator?.clipboard?.writeText(key);
      this.copied.set(true);
    } catch {
      this.toast.error("Couldn't copy. Select the key and copy it by hand.");
    }
  }

  protected async register(): Promise<void> {
    if (this.saving()) return;
    this.form.markAllAsTouched();
    if (this.form.invalid) {
      this.focusInvalid(this.formElement()?.nativeElement);
      return;
    }
    const value = this.form.getRawValue();
    this.saving.set(true);
    this.registerError.set(null);
    try {
      const created = await firstValueFrom(
        this.api.registerDevice(value.name.trim(), value.projectId, value.kind, this.requestId),
      );
      this.show(created);
      this.registering.set(false);
      this.form.reset({ name: '', projectId: '', kind: 'Kiosk' });
      this.requestId = newId();
      this.list.reload();
    } catch (failure) {
      const problem = asApiError(failure);
      this.registerError.set(problem);
      this.extra.set(applyServerErrors(this.form, problem));
    } finally {
      this.saving.set(false);
    }
  }

  protected async rotate(device: TimeClockDevice): Promise<void> {
    if (this.busy()) return;
    this.busy.set(`${device.id}:rotate`);
    this.actionError.set(null);
    try {
      this.show(await firstValueFrom(this.api.rotateKey(device.id, device.version)));
      this.list.reload();
    } catch (failure) {
      this.actionError.set(asApiError(failure));
    } finally {
      this.busy.set(null);
    }
  }

  protected async revoke(device: TimeClockDevice): Promise<void> {
    if (this.busy()) return;
    this.busy.set(`${device.id}:revoke`);
    this.actionError.set(null);
    try {
      await firstValueFrom(this.api.revoke(device.id, device.version));
      this.revoking.set(null);
      if (this.shownKey()?.device.id === device.id) this.shownKey.set(null);
      this.toast.success(`${device.name} was revoked.`);
      this.list.reload();
    } catch (failure) {
      this.revoking.set(null);
      this.actionError.set(asApiError(failure));
    } finally {
      this.busy.set(null);
    }
  }

  private show(created: DeviceWithKey): void {
    this.copied.set(false);
    this.shownKey.set(created);
  }
}
