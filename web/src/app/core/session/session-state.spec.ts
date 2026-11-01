import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { VIEWER_PERMISSIONS, sessionFixture, signInForTest } from '../../testing/session-fixture';
import { Permission } from './permissions';
import { roleLabel } from './roles';
import { SessionState } from './session-state';

function setup() {
  TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
  return { session: TestBed.inject(SessionState), backend: TestBed.inject(HttpTestingController) };
}

describe('SessionState', () => {
  it('starts signed out with the default company settings', () => {
    const { session } = setup();

    expect(session.isSignedIn()).toBe(false);
    expect(session.currencyCode()).toBe('PHP');
    expect(session.timeZoneId()).toBe('Asia/Manila');
    expect(session.can(Permission.projectsRead)).toBe(false);
  });

  it('gets the anti-forgery cookie once, then reads the session, when the app starts', async () => {
    const { session, backend } = setup();

    const started = session.initialize();
    backend.expectOne('/api/identity/csrf').flush(null, { status: 204, statusText: 'No Content' });
    await Promise.resolve();
    await Promise.resolve();
    backend
      .expectOne('/api/identity/session')
      .flush(sessionFixture({ organizationName: 'Acme Builders', vatRateBps: 1200 }));
    await started;

    expect(session.isSignedIn()).toBe(true);
    expect(session.organizationName()).toBe('Acme Builders');
    expect(session.vatRateBps()).toBe(1200);
    backend.verify();
  });

  it('stays signed out when there is no session (401 at start-up)', async () => {
    const { session, backend } = setup();

    const started = session.initialize();
    backend.expectOne('/api/identity/csrf').flush(null, { status: 204, statusText: 'No Content' });
    await Promise.resolve();
    await Promise.resolve();
    backend
      .expectOne('/api/identity/session')
      .flush(null, { status: 401, statusText: 'Unauthorized' });
    await started;

    expect(session.isSignedIn()).toBe(false);
  });

  it('answers can() and hasAnyPermission() from the permissions in the session', async () => {
    const { session, backend } = setup();
    await signInForTest(session, backend, { roles: ['Viewer'], permissions: VIEWER_PERMISSIONS });

    expect(session.can(Permission.projectsRead)).toBe(true);
    expect(session.can(Permission.projectsManage)).toBe(false);
    expect(session.hasAnyPermission(Permission.usersManage, Permission.costsRead)).toBe(true);
    expect(session.hasAnyPermission(Permission.usersManage, Permission.auditRead)).toBe(false);
    expect(session.hasAnyPermission()).toBe(false);
  });

  it('gives a signal for hiding a control from people who cannot use it', async () => {
    const { session, backend } = setup();
    const canManage = session.permit(Permission.projectsManage);
    expect(canManage()).toBe(false);

    await signInForTest(session, backend);
    expect(canManage()).toBe(true);
  });

  it('shows role names the way people say them', async () => {
    const { session, backend } = setup();
    await signInForTest(session, backend, { roles: ['ProjectManager', 'HrOfficer'] });

    expect(session.roleLabels()).toEqual(['Project Manager', 'HR Officer']);
    expect(roleLabel('SomethingNew')).toBe('Something New');
  });

  it('uses the company currency, time zone and VAT rate from the session', async () => {
    const { session, backend } = setup();
    await signInForTest(session, backend, {
      currencyCode: 'USD',
      timeZoneId: 'America/Los_Angeles',
      vatRateBps: 500,
    });

    expect(session.currencyCode()).toBe('USD');
    expect(session.timeZoneId()).toBe('America/Los_Angeles');
    expect(session.vatRateBps()).toBe(500);
  });

  it('signs out on the server before forgetting the session, and keeps it if the server cannot be reached', async () => {
    const { session, backend } = setup();
    await signInForTest(session, backend);

    const failed = session.signOut().catch(() => 'failed');
    backend
      .expectOne('/api/identity/sign-out')
      .flush(null, { status: 0, statusText: 'Unknown Error' });
    expect(await failed).toBe('failed');
    expect(session.isSignedIn()).toBe(true);

    const done = session.signOut();
    backend
      .expectOne('/api/identity/sign-out')
      .flush(null, { status: 204, statusText: 'No Content' });
    await done;
    expect(session.isSignedIn()).toBe(false);
  });

  it('remembers that a new password is required', async () => {
    const { session, backend } = setup();
    await signInForTest(session, backend);

    session.requirePasswordChange();

    expect(session.mustChangePassword()).toBe(true);
  });

  it('takes the new session after a password change', async () => {
    const { session, backend } = setup();
    await signInForTest(session, backend, { mustChangePassword: true });
    expect(session.mustChangePassword()).toBe(true);

    const done = session.changePassword({
      currentPassword: 'temp-password-1',
      newPassword: 'my-own-password-2',
    });
    backend
      .expectOne('/api/identity/change-password')
      .flush(sessionFixture({ mustChangePassword: false }));
    await done;

    expect(session.mustChangePassword()).toBe(false);
  });

  it('treats a failed setup check as "no setup needed" rather than blocking sign-in', async () => {
    const { session, backend } = setup();

    const needs = session.needsSetup();
    backend
      .expectOne('/api/identity/setup-status')
      .flush(null, { status: 500, statusText: 'Server Error' });

    expect(await needs).toBe(false);
  });
});
