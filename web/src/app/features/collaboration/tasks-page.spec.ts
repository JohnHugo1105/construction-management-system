import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ChangeDetectionStrategy, Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { Observable, of } from 'rxjs';
import type { Mock } from 'vitest';
import { SessionState } from '../../core/session/session-state';
import { buttonNamed, settle, text, texts, type } from '../../testing/dom';
import { pageOf, signInForTest } from '../../testing/session-fixture';
import { CollaborationApi } from './collaboration-api';
import { WORKER, task, taskItem } from './collaboration.fixtures';
import { TaskListItem } from './collaboration.models';
import { TasksPage } from './tasks-page';

@Component({ template: '', changeDetection: ChangeDetectionStrategy.OnPush })
class Blank {}

type Call = (...args: unknown[]) => Observable<unknown>;
const stub = (implementation: Call): Mock<Call> => vi.fn<Call>(implementation);

interface Options {
  readonly items?: readonly TaskListItem[];
  readonly roles?: readonly string[];
  readonly url?: string;
  readonly api?: Record<string, Mock<Call>>;
}

async function open(options: Options = {}) {
  const calls = {
    listTasks: stub(() => of(pageOf(options.items ?? []))),
    projects: stub(() => of([{ id: 'p1', name: 'Riverside Residences', status: 'Active' }])),
    colleagues: stub(() =>
      of([
        { id: 'user-1', displayName: 'Ana Reyes' },
        { id: 'user-2', displayName: 'Pia Santos' },
      ]),
    ),
    ...options.api,
  };
  TestBed.configureTestingModule({
    providers: [
      provideRouter([
        { path: 'tasks', component: TasksPage },
        { path: 'tasks/:id', component: Blank },
      ]),
      provideHttpClient(),
      provideHttpClientTesting(),
      { provide: CollaborationApi, useValue: calls },
    ],
  });
  await signInForTest(TestBed.inject(SessionState), TestBed.inject(HttpTestingController), {
    roles: options.roles ?? ['ProjectManager'],
    permissions: WORKER,
  });
  const harness = await RouterTestingHarness.create();
  await harness.navigateByUrl(options.url ?? '/tasks', TasksPage);
  await settle(harness.fixture);
  return { harness, root: harness.fixture.nativeElement as HTMLElement, calls };
}

describe('TasksPage', () => {
  it('opens on my open tasks with the due words, who gave each and the project, and leads with Add task', async () => {
    const { root, calls } = await open({
      items: [
        taskItem({ isOverdue: true, dueDetail: '2 days overdue, due 31 Oct 2026' }),
        taskItem({
          id: 't2',
          title: 'Send the revised schedule',
          dueDate: null,
          dueDetail: 'No due date',
          createdByUserId: 'user-1',
          projectId: null,
          projectName: null,
        }),
      ],
    });

    expect(calls['listTasks']).toHaveBeenCalledWith({
      scope: 'assigned',
      status: 'Open',
      projectId: '',
      page: 1,
      pageSize: 25,
    });
    expect(texts(root, '.row-card__title')).toEqual([
      'Check the rebar quantities',
      'Send the revised schedule',
    ]);
    expect(texts(root, '.row-card app-status-chip')).toEqual(['Overdue', 'Open']);
    expect(texts(root, '.row-card:first-child .row-card__meta span')).toEqual([
      '2 days overdue, due 31 Oct 2026',
      'From Pia Santos',
      'Riverside Residences',
    ]);
    expect(texts(root, '.row-card:last-child .row-card__meta span')).toEqual([
      'No due date',
      'From you',
    ]);
    expect(root.querySelector('.row-card a')?.getAttribute('href')).toBe('/tasks/t1');
    expect(root.querySelectorAll('.btn--primary').length).toBe(1);
    expect(text(root, '.btn--primary')).toBe('Add task');
    // Folded until asked for; only an Administrator sees every task.
    expect(root.querySelector('#task-filters')).toBeNull();
    expect(texts(root, '.filter-chip')).toEqual(['Assigned to me', 'Created by me']);
  });

  it('switches to the tasks I created, naming who each is for', async () => {
    const { harness, root, calls } = await open({
      items: [
        taskItem({ assigneeUserId: 'user-3', assigneeName: 'Ben Cruz', createdByUserId: 'user-1' }),
      ],
    });

    buttonNamed(root, 'Created by me').click();
    await settle(harness.fixture);

    expect(calls['listTasks']).toHaveBeenLastCalledWith(
      expect.objectContaining({ scope: 'created' }),
    );
    expect(text(root, '.row-card__meta')).toContain('For Ben Cruz');
    expect(root.querySelector('.filter-chip[aria-pressed="true"]')?.textContent?.trim()).toBe(
      'Created by me',
    );
  });

  it('filters by state and project from the address, and offers every task to an Administrator', async () => {
    const { root, calls } = await open({
      roles: ['Administrator'],
      url: '/tasks?scope=all&status=Done&project=p1',
    });

    expect(calls['listTasks']).toHaveBeenCalledWith({
      scope: 'all',
      status: 'Done',
      projectId: 'p1',
      page: 1,
      pageSize: 25,
    });
    expect(texts(root, '.filter-chip')).toContain('Every task');
    expect(root.querySelector('#task-filters')).not.toBeNull();
    expect(text(root, 'app-empty-state')).toContain('No tasks match');
  });

  it('says nothing is assigned when the list is empty', async () => {
    const { root } = await open();
    expect(text(root, 'app-empty-state')).toContain('Nothing assigned to you');
  });

  it('adds a task for a colleague and opens it', async () => {
    const createTask = stub(() =>
      of(task({ assigneeUserId: 'user-2', assigneeName: 'Pia Santos' })),
    );
    const { harness, root } = await open({ api: { createTask } });

    buttonNamed(root, 'Add task').click();
    await settle(harness.fixture);
    buttonNamed(root, 'Save task').click();
    await settle(harness.fixture);
    expect(createTask).not.toHaveBeenCalled();
    expect(texts(root, '.field__error')).toEqual([
      'Say what needs to be done.',
      'Choose who the task is for.',
    ]);

    type(root, 'input[formcontrolname="title"]', 'Confirm the waterproofing scope');
    const pia = Array.from(root.querySelectorAll<HTMLInputElement>('input[type="radio"]')).find(
      (radio) => radio.closest('label')?.textContent?.includes('Pia Santos'),
    )!;
    pia.click();
    await settle(harness.fixture);
    buttonNamed(root, 'Save task').click();
    await settle(harness.fixture);

    expect(createTask).toHaveBeenCalledWith(
      {
        title: 'Confirm the waterproofing scope',
        description: null,
        dueDate: null,
        assigneeUserId: 'user-2',
        projectId: null,
      },
      expect.any(String),
    );
    expect(TestBed.inject(Router).url).toBe('/tasks/t1');
  });
});
