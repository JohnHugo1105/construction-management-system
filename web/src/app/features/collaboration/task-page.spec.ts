import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { Observable, of, throwError } from 'rxjs';
import type { Mock } from 'vitest';
import { ApiError } from '../../core/api/api-error';
import { SessionState } from '../../core/session/session-state';
import { buttonNamed, settle, text, texts } from '../../testing/dom';
import { signInForTest } from '../../testing/session-fixture';
import { CollaborationApi } from './collaboration-api';
import { WORKER, commentPage, task } from './collaboration.fixtures';
import { WorkTask } from './collaboration.models';
import { TaskPage } from './task-page';

type Call = (...args: unknown[]) => Observable<unknown>;
const stub = (implementation: Call): Mock<Call> => vi.fn<Call>(implementation);

async function open(current: WorkTask, api: Record<string, Mock<Call>> = {}) {
  const calls = {
    getTask: stub(() => of(current)),
    comments: stub(() => of(commentPage([]))),
    colleagues: stub(() => of([{ id: 'user-3', displayName: 'Ben Cruz' }])),
    ...api,
  };
  TestBed.configureTestingModule({
    providers: [
      provideRouter([{ path: 'tasks/:id', component: TaskPage }], withComponentInputBinding()),
      provideHttpClient(),
      provideHttpClientTesting(),
      { provide: CollaborationApi, useValue: calls },
    ],
  });
  await signInForTest(TestBed.inject(SessionState), TestBed.inject(HttpTestingController), {
    roles: ['ProjectManager'],
    permissions: WORKER,
  });
  const harness = await RouterTestingHarness.create();
  await harness.navigateByUrl(`/tasks/${current.id}`, TaskPage);
  await settle(harness.fixture);
  return { fixture: harness.fixture, root: harness.fixture.nativeElement as HTMLElement, calls };
}

describe('TaskPage', () => {
  it('shows what to do, for whom, by when, the record it is about and its discussion, led by Mark as done', async () => {
    const { root, calls } = await open(task());

    expect(text(root, '.head')).toBe('Open Due 4 Nov 2026');
    expect(texts(root, '.def-list dt')).toEqual(['For', 'From', 'Project', 'About']);
    expect(texts(root, '.def-list dd')).toEqual([
      'You',
      'Pia Santos',
      'Riverside Residences',
      'Purchase order PO-0012',
    ]);
    expect(root.querySelector('.def-list a[href="/purchasing/orders/po1"]')).not.toBeNull();
    expect(text(root, '.description')).toBe(
      'Compare the bars on the purchase orders with revision C.',
    );
    expect(texts(root, '.actions button')).toEqual(['Mark as done']);
    expect(root.querySelector('app-discussion')).not.toBeNull();
    expect(calls['comments']).toHaveBeenCalledWith('Task', 't1', 1, 20);
  });

  it('marks it done and then offers nothing more to the assignee', async () => {
    const done = task({
      status: 'Done',
      dueDetail: null,
      completedAtUtc: '2026-11-02T07:10:00Z',
      completedByName: 'Ana Reyes',
      version: 2,
      actions: { complete: false, cancel: false, reassign: false, reopen: false },
    });
    const completeTask = stub(() => of(done));
    const { fixture, root } = await open(task(), { completeTask });

    buttonNamed(root, 'Mark as done').click();
    await settle(fixture);

    expect(completeTask).toHaveBeenCalledWith('t1', 1);
    expect(text(root, '.head')).toBe('Done');
    expect(root.querySelector('.actions')).toBeNull();
    expect(root.textContent).toContain('Done by Ana Reyes, 2 Nov 2026, 3:10 pm.');
  });

  it('gives the creator Reassign and Cancel task, and reassigns to the chosen colleague', async () => {
    const mine = task({
      assigneeUserId: 'user-2',
      assigneeName: 'Pia Santos',
      createdByUserId: 'user-1',
      createdByName: 'Ana Reyes',
      actions: { complete: true, cancel: true, reassign: true, reopen: false },
    });
    const reassignTask = stub(() =>
      of({ ...mine, assigneeUserId: 'user-3', assigneeName: 'Ben Cruz', version: 2 }),
    );
    const { fixture, root } = await open(mine, { reassignTask });

    expect(texts(root, '.actions button')).toEqual(['Mark as done', 'Reassign', 'Cancel task']);
    buttonNamed(root, 'Reassign').click();
    await settle(fixture);
    root.querySelector<HTMLInputElement>('dialog input[type="radio"]')!.click();
    await settle(fixture);
    buttonNamed(root, 'Reassign task').click();
    await settle(fixture);

    expect(reassignTask).toHaveBeenCalledWith('t1', 'user-3', 1);
    expect(texts(root, '.def-list dd')[0]).toBe('Ben Cruz');
  });

  it('says a task on an archived project is read-only, and a stale save asks to reload', async () => {
    const archived = task({
      projectArchived: true,
      actions: { complete: false, cancel: false, reassign: false, reopen: false },
    });
    const { root } = await open(archived);
    expect(root.textContent).toContain(
      'This project is archived, so this task can be read but not changed.',
    );
    expect(root.querySelector('.actions')).toBeNull();

    TestBed.resetTestingModule();
    const conflict = new ApiError({
      status: 409,
      title: 'Someone else changed this',
      detail: 'Reload to see the latest.',
      code: 'concurrency_conflict',
    });
    const second = await open(task(), { completeTask: stub(() => throwError(() => conflict)) });
    buttonNamed(second.root, 'Mark as done').click();
    await settle(second.fixture);
    expect(text(second.root, 'app-alert')).toContain('Someone else changed this');
    expect(buttonNamed(second.root, 'Reload task')).toBeTruthy();
  });
});
