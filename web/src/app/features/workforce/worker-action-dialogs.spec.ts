import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Type } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Observable, of, throwError } from 'rxjs';
import type { Mock } from 'vitest';
import { SessionState } from '../../core/session/session-state';
import { buttonNamed, settle, text, texts, type } from '../../testing/dom';
import { signInForTest } from '../../testing/session-fixture';
import { AddCaseDialog } from './add-case-dialog';
import { ReinstateDialog } from './reinstate-dialog';
import { ReleaseDialog } from './release-dialog';
import {
  HR_PERMISSIONS,
  riverside,
  ruleViolation,
  validationError,
  workerWithoutPay,
} from './workforce.fixtures';
import { WorkforceApi } from './workforce-api';
import { WorkerDetail } from './workforce.models';

type Call = (...args: unknown[]) => Observable<unknown>;
const stub = (implementation: Call): Mock<Call> => vi.fn<Call>(implementation);

async function open<T>(component: Type<T>, worker: WorkerDetail, api: Record<string, Mock<Call>>) {
  TestBed.configureTestingModule({
    providers: [
      provideHttpClient(),
      provideHttpClientTesting(),
      { provide: WorkforceApi, useValue: { projectChoices: () => of([riverside]), ...api } },
    ],
  });
  await signInForTest(TestBed.inject(SessionState), TestBed.inject(HttpTestingController), {
    roles: ['Custom'],
    permissions: HR_PERMISSIONS,
  });
  const fixture = TestBed.createComponent(component);
  fixture.componentRef.setInput('worker', worker);
  await settle(fixture);
  return { fixture, root: fixture.nativeElement as HTMLElement };
}

describe('ReleaseDialog', () => {
  it('names the project, says attendance stays, and sends the end date with the worker’s version', async () => {
    const release = stub(() => of(workerWithoutPay()));
    const { root, fixture } = await open(ReleaseDialog, workerWithoutPay({ version: 8 }), {
      release,
    });

    expect(text(root, '.dialog__body p')).toContain(
      'Juan Dela Cruz will no longer be posted to Riverside Residences.',
    );
    expect(text(root, '.dialog__body p')).toContain('Attendance already recorded stays as it is.');
    expect(text(root, '.field__hint')).toBe(
      'It must be after 2 Mar 2026, when the posting started.',
    );

    type(root, '[formControlName="endDate"]', '2026-10-30');
    buttonNamed(root, 'Release worker').click();
    await settle(fixture);

    expect(release).toHaveBeenCalledWith('w1', { endDate: '2026-10-30', version: 8 });
  });

  it('shows the API’s date message under the date', async () => {
    const release = stub(() =>
      throwError(() =>
        validationError({ endDate: ['Choose a date after 2 Mar 2026, when the posting started.'] }),
      ),
    );
    const { root, fixture } = await open(ReleaseDialog, workerWithoutPay(), { release });

    buttonNamed(root, 'Release worker').click();
    await settle(fixture);

    expect(texts(root, '.field__error')).toEqual([
      'Choose a date after 2 Mar 2026, when the posting started.',
    ]);
  });
});

describe('ReinstateDialog', () => {
  const gone = workerWithoutPay({
    status: 'Resigned',
    statusChangedOn: '2026-10-20',
    statusReason: 'Moved away',
    version: 5,
  });

  it('says when the worker left and requires a reason', async () => {
    const reinstate = stub(() => of(workerWithoutPay()));
    const { root, fixture } = await open(ReinstateDialog, gone, { reinstate });

    expect(text(root, '.dialog__body p')).toContain('Juan Dela Cruz resigned on 20 Oct 2026.');
    buttonNamed(root, 'Reinstate worker').click();
    await settle(fixture);
    expect(reinstate).not.toHaveBeenCalled();
    expect(text(root, 'app-form-field:has(textarea) .field__error')).toBe(
      'Say why the worker is being reinstated.',
    );

    type(root, '[formControlName="reason"]', 'Asked to return');
    type(root, '[formControlName="effectiveDate"]', '2026-10-28');
    buttonNamed(root, 'Reinstate worker').click();
    await settle(fixture);

    expect(reinstate).toHaveBeenCalledWith('w1', {
      effectiveDate: '2026-10-28',
      reason: 'Asked to return',
      version: 5,
    });
  });

  it('shows a rule the API enforces in its words', async () => {
    const reinstate = stub(() =>
      throwError(() =>
        ruleViolation("Juan Dela Cruz hasn't left, so there is nothing to reinstate."),
      ),
    );
    const { root, fixture } = await open(ReinstateDialog, gone, { reinstate });

    type(root, '[formControlName="reason"]', 'Asked to return');
    buttonNamed(root, 'Reinstate worker').click();
    await settle(fixture);

    expect(text(root, 'app-api-error-alert')).toContain('there is nothing to reinstate');
  });
});

describe('AddCaseDialog', () => {
  it('says a case cannot be edited, and requires what happened', async () => {
    const addCase = stub(() => of(workerWithoutPay()));
    const { root, fixture } = await open(AddCaseDialog, workerWithoutPay({ version: 6 }), {
      addCase,
    });

    expect(text(root, '.dialog__body p')).toContain("Once added it can't be edited or removed.");
    buttonNamed(root, 'Add case').click();
    await settle(fixture);
    expect(addCase).not.toHaveBeenCalled();
    expect(text(root, 'app-form-field:has([formControlName="title"]) .field__error')).toBe(
      'Say what happened.',
    );

    type(root, '[formControlName="title"]', '  Late three days in a row ');
    type(root, '[formControlName="caseDate"]', '2026-10-30');
    const project = root.querySelector<HTMLSelectElement>('select[formControlName="projectId"]')!;
    project.value = 'p1';
    project.dispatchEvent(new Event('change', { bubbles: true }));
    buttonNamed(root, 'Add case').click();
    await settle(fixture);

    expect(addCase).toHaveBeenCalledWith('w1', {
      title: 'Late three days in a row',
      remarks: null,
      caseDate: '2026-10-30',
      projectId: 'p1',
      version: 6,
    });
  });

  it('puts the API’s date message under the date', async () => {
    const addCase = stub(() =>
      throwError(() =>
        validationError({ caseDate: ['Choose a date between the hire date and today.'] }),
      ),
    );
    const { root, fixture } = await open(AddCaseDialog, workerWithoutPay(), { addCase });

    type(root, '[formControlName="title"]', 'Late');
    buttonNamed(root, 'Add case').click();
    await settle(fixture);

    expect(texts(root, '.field__error')).toEqual([
      'Choose a date between the hire date and today.',
    ]);
  });
});
