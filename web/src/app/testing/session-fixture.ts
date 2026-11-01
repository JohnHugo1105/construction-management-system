import { HttpTestingController } from '@angular/common/http/testing';
import { Permission } from '../core/session/permissions';
import { Session } from '../core/session/session.models';
import { SessionState } from '../core/session/session-state';

/** Every permission, as an administrator holds them. */
export const ALL_PERMISSIONS: readonly string[] = Object.values(Permission);

/** A signed-in administrator of "Acme Builders". Pass overrides for the case under test. */
export function sessionFixture(overrides: Partial<Session> = {}): Session {
  return {
    userId: 'user-1',
    tenantId: 'tenant-1',
    username: 'ana',
    displayName: 'Ana Reyes',
    roles: ['Administrator'],
    permissions: ALL_PERMISSIONS,
    mustChangePassword: false,
    organizationName: 'Acme Builders',
    currencyCode: 'PHP',
    timeZoneId: 'Asia/Manila',
    vatRateBps: 1200,
    ...overrides,
  };
}

/** A read-only person: can look at projects, costs, workers and stock, and change nothing. */
export const VIEWER_PERMISSIONS: readonly string[] = [
  Permission.projectsRead,
  Permission.costsRead,
  Permission.workforceRead,
  Permission.inventoryRead,
];

/** Signs the session in through the fake backend, the way the sign-in page does. */
export async function signInForTest(
  session: SessionState,
  backend: HttpTestingController,
  overrides: Partial<Session> = {},
): Promise<void> {
  const done = session.signIn({ username: 'ana', password: 'a-long-password-1' });
  backend.expectOne('/api/identity/sign-in').flush(sessionFixture(overrides));
  await done;
}

/** A page of results, shaped as the API returns it. */
export function pageOf<T>(items: readonly T[], total = items.length, page = 1, pageSize = 25) {
  return { items, total, page, pageSize };
}
