import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ChangeDetectionStrategy, Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter, withComponentInputBinding } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { Observable, of, throwError } from 'rxjs';
import type { Mock } from 'vitest';
import { ApiError } from '../../core/api/api-error';
import { SessionState } from '../../core/session/session-state';
import { buttonNamed, flushMicrotasks, leave, settle, text, texts, type } from '../../testing/dom';
import { signInForTest } from '../../testing/session-fixture';
import { ProjectFormPage } from './project-form-page';
import { ProjectsApi } from './projects-api';
import { Project } from './projects.models';

@Component({ template: '', changeDetection: ChangeDetectionStrategy.OnPush })
class Blank {}

const existing: Project = {
  id: 'p1',
  name: 'Harbour Tower',
  client: 'Acme Realty',
  location: 'Cebu',
  contractPriceMinor: 125050,
  startDate: '2026-12-03',
  targetEndDate: '2027-03-01',
  status: 'Active',
  archiveReason: null,
  archiveNote: null,
  archivedAtUtc: null,
  subProjects: [],
  version: 7,
};

function validationError(errors: Record<string, string[]>): ApiError {
  return new ApiError({
    status: 422,
    title: 'Some details need your attention',
    detail: 'Check the highlighted fields and try again.',
    code: 'validation_failed',
    errors,
  });
}

type Call = (...args: unknown[]) => Observable<unknown>;
const stub = (implementation: Call): Mock<Call> => vi.fn<Call>(implementation);

async function open(url: string, api: Partial<Record<'create' | 'update' | 'get', Mock<Call>>>) {
  TestBed.configureTestingModule({
    providers: [
      provideRouter(
        [
          { path: 'projects/new', component: ProjectFormPage },
          { path: 'projects/:id/edit', component: ProjectFormPage },
          { path: 'projects/:id', component: Blank },
          { path: 'projects', component: Blank },
        ],
        withComponentInputBinding(),
      ),
      provideHttpClient(),
      provideHttpClientTesting(),
      { provide: ProjectsApi, useValue: api },
    ],
  });
  await signInForTest(TestBed.inject(SessionState), TestBed.inject(HttpTestingController));
  const harness = await RouterTestingHarness.create();
  await harness.navigateByUrl(url, ProjectFormPage);
  await settle(harness.fixture);
  return {
    harness,
    root: harness.fixture.nativeElement as HTMLElement,
    router: TestBed.inject(Router),
  };
}

const field = (root: HTMLElement, name: string) =>
  root.querySelector<HTMLInputElement>(`[formControlName="${name}"]`)!;
const nameInput = (root: HTMLElement) => field(root, 'name');
const priceInput = (root: HTMLElement) =>
  root.querySelector<HTMLInputElement>('app-money-input input')!;
const saveButton = (root: HTMLElement) => buttonNamed(root, 'Save project');

