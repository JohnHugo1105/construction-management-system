import { Injectable, Signal, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { DEFAULT_TIME_ZONE } from '../../shared/presentation/business-time';
import { IdentityApi } from './identity-api';
import { roleLabel } from './roles';
import { BootstrapRequest, ChangePasswordRequest, Session, SignInRequest } from './session.models';

/**
 * Who is signed in, what they may do, and their company's settings. The one place the session lives: the shell, the guards
 * and every feature read it from here. Screens never keep their own copy of a permission or of the currency.
 */
@Injectable({ providedIn: 'root' })
export class SessionState {
  private readonly api = inject(IdentityApi);
  private readonly current = signal<Session | null>(null);

  /** The full session, or null while nobody is signed in. */
  readonly session = this.current.asReadonly();
  readonly isSignedIn = computed(() => this.current() !== null);
  readonly displayName = computed(() => this.current()?.displayName ?? '');
  readonly username = computed(() => this.current()?.username ?? '');
  readonly organizationName = computed(() => this.current()?.organizationName ?? '');
  readonly mustChangePassword = computed(() => this.current()?.mustChangePassword ?? false);

  /** Money is shown in this currency (ISO code). */
  readonly currencyCode = computed(() => this.current()?.currencyCode ?? 'PHP');

  /** Business dates and times are read in this time zone. */
  readonly timeZoneId = computed(() => this.current()?.timeZoneId ?? DEFAULT_TIME_ZONE);

  /** The company VAT rate in basis points (1,200 is 12%). */
  readonly vatRateBps = computed(() => this.current()?.vatRateBps ?? 0);

  readonly roles = computed(() => this.current()?.roles ?? []);
  readonly roleLabels = computed(() => this.roles().map(roleLabel));
  readonly permissions = computed(() => new Set(this.current()?.permissions ?? []));

  /** True when the signed-in person holds the permission. */
  can(permission: string): boolean {
    return this.permissions().has(permission);
  }

  /** True when the signed-in person holds at least one of the permissions. */
  hasAnyPermission(...permissions: readonly string[]): boolean {
    const held = this.permissions();
    return permissions.some((permission) => held.has(permission));
  }

  /**
   * A signal that is true while the person holds the permission, for showing a control only to roles that can use it:
   * `protected readonly canManage = session.permit('projects.manage');` then `@if (canManage()) { ... }`.
   */
  permit(permission: string): Signal<boolean> {
    return computed(() => this.can(permission));
  }

  /** Called once before the app starts: gets the anti-forgery cookie and finds out whether a session already exists. */
  async initialize(): Promise<void> {
    await this.ensureCsrf();
    try {
      this.current.set(await firstValueFrom(this.api.session()));
    } catch {
      // A 401 simply means nobody is signed in. Any other failure also leaves the app signed out, and the sign-in page explains.
      this.current.set(null);
    }
  }

  /** Fetches a fresh anti-forgery cookie. Safe to call at any time; a failure is not worth stopping for. */
  async ensureCsrf(): Promise<void> {
    try {
      await firstValueFrom(this.api.csrf());
    } catch {
      // The next request that needs it reports the problem.
    }
  }

  /** Whether the system still needs its first-time setup. A failed check counts as "no". */
  async needsSetup(): Promise<boolean> {
    try {
      return (await firstValueFrom(this.api.setupStatus())).needsSetup;
    } catch {
      return false;
    }
  }

  async signIn(request: SignInRequest): Promise<Session> {
    const session = await firstValueFrom(this.api.signIn(request));
    this.current.set(session);
    return session;
  }

  async bootstrap(request: BootstrapRequest): Promise<Session> {
    const session = await firstValueFrom(this.api.bootstrap(request));
    this.current.set(session);
    return session;
  }

  async changePassword(request: ChangePasswordRequest): Promise<Session> {
    const session = await firstValueFrom(this.api.changePassword(request));
    this.current.set(session);
    return session;
  }

  /** Signs out on the server, then forgets the session here. If the server cannot be reached the person stays signed in. */
  async signOut(): Promise<void> {
    await firstValueFrom(this.api.signOut());
    this.clear();
  }

  /** Reads the session again, for example after the company settings changed. */
  async refresh(): Promise<void> {
    try {
      this.current.set(await firstValueFrom(this.api.session()));
    } catch {
      // Keep what is held; a 401 is handled by the auth interceptor.
    }
  }

  /** Forgets the session here without telling the server (it has already ended, or the person signed out). */
  clear(): void {
    this.current.set(null);
  }

  /** The server said a new password is needed first; the shell then stays minimal until it is set. */
  requirePasswordChange(): void {
    const session = this.current();
    if (session && !session.mustChangePassword)
      this.current.set({ ...session, mustChangePassword: true });
  }
}
