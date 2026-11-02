import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ChangeDetectionStrategy, Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { Observable, of } from 'rxjs';
import type { Mock } from 'vitest';
import { SessionState } from '../../core/session/session-state';
import { buttonNamed, settle, text, texts } from '../../testing/dom';
import { pageOf, signInForTest } from '../../testing/session-fixture';
import { CollaborationApi } from './collaboration-api';
import { READER, notification } from './collaboration.fixtures';
import { AppNotification } from './collaboration.models';
import { NotificationBellState } from './notification-bell-state';
import { NotificationsPage } from './notifications-page';

@Component({ template: '', changeDetection: ChangeDetectionStrategy.OnPush })
class Blank {}

type Call = (...args: unknown[]) => Observable<unknown>;
const stub = (implementation: Call): Mock<Call> => vi.fn<Call>(implementation);

async function open(
  items: readonly AppNotification[],
  api: Record<string, Mock<Call>> = {},
  unread = 2,
) {
  const calls = {
    notifications: stub(() => of(pageOf(items))),
    unreadCount: stub(() => of({ unread })),
    markRead: stub(() => of({ unread: unread - 1 })),
    markAllRead: stub(() => of({ unread: 0 })),
    ...api,
  };
  TestBed.configureTestingModule({
    providers: [
      provideRouter([
        { path: 'notifications', component: NotificationsPage },
        { path: '**', component: Blank },
      ]),
      provideHttpClient(),
      provideHttpClientTesting(),
      { provide: CollaborationApi, useValue: calls },
    ],
  });
  await signInForTest(TestBed.inject(SessionState), TestBed.inject(HttpTestingController), {
    roles: ['Viewer'],
    permissions: READER,
  });
  const harness = await RouterTestingHarness.create();
  await harness.navigateByUrl('/notifications', NotificationsPage);
  await settle(harness.fixture);
  return { harness, root: harness.fixture.nativeElement as HTMLElement, calls };
}

const mention = notification({
  id: 'n2',
  kind: 'Mention',
  message: 'Pia Santos mentioned you on change order CO-002 on Riverside Residences.',
  targetKind: 'ChangeOrder',
  targetId: 'co2',
});
const decided = notification({
  id: 'n3',
  kind: 'DraftDecided',
  message: 'Ben Cruz approved purchase order PO-0012.',
  targetKind: 'PurchaseOrder',
  targetId: 'po12',
  isRead: true,
});

describe('NotificationsPage', () => {
  it('lists what happened newest first, says which are new and links each to its record', async () => {
    const { root, calls } = await open([notification(), mention, decided]);

    expect(calls['notifications']).toHaveBeenCalledWith(false, 1, 25);
    expect(calls['unreadCount']).toHaveBeenCalled();
    expect(texts(root, '.item__message')).toEqual([
      'Pia Santos assigned you a task on Riverside Residences.',
      'Pia Santos mentioned you on change order CO-002 on Riverside Residences.',
      'Ben Cruz approved purchase order PO-0012.',
    ]);
    expect(texts(root, '.item__new')).toEqual(['New', 'New']);
    expect(
      Array.from(root.querySelectorAll('.item__message')).map((a) => a.getAttribute('href')),
    ).toEqual(['/tasks/t1', '/projects/p1/changes?order=co2', '/purchasing/orders/po12']);
    expect(buttonNamed(root, 'Mark all as read')).toBeTruthy();
  });

  it('marks one read when it is opened, and the bell takes the new count', async () => {
    const { harness, root, calls } = await open([notification()], {}, 3);

    root.querySelector<HTMLAnchorElement>('.item__message')!.click();
    await settle(harness.fixture);

    expect(calls['markRead']).toHaveBeenCalledWith('n1');
    expect(TestBed.inject(NotificationBellState).unread()).toBe(2);
    expect(TestBed.inject(Router).url).toBe('/tasks/t1');
  });

  it('marks all as read', async () => {
    const { harness, root, calls } = await open([notification(), mention]);

    buttonNamed(root, 'Mark all as read').click();
    await settle(harness.fixture);

    expect(calls['markAllRead']).toHaveBeenCalled();
    expect(TestBed.inject(NotificationBellState).unread()).toBe(0);
    expect(calls['notifications']).toHaveBeenCalledTimes(2);
    expect(root.textContent).not.toContain('Mark all as read');
  });

  it('says what will appear here before anything has happened, with no filter to show', async () => {
    const { root } = await open([], {}, 0);
    expect(text(root, 'app-empty-state')).toContain('No notifications yet');
    expect(root.querySelector('.filter-chips')).toBeNull();
  });
});