describe('ProjectFormPage (new project)', () => {
  it('sends trimmed text, the price in centavos, and dates as typed, then opens the project', async () => {
    const create = stub(() => of({ ...existing, id: 'new-1' }));
    const { harness, root, router } = await open('/projects/new', { create });

    type(root, '[formControlName="name"]', '  Harbour Tower  ');
    type(root, '[formControlName="client"]', '   ');
    type(root, 'app-money-input input', '1,250.50');
    type(root, '[formControlName="startDate"]', '2026-12-03');
    type(root, '[formControlName="targetEndDate"]', '2027-03-01');
    saveButton(root).click();
    await settle(harness.fixture);

    expect(create).toHaveBeenCalledWith({
      name: 'Harbour Tower',
      client: null,
      location: null,
      contractPriceMinor: 125050,
      startDate: '2026-12-03',
      targetEndDate: '2027-03-01',
    });
    await flushMicrotasks(6);
    expect(router.url).toBe('/projects/new-1');
  });

  it('leaves the price empty when none is given, rather than sending zero', async () => {
    const create = stub(() => of({ ...existing, id: 'new-2' }));
    const { harness, root } = await open('/projects/new', { create });

    type(root, '[formControlName="name"]', 'Riverside Clinic');
    saveButton(root).click();
    await settle(harness.fixture);

    expect(create.mock.calls[0]![0]).toMatchObject({
      name: 'Riverside Clinic',
      contractPriceMinor: null,
    });
  });

  it('puts each server message under the field it is about, and moves the keyboard to the first one', async () => {
    const create = stub(() =>
      throwError(() =>
        validationError({
          name: ['You already have a project named "Harbour Tower". Choose a different name.'],
          // The API calls the price "contractPriceMinor"; the form calls it the contract price.
          contractPriceMinor: ["The contract price can't be negative."],
          targetEndDate: ["The target end date can't be before the start date."],
        }),
      ),
    );
    const { harness, root } = await open('/projects/new', { create });

    type(root, '[formControlName="name"]', 'Harbour Tower');
    saveButton(root).click();
    await settle(harness.fixture);

    const errorFor = (control: HTMLElement) =>
      text(control.closest('app-form-field')!, '.field__error');
    expect(errorFor(nameInput(root))).toBe(
      'You already have a project named "Harbour Tower". Choose a different name.',
    );
    expect(errorFor(priceInput(root))).toBe("The contract price can't be negative.");
    expect(errorFor(field(root, 'targetEndDate'))).toBe(
      "The target end date can't be before the start date.",
    );
    expect(nameInput(root).getAttribute('aria-invalid')).toBe('true');
    expect(priceInput(root).getAttribute('aria-invalid')).toBe('true');
    expect(document.activeElement).toBe(nameInput(root));
    // The field messages are not repeated in a general error block.
    expect(text(root, 'app-api-error-alert')).toBe(
      'Some details need your attention Check the highlighted fields and try again.',
    );
  });

  it('keeps what the person typed when the server refuses', async () => {
    const create = stub(() => throwError(() => validationError({ name: ['Taken.'] })));
    const { harness, root } = await open('/projects/new', { create });

    type(root, '[formControlName="name"]', 'Harbour Tower');
    type(root, '[formControlName="client"]', 'Acme Realty');
    type(root, 'app-money-input input', '1,250.50');
    saveButton(root).click();
    await settle(harness.fixture);

    expect(nameInput(root).value).toBe('Harbour Tower');
    expect(field(root, 'client').value).toBe('Acme Realty');
    expect(priceInput(root).value).toBe('1,250.50');
  });

  it('clears a server message as soon as that field is changed', async () => {
    const create = stub(() => throwError(() => validationError({ name: ['Taken.'] })));
    const { harness, root } = await open('/projects/new', { create });
    type(root, '[formControlName="name"]', 'Harbour Tower');
    saveButton(root).click();
    await settle(harness.fixture);
    expect(root.querySelectorAll('.field__error')).toHaveLength(1);

    type(root, '[formControlName="name"]', 'Harbour Tower 2');
    await settle(harness.fixture);

    expect(root.querySelectorAll('.field__error')).toHaveLength(0);
    expect(nameInput(root).getAttribute('aria-invalid')).toBeNull();
  });

  it('shows messages the form has no field for, above the form', async () => {
    const create = stub(() =>
      throwError(() =>
        validationError({ somethingElse: ['A message for a field this form does not have.'] }),
      ),
    );
    const { harness, root } = await open('/projects/new', { create });
    type(root, '[formControlName="name"]', 'Harbour Tower');

    saveButton(root).click();
    await settle(harness.fixture);

    expect(text(root, 'app-api-error-alert')).toContain(
      'A message for a field this form does not have.',
    );
  });

  it('checks fields before sending anything, with a short summary and plain messages', async () => {
    const create = stub(() => of(null));
    const { harness, root } = await open('/projects/new', { create });

    type(root, 'app-money-input input', '1.234');
    type(root, '[formControlName="startDate"]', '2026-12-03');
    type(root, '[formControlName="targetEndDate"]', '2026-11-01');
    saveButton(root).click();
    await settle(harness.fixture);

    expect(create).not.toHaveBeenCalled();
    expect(texts(root, '.field__error')).toEqual([
      'Enter the project name.',
      'Use at most two decimal places, like 1,250.50.',
      "The target end date can't be before the start date.",
    ]);
    expect(text(root, '.alert__title')).toBe('Some details need your attention');
    expect(document.activeElement).toBe(nameInput(root));
  });

  it('validates a field when the person leaves it, not before', async () => {
    const { harness, root } = await open('/projects/new', { create: stub(() => of(null)) });
    expect(root.querySelectorAll('.field__error')).toHaveLength(0);

    nameInput(root).focus();
    leave(root, '[formControlName="name"]');
    await settle(harness.fixture);

    expect(text(root, '.field__error')).toBe('Enter the project name.');
  });

  it('shows the price tidily once the person leaves the field', async () => {
    const { harness, root } = await open('/projects/new', { create: stub(() => of(null)) });

    type(root, 'app-money-input input', '1250.5');
    leave(root, 'app-money-input input');
    await settle(harness.fixture);

    expect(priceInput(root).value).toBe('1,250.50');
  });

  it('labels the optional fields and keeps the labels above the fields', async () => {
    const { root } = await open('/projects/new', { create: stub(() => of(null)) });

    expect(texts(root, '.field__label')).toEqual([
      'Project name',
      'Client (optional)',
      'Location (optional)',
      'Contract price (optional)',
      'Start date',
      'Target end date (optional)',
    ]);
    // Every label is tied to its control.
    for (const label of Array.from(root.querySelectorAll<HTMLLabelElement>('label.field__label'))) {
      expect(root.querySelector(`#${label.getAttribute('for')}`)).not.toBeNull();
    }
  });
});

