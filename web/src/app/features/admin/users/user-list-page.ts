import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { ApiError, asApiError } from '../../../core/api/api-error';
import { ToastService } from '../../../core/notifications/toast-service';
import { roleLabel } from '../../../core/session/roles';
import { SessionState } from '../../../core/session/session-state';
import { formatDateTime } from '../../../shared/presentation/business-time';
import { ApiErrorAlert } from '../../../shared/ui/api-error-alert/api-error-alert';
import { Button } from '../../../shared/ui/button/button';
import { ConfirmDialog } from '../../../shared/ui/dialog/confirm-dialog';
import { EmptyState } from '../../../shared/ui/empty-state/empty-state';
import { Icon } from '../../../shared/ui/icon/icon';
import { Menu } from '../../../shared/ui/menu/menu';
import { PageHeader } from '../../../shared/ui/page-header/page-header';
import { Skeleton } from '../../../shared/ui/skeleton/skeleton';
import { StatusChip } from '../../../shared/ui/status-chip/status-chip';
import { Viewport } from '../../../shared/ui/viewport/viewport';
import { ResetPasswordDialog } from './reset-password-dialog';
import { UsersApi } from './users-api';
import { UserAccount } from './users.models';

/** The people who can sign in: their roles and state, and the few things an administrator does with each. */
@Component({
  selector: 'app-user-list-page',
  imports: [
    RouterLink,
    PageHeader,
    Button,
    Icon,
    Menu,
    StatusChip,
    EmptyState,
    Skeleton,
    ApiErrorAlert,
    ConfirmDialog,
    ResetPasswordDialog,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './user-list-page.html',
  styleUrl: './user-list-page.scss',
})
export class UserListPage {
  private readonly api = inject(UsersApi);
  private readonly session = inject(SessionState);
  private readonly toast = inject(ToastService);
  protected readonly viewport = inject(Viewport);

  private readonly users = rxResource({ stream: () => this.api.list() });

  protected readonly confirmOff = signal<UserAccount | null>(null);
  protected readonly resetFor = signal<UserAccount | null>(null);
  protected readonly working = signal(false);
  protected readonly actionError = signal<ApiError | null>(null);

  protected readonly loading = computed(() => this.users.isLoading() && !this.users.hasValue());
  protected readonly loadError = computed(() => {
    const failure = this.users.error();
    return failure ? asApiError(failure) : null;
  });

  protected readonly rows = computed(() => {
    const zone = this.session.timeZoneId();
    const me = this.session.session()?.userId;
    const now = Date.now();
    return (this.users.hasValue() ? this.users.value() : []).map((user) => {
      const locked = user.lockedUntilUtc !== null && Date.parse(user.lockedUntilUtc) > now;
      return {
        user,
        roles: user.roles.map(roleLabel),
        status: !user.isActive
          ? ({ label: 'Turned off', tone: 'neutral', icon: 'minus-circle' } as const)
          : locked
            ? ({ label: 'Locked for now', tone: 'warning', icon: 'lock' } as const)
            : ({ label: 'Active', tone: 'success', icon: 'check-circle' } as const),
        lastSignIn: user.lastSignInUtc ? formatDateTime(user.lastSignInUtc, zone) : 'Never',
        isSelf: user.id === me,
      };
    });
  });

  protected reload(): void {
    this.actionError.set(null);
    this.users.reload();
  }

  protected async turnOff(): Promise<void> {
    const user = this.confirmOff();
    if (!user) return;
    if (await this.run(() => firstValueFrom(this.api.deactivate(user.id)))) {
      this.toast.success(`${user.displayName} has been turned off.`);
    }
    this.confirmOff.set(null);
  }

  protected async turnOn(user: UserAccount): Promise<void> {
    if (await this.run(() => firstValueFrom(this.api.reactivate(user.id)))) {
      this.toast.success(`${user.displayName} can sign in again.`);
    }
  }

  protected passwordWasReset(): void {
    const user = this.resetFor();
    this.resetFor.set(null);
    if (user) this.toast.success(`Temporary password set for ${user.displayName}.`);
    this.users.reload();
  }

  /** Runs one change and refreshes the list. Returns whether it worked; the API's own message is shown when it did not. */
  private async run(change: () => Promise<unknown>): Promise<boolean> {
    if (this.working()) return false;
    this.working.set(true);
    this.actionError.set(null);
    try {
      await change();
      this.users.reload();
      return true;
    } catch (failure) {
      this.actionError.set(asApiError(failure));
      return false;
    } finally {
      this.working.set(false);
    }
  }
}
