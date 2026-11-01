import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Observable, of, throwError } from 'rxjs';
import type { Mock } from 'vitest';
import { SessionState } from '../../core/session/session-state';
import { buttonNamed, settle, text, texts, type } from '../../testing/dom';
import { signInForTest } from '../../testing/session-fixture';
import { AssignDialog, AssignTarget } from './assign-dialog';
import {
  HR_PERMISSIONS,
  annex,
  riverside,
  ruleViolation,
  validationError,
  workerWithPay,
} from './workforce.fixtures';
import { WorkforceApi } from './workforce-api';

type Call = (...args: unknown[]) => Observable<unknown>;
const stub = (implementation: Call): Mock<Call> => vi.fn<Call>(implementation);

async function open(
  workers: readonly AssignTarget[],
  api: Record<string, Mock<Call>>,
  options: { crew?: boolean; summary?: string } = {},
) {
  TestBed.configureTestingModule({
    providers: [
      provideHttpClient(),
      provideHttpClientTesting(),
      { provide: WorkforceApi, useValue: { projectChoices: () => of([riverside, annex]), ...api } },
    ],
  });
  await signInForTest(TestBed.inject(SessionState), TestBed.inject(HttpTestingController), {
    roles: ['Custom'],
    permissions: HR_PERMISSIONS,
  });
  const fixture = TestBed.createComponent(AssignDialog);
  fixture.componentRef.setInput('workers', workers);
  fixture.componentRef.setInput('summary', options.summary ?? 'Juan Dela Cruz');
  fixture.componentRef.setInput('crew', options.crew ?? false);
  const assigned = vi.fn();
  fixture.componentInstance.assigned.subscribe(assigned);
  await settle(fixture);
  return { fixture, root: fixture.nativeElement as HTMLElement, assigned };
}

const pick = (root: HTMLElement, selector: string, value: string) => {
  const select = root.querySelector<HTMLSelectElement>(selector)!;
  select.value = value;
  select.dispatchEvent(new Event('change', { bubbles: true }));
};

describe('AssignDialog for one worker', () => {
  it('posts the worker with the version of the worker the person opened, and hands back the updated worker', async () => {
    const updated = workerWithPay({ version: 5 });
    const assign = stub(() => of(updated));
    const { root, fixture, assigned } = await open([{ id: 'w1', version: 4 }], { assign });

    expect(text(root, '.dialog__title')).toBe('Assign to a project');
    pick(root, 'select[formControlName="projectId"]', 'p1');
    await settle(fixture);
    // The project has a sub-project, so it can be chosen, but is not required.
    expect(texts(root, 'select[formControlName="subProjectId"] option')).toEqual([
      'Whole project',
      'Tower A',
    ]);
    pick(root, 'select[formControlName="subProjectId"]', 's1');
    type(root, '[formControlName="startDate"]', '2026-11-02');
    buttonNamed(root, 'Assign worker').click();
    await settle(fixture);

    expect(assign).toHaveBeenCalledWith('w1', {
      projectId: 'p1',
      subProjectId: 's1',
      startDate: '2026-11-02',
      version: 4,
    });
    expect(assigned).toHaveBeenCalledWith(updated);
  });

  it('offers no sub-project for a project that has none, and forgets the sub-project when the project changes', async () => {
    const assign = stub(() => of(workerWithPay()));
    const { root, fixture } = await open([{ id: 'w1', version: 4 }], { assign });

    pick(root, 'select[formControlName="projectId"]', 'p1');
    await settle(fixture);
    pick(root, 'select[formControlName="subProjectId"]', 's1');
    pick(root, 'select[formControlName="projectId"]', 'p2');
    await settle(fixture);

    expect(root.querySelector('select[formControlName="subProjectId"]')).toBeNull();
    buttonNamed(root, 'Assign worker').click();
    await settle(fixture);
    expect(assign.mock.calls[0]![1]).toMatchObject({ projectId: 'p2', subProjectId: null });
  });

  it('puts the API’s date message under the start date and keeps the dialog open', async () => {
    const assign = stub(() =>
      throwError(() =>
        validationError({
          startDate: ['Choose a date after 2 Mar 2026, when the current posting started.'],
        }),
      ),
    );
    const { root, fixture, assigned } = await open([{ id: 'w1', version: 4 }], { assign });

    pick(root, 'select[formControlName="projectId"]', 'p2');
    buttonNamed(root, 'Assign worker').click();
    await settle(fixture);

    expect(text(root, 'app-form-field:has([formControlName="startDate"]) .field__error')).toBe(
      'Choose a date after 2 Mar 2026, when the current posting started.',
    );
    expect(assigned).not.toHaveBeenCalled();
    expect(root.querySelector('dialog[open]')).not.toBeNull();
  });

  it('shows a rule the API enforces, in its words, above the form', async () => {
    const assign = stub(() =>
      throwError(() => ruleViolation('Juan Dela Cruz is already assigned there.')),
    );
    const { root, fixture } = await open([{ id: 'w1', version: 4 }], { assign });

    pick(root, 'select[formControlName="projectId"]', 'p1');
    buttonNamed(root, 'Assign worker').click();
    await settle(fixture);

    expect(text(root, 'app-api-error-alert')).toContain(
      'Juan Dela Cruz is already assigned there.',
    );
  });
});

describe('AssignDialog for a crew', () => {
  it('posts even one ticked worker through the crew request, which carries no worker versions', async () => {
    const assignMany = stub(() => of([]));
    const { root, fixture, assigned } = await open(
      [{ id: 'w1', version: 4 }],
      { assignMany },
      { crew: true },
    );

    pick(root, 'select[formControlName="projectId"]', 'p2');
    buttonNamed(root, 'Assign worker').click();
    await settle(fixture);

    expect(assignMany).toHaveBeenCalledTimes(1);
    const request = assignMany.mock.calls[0]![0] as Record<string, unknown>;
    expect(request).toMatchObject({ workerIds: ['w1'], projectId: 'p2', subProjectId: null });
    expect(Object.keys(request)).not.toContain('version');
    expect(assigned).toHaveBeenCalledWith(null);
  });
});