describe('ProjectFormPage (editing)', () => {
  it('shows the project and sends back the version it opened', async () => {
    const get = stub(() => of(existing));
    const update = stub(() => of({ ...existing, version: 8 }));
    const { harness, root, router } = await open('/projects/p1/edit', { get, update });

    expect(nameInput(root).value).toBe('Harbour Tower');
    expect(priceInput(root).value).toBe('1,250.50');
    expect(field(root, 'targetEndDate').value).toBe('2027-03-01');

    type(root, '[formControlName="name"]', 'Harbour Tower Phase 2');
    saveButton(root).click();
    await settle(harness.fixture);

    expect(update).toHaveBeenCalledWith(
      'p1',
      {
        name: 'Harbour Tower Phase 2',
        client: 'Acme Realty',
        location: 'Cebu',
        contractPriceMinor: 125050,
        startDate: '2026-12-03',
        targetEndDate: '2027-03-01',
      },
      7,
    );
    await flushMicrotasks(6);
    expect(router.url).toBe('/projects/p1');
  });

  it('tells the person someone else changed it, and Reload brings in the latest version', async () => {
    const get = stub(() =>
      of({ ...existing, name: 'Harbour Tower (renamed)', version: 9 }),
    ).mockReturnValueOnce(of(existing));
    const update = stub(() =>
      throwError(
        () =>
          new ApiError({
            status: 409,
            title: 'This record changed',
            detail:
              'Someone else changed this record while you were editing. Reload it to see the latest version, then try again.',
            code: 'concurrency_conflict',
          }),
      ),
    );
    const { harness, root } = await open('/projects/p1/edit', { get, update });

    saveButton(root).click();
    await settle(harness.fixture);

    expect(text(root, 'app-api-error-alert')).toContain(
      'Someone else changed this record while you were editing.',
    );
    buttonNamed(root, 'Reload project').click();
    await settle(harness.fixture);

    expect(get).toHaveBeenCalledTimes(2);
    expect(nameInput(root).value).toBe('Harbour Tower (renamed)');
    expect(root.querySelector('app-api-error-alert .alert')).toBeNull();

    // The next save carries the new version.
    update.mockReturnValue(of({ ...existing, version: 10 }));
    saveButton(root).click();
    await settle(harness.fixture);
    expect(update.mock.calls.at(-1)![2]).toBe(9);
  });

  it('does not offer a form for an archived project, and says how to get one', async () => {
    const get = stub(() => of({ ...existing, status: 'Archived' as const }));
    const { root } = await open('/projects/p1/edit', { get });

    expect(text(root, 'app-alert')).toContain(
      "An archived project can't be edited. Restore it first",
    );
    expect(root.querySelector('form')).toBeNull();
    expect(buttonNamed(root, 'Go to project').getAttribute('href')).toBe('/projects/p1');
  });

  it('offers to try again when the project cannot be loaded', async () => {
    const get = stub(() =>
      throwError(
        () =>
          new ApiError({
            status: 500,
            title: 'Something went wrong',
            detail: "We couldn't finish that. Try again in a moment.",
          }),
      ),
    );
    const { root } = await open('/projects/p1/edit', { get });

    expect(root.querySelector('form')).toBeNull();
    expect(buttonNamed(root, 'Try again')).toBeTruthy();
  });
});
