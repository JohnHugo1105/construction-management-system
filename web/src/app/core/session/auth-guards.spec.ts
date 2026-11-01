import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import {
  ActivatedRouteSnapshot,
  CanActivateFn,
  Router,
  RouterStateSnapshot,
  UrlTree,
  provideRouter,
} from '@angular/router';
import { VIEWER_PERMISSIONS, signInForTest } from '../../testing/session-fixture';
import {
  adminGuard,
  authGuard,
  guestGuard,
  permissionGuard,
  safeReturnUrl,
  setupGuard,
} from './auth-guards';
import { Permission } from './permissions';
import { SessionState } from './session-state';

function setup() {
  TestBed.configureTestingModule({
    providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
  });
  return {
    session: TestBed.inject(SessionState),
    backend: TestBed.inject(HttpTestingController),
    router: TestBed.inject(Router),
  };
}

function run(guard: CanActivateFn, url = '/projects') {
  return TestBed.runInInjectionContext(() =>
    guard({} as ActivatedRouteSnapshot, { url } as RouterStateSnapshot),
  );
}

function asUrl(result: unknown): string {
  expect(result).toBeInstanceOf(UrlTree);
  return TestBed.inject(Router).serializeUrl(result as UrlTree);
}

describe('authGuard', () => {
  it('sends a signed-out person to sign-in and remembers the page they wanted', () => {
    setup();

    expect(asUrl(run(authGuard, '/projects/42?tab=costs'))).toBe(
      '/sign-in?returnUrl=%2Fprojects%2F42%3Ftab%3Dcosts',
    );
  });

  it('does not make the home address a return page', () => {
    setup();

    expect(asUrl(run(authGuard, '/'))).toBe('/sign-in');
  });

  it('lets a signed-in person through', async () => {
    const { session, backend } = setup();
    await signInForTest(session, backend);

    expect(run(authGuard)).toBe(true);
  });

  it('allows only the change-password page while a new password is required', async () => {
    const { session, backend } = setup();
    await signInForTest(session, backend, { mustChangePassword: true });

    expect(asUrl(run(authGuard, '/projects'))).toBe('/change-password');
    expect(run(authGuard, '/change-password')).toBe(true);
  });
});

describe('guestGuard', () => {
  it('lets a signed-out person see sign-in, and sends a signed-in person home', async () => {
    const { session, backend } = setup();
    expect(run(guestGuard, '/sign-in')).toBe(true);

    await signInForTest(session, backend);
    expect(asUrl(run(guestGuard, '/sign-in'))).toBe('/projects');
  });
});

describe('setupGuard', () => {
  it('opens setup only while the system has no users', async () => {
    const { backend } = setup();

    const open = run(setupGuard) as Promise<unknown>;
    backend.expectOne('/api/identity/setup-status').flush({ needsSetup: true });
    expect(await open).toBe(true);
  });

  it('sends everyone to sign-in once the system is set up', async () => {
    const { backend } = setup();

    const closed = run(setupGuard) as Promise<unknown>;
    backend.expectOne('/api/identity/setup-status').flush({ needsSetup: false });
    expect(asUrl(await closed)).toBe('/sign-in');
  });
});

describe('permissionGuard', () => {
  it('lets in anyone who holds the permission', async () => {
    const { session, backend } = setup();
    await signInForTest(session, backend);

    expect(run(permissionGuard(Permission.projectsManage))).toBe(true);
  });

  it('shows the not-allowed page to everyone else', async () => {
    const { session, backend } = setup();
    await signInForTest(session, backend, { roles: ['Viewer'], permissions: VIEWER_PERMISSIONS });

    expect(asUrl(run(permissionGuard(Permission.projectsManage)))).toBe('/not-allowed');
  });

  it('accepts any one of several permissions', async () => {
    const { session, backend } = setup();
    await signInForTest(session, backend, {
      roles: ['Accountant'],
      permissions: [Permission.auditRead],
    });

    expect(run(adminGuard)).toBe(true);
    expect(asUrl(run(permissionGuard(Permission.usersManage, Permission.settingsManage)))).toBe(
      '/not-allowed',
    );
  });
});

describe('safeReturnUrl', () => {
  it('follows only addresses inside the app', () => {
    expect(safeReturnUrl('/projects/42?tab=costs')).toBe('/projects/42?tab=costs');
    expect(safeReturnUrl('/admin/audit')).toBe('/admin/audit');
  });

  it('refuses anything that could leave the app or loop', () => {
    expect(safeReturnUrl('https://evil.example/steal')).toBe('/projects');
    expect(safeReturnUrl('//evil.example')).toBe('/projects');
    expect(safeReturnUrl('/\\evil.example')).toBe('/projects');
    expect(safeReturnUrl('javascript:alert(1)')).toBe('/projects');
    expect(safeReturnUrl('/sign-in?returnUrl=%2F')).toBe('/projects');
    expect(safeReturnUrl('/setup')).toBe('/projects');
    expect(safeReturnUrl(null)).toBe('/projects');
    expect(safeReturnUrl('')).toBe('/projects');
  });
});
