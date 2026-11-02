import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { settle, text } from '../../testing/dom';
import { signInForTest } from '../../testing/session-fixture';
import { SessionState } from '../../core/session/session-state';
import { NotificationBell } from './notification-bell';
import { NotificationBellState } from './notification-bell-state';
import { READER } from './collaboration.fixtures';

const COUNT = '/api/notifications/count';

async function open(permissions: readonly string[] = READER) {
  TestBed.configureTestingModule({
    providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
  });
  const backend = TestBed.inject(HttpTestingController);
  await signInForTest(TestBed.inject(SessionState), backend, { roles: ['Viewer'], permissions });
  const fixture = TestBed.createComponent(NotificationBell);
  fixture.detectChanges();
  return { fixture, backend, root: fixture.nativeElement as HTMLElement };
}

describe('NotificationBell', () => {
  it('reads the unread count once and shows it on the bell, which opens Notifications', async () => {
    const { fixture, backend, root } = await open();
    backend.expectOne(COUNT).flush({ unread: 3 });
    await settle(fixture);

    const link = root.querySelector('a')!;
    expect(link.getAttribute('href')).toBe('/notifications');
    expect(link.getAttribute('aria-label')).toBe('Notifications, 3 unread');
    expect(text(root, '.bell__badge')).toBe('3');
    backend.verify();
  });

  it('hides the number at 0 and shows 99+ above 99', async () => {
    const { fixture, backend, root } = await open();
    backend.expectOne(COUNT).flush({ unread: 0 });
    await settle(fixture);
    expect(root.querySelector('.bell__badge')).toBeNull();
    expect(root.querySelector('a')?.getAttribute('aria-label')).toBe('Notifications');

    TestBed.inject(NotificationBellState).setUnread(132);
    await settle(fixture);
    expect(text(root, '.bell__badge')).toBe('99+');
  });

  it('is not shown, and asks nothing, to someone without notifications', async () => {
    const { fixture, backend, root } = await open([]);
    await settle(fixture);
    expect(root.querySelector('a')).toBeNull();
    backend.expectNone(COUNT);
  });

  it('reads the count again when the tab comes back, at most every 30 seconds, with no timer', async () => {
    const { fixture, backend } = await open();
    backend.expectOne(COUNT).flush({ unread: 1 });
    await settle(fixture);

    document.dispatchEvent(new Event('visibilitychange'));
    backend.expectNone(COUNT);

    const later = Date.now() + 31_000;
    const now = vi.spyOn(Date, 'now').mockReturnValue(later);
    document.dispatchEvent(new Event('visibilitychange'));
    backend.expectOne(COUNT).flush({ unread: 4 });
    now.mockRestore();
    await settle(fixture);
    expect(TestBed.inject(NotificationBellState).unread()).toBe(4);
  });
});
