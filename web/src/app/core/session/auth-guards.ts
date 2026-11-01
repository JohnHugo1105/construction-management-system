import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { Permission } from './permissions';
import { SessionState } from './session-state';

/** Where a signed-in person lands when nothing else was asked for. The home route sends them to the page that suits what they may do. */
export const HOME_URL = '/';
export const SIGN_IN_URL = '/sign-in';
export const CHANGE_PASSWORD_URL = '/change-password';
export const NOT_ALLOWED_URL = '/not-allowed';

function pathOf(url: string): string {
  return url.split(/[?#]/)[0] ?? url;
}

/**
 * A return URL is followed after sign-in, so it must stay inside this app: it has to start with a single slash and may not be
 * a protocol-relative or absolute address, nor point back at the sign-in or setup pages.
 */
export function safeReturnUrl(value: string | null | undefined): string {
  if (!value || !value.startsWith('/') || value.startsWith('//') || value.startsWith('/\\'))
    return HOME_URL;
  const path = pathOf(value);
  if (path === SIGN_IN_URL || path === '/setup' || path === '/') return HOME_URL;
  return value;
}

/**
 * Needs a signed-in person. Anyone else goes to sign-in and comes back to the page they wanted. While a new password is
 * required, the only page open is the change-password page.
 */
export const authGuard: CanActivateFn = (_route, state) => {
  const session = inject(SessionState);
  const router = inject(Router);

  if (!session.isSignedIn()) {
    const wanted = pathOf(state.url) === '/' ? {} : { returnUrl: state.url };
    return router.createUrlTree([SIGN_IN_URL], { queryParams: wanted });
  }
  if (session.mustChangePassword() && pathOf(state.url) !== CHANGE_PASSWORD_URL) {
    return router.createUrlTree([CHANGE_PASSWORD_URL]);
  }
  return true;
};

/** For pages that only make sense before signing in (sign-in, setup): a signed-in person goes home instead. */
export const guestGuard: CanActivateFn = () => {
  const session = inject(SessionState);
  return session.isSignedIn() ? inject(Router).createUrlTree([HOME_URL]) : true;
};

/** First-time setup is open only while the system has no users. Afterwards the address leads to sign-in. */
export const setupGuard: CanActivateFn = async () => {
  const session = inject(SessionState);
  const router = inject(Router);
  if (session.isSignedIn()) return router.createUrlTree([HOME_URL]);
  return (await session.needsSetup()) ? true : router.createUrlTree([SIGN_IN_URL]);
};

/**
 * Lets a person in when they hold at least one of the permissions; anyone else sees the "not allowed" page.
 * `permissionGuard(Permission.projectsManage)` for one, `permissionGuard(a, b)` for "either".
 */
export function permissionGuard(...permissions: readonly string[]): CanActivateFn {
  return () => {
    const session = inject(SessionState);
    return session.hasAnyPermission(...permissions)
      ? true
      : inject(Router).createUrlTree([NOT_ALLOWED_URL]);
  };
}

/**
 * The home address: people who see money start on the dashboard (every project's position at a glance), everyone else on the project
 * list, which every role may open.
 */
export const homeGuard: CanActivateFn = () =>
  inject(Router).createUrlTree([
    inject(SessionState).hasAnyPermission(Permission.costsRead) ? '/dashboard' : '/projects',
  ]);

/** The Administration area opens for anyone who may use at least one of its pages. */
export const adminGuard: CanActivateFn = permissionGuard(
  Permission.usersManage,
  Permission.settingsManage,
  Permission.auditRead,
);
