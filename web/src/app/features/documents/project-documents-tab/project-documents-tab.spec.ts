import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { Observable, of } from 'rxjs';
import type { Mock } from 'vitest';
import { ToastService } from '../../../core/notifications/toast-service';
import { SessionState } from '../../../core/session/session-state';
import { buttonNamed, settle, text, texts, type } from '../../../testing/dom';
import { signInForTest } from '../../../testing/session-fixture';
import { ProjectContext } from '../../projects/project-context';
import { Project } from '../../projects/projects.models';
import { DocumentsApi } from '../documents-api';
import {
  MANAGER,
  PROJECT_ID,
  READER,
  UPLOADS_NOT_SET_UP,
  documentItem,
  documentOf,
  fileVersion,
  linkVersion,
  listOf,
  permit,
} from '../documents.fixtures';
import { DocumentList } from '../documents.models';
import { ProjectDocumentsTab } from './project-documents-tab';

type Call = (...args: unknown[]) => Observable<unknown>;
const stub = (implementation: Call): Mock<Call> => vi.fn<Call>(implementation);

const riverside: Project = {
  id: PROJECT_ID,
  name: 'Riverside Residences',
  client: null,
  location: null,
  contractPriceMinor: null,
  startDate: '2026-01-05',
  targetEndDate: null,
  status: 'Active',
  archiveReason: null,
  archiveNote: null,
  archivedAtUtc: null,
  subProjects: [],
  version: 2,
};

const plan = documentItem({ current: fileVersion({ number: 2 }), version: 3 });
const expiring = permit();
const voided = documentItem({
  id: 'd-old',
  title: 'Old site plan',
  status: 'Void',
  current: linkVersion(),
});

interface Options {
  readonly list?: DocumentList;
  readonly permissions?: readonly string[];
  readonly project?: Project;
  readonly url?: string;
  readonly api?: Record<string, Mock<Call>>;
}

async function open(options: Options = {}) {
  const list = stub(() => of(options.list ?? listOf([plan, expiring, voided])));
  const api: Record<string, unknown> = {
    list,
    fileUrl: (id: string, n: number) => `/api/documents/${id}/versions/${n}/file`,
    get: stub(() => of(documentOf(plan, [fileVersion({ number: 2 }), linkVersion()]))),
    ...options.api,
  };
  TestBed.configureTestingModule({
    providers: [
      provideRouter([{ path: 'projects/:id/documents', component: ProjectDocumentsTab }]),
      provideHttpClient(),
      provideHttpClientTesting(),
      ProjectContext,
      { provide: DocumentsApi, useValue: api },
    ],
  });
  await signInForTest(TestBed.inject(SessionState), TestBed.inject(HttpTestingController), {
    roles: ['Custom'],
    permissions: options.permissions ?? MANAGER,
  });
  TestBed.inject(ProjectContext).replace(options.project ?? riverside);
  const harness = await RouterTestingHarness.create();
  await harness.navigateByUrl(options.url ?? `/projects/${PROJECT_ID}/documents`, ProjectDocumentsTab);
  await settle(harness.fixture);
  return {
    harness,
    root: harness.fixture.nativeElement as HTMLElement,
    list,
    toast: TestBed.inject(ToastService),
  };
}

const submit = () => document.body.querySelector<HTMLButtonElement>('button[type="submit"]')!.click();

describe('ProjectDocumentsTab: the list', () => {
  it('asks for this project’s first page and groups the documents by kind, voided ones folded away last', async () => {
    const { root, list } = await open();

    expect(list).toHaveBeenCalledWith(PROJECT_ID, '', 1, 25);
    expect(texts(root, '.group__title')).toEqual(['Drawings', 'Permits']);
    expect(texts(root, '.group .row-card__title')).toEqual(['Ground floor plan', 'Building permit']);
    expect(text(root, '.voided__summary')).toBe('Voided (1)');
    expect(root.querySelector('details.voided')!.hasAttribute('open')).toBe(false);
  });

  it('shows the newest version, an expiry chip in words, and a download for a file and a link for a link', async () => {
    const { root } = await open();

    const [planCard, permitCard] = Array.from(root.querySelectorAll<HTMLElement>('.group .row-card'));
    expect(text(planCard!, '.text-muted')).toBe('Version 2 · PDF, 1.2 MB · Ana Reyes, 3 Dec 2026');
    expect(planCard!.querySelector('app-status-chip')).toBeNull();
    const download = planCard!.querySelector<HTMLAnchorElement>('a[appButton]')!;
    expect(download.textContent?.trim()).toBe('Download');
    expect(download.getAttribute('href')).toBe('/api/documents/d-plan/versions/2/file');
    expect(download.hasAttribute('download')).toBe(true);

    expect(text(permitCard!, 'app-status-chip')).toBe('Expires in 12 days');
    const link = permitCard!.querySelector<HTMLAnchorElement>('a[appButton]')!;
    expect(link.textContent?.trim()).toBe('Open link');
    expect(link.getAttribute('href')).toBe('https://files.example.com/permit.pdf');
    expect(link.getAttribute('rel')).toBe('noopener noreferrer');
  });

  it('gives a manager one primary action and the rest under More', async () => {
    const { root } = await open();

    expect(texts(root, '.toolbar button')).toEqual(['Add document']);
    expect(root.querySelectorAll('.group app-menu')).toHaveLength(2);
    // A voided document can still be opened but not changed.
    expect(root.querySelector('.voided app-menu')).toBeNull();
  });

  it('gives a reader downloads and earlier versions only', async () => {
    const { root } = await open({ permissions: READER });

    expect(root.querySelector('.toolbar')).toBeNull();
    expect(root.querySelector('app-menu')).toBeNull();
    expect(texts(root, '.group .row-card button')).toEqual(['Earlier versions']);
  });

  it('lists every version of a document, each still downloadable', async () => {
    const { root, harness } = await open({ permissions: READER });

    buttonNamed(root, 'Earlier versions').click();
    await settle(harness.fixture);

    const items = document.body.querySelectorAll('.versions__item');
    expect(items).toHaveLength(2);
    expect(
      Array.from(items).map((item) => item.querySelector('a')!.getAttribute('href')),
    ).toEqual(['/api/documents/d-plan/versions/2/file', 'https://files.example.com/permit.pdf']);
  });

  it('says the project is archived and offers no changes there', async () => {
    const { root } = await open({ project: { ...riverside, status: 'Archived' } });

    expect(root.textContent).toContain('Nothing can be added or changed until the project is restored.');
    expect(root.querySelector('.toolbar')).toBeNull();
    expect(root.querySelector('app-menu')).toBeNull();
  });

  it('leads an empty project to adding the first document, and a reader to plain facts', async () => {
    const manager = await open({ list: listOf([]) });
    expect(text(manager.root, 'app-empty-state')).toContain('No documents yet');
    expect(texts(manager.root, 'app-empty-state button')).toEqual(['Add document']);
    TestBed.resetTestingModule();

    const reader = await open({ list: listOf([]), permissions: READER });
    expect(text(reader.root, 'app-empty-state')).toContain(
      'Documents appear here once someone adds them.',
    );
    expect(reader.root.querySelector('button')).toBeNull();
  });

  it('shows the kind filter only when there is more than one page, and reads the kind from the address', async () => {
    const short = await open();
    expect(short.root.querySelector('.toolbar select')).toBeNull();
    TestBed.resetTestingModule();

    const { root, list } = await open({
      url: `/projects/${PROJECT_ID}/documents?kind=Permit`,
      list: listOf([expiring]),
    });
    expect(list).toHaveBeenCalledWith(PROJECT_ID, 'Permit', 1, 25);
    const select = root.querySelector<HTMLSelectElement>('.toolbar select')!;
    expect(select.value).toBe('Permit');
  });
});

describe('ProjectDocumentsTab: adding and changing', () => {
  it('adds a link when file storage is not set up, saying why the upload is off, and keeps the list fresh', async () => {
    const createLink = stub(() => of(documentOf(expiring)));
    const { root, harness, list, toast } = await open({
      list: listOf([plan], { uploads: UPLOADS_NOT_SET_UP }),
      api: { createLink },
    });
    const success = vi.spyOn(toast, 'success');

    buttonNamed(root, 'Add document').click();
    await settle(harness.fixture);

    const fileChoice = document.body.querySelector<HTMLInputElement>('input[name="document-method-off"]')!;
    expect(fileChoice.disabled).toBe(true);
    expect(document.body.textContent).toContain(UPLOADS_NOT_SET_UP.message);
    expect(document.body.querySelector<HTMLInputElement>('input[value="link"]')!.checked).toBe(true);
    expect(document.body.querySelector('input[type="file"]')).toBeNull();

    document.body.querySelector<HTMLSelectElement>('select[formcontrolname="category"]')!.value = 'Permit';
    document.body
      .querySelector<HTMLSelectElement>('select[formcontrolname="category"]')!
      .dispatchEvent(new Event('change'));
    await settle(harness.fixture);
    type(document.body, 'input[formcontrolname="title"]', ' Building permit ');
    type(document.body, 'input[formcontrolname="url"]', 'https://files.example.com/permit.pdf');
    type(document.body, 'input[formcontrolname="validUntil"]', '2027-01-31');
    submit();
    await settle(harness.fixture);

    expect(createLink).toHaveBeenCalledWith(
      PROJECT_ID,
      {
        title: 'Building permit',
        description: null,
        category: 'Permit',
        validUntil: '2027-01-31',
        note: null,
        url: 'https://files.example.com/permit.pdf',
      },
      expect.any(String),
    );
    expect(list).toHaveBeenCalledTimes(2);
    expect(success).toHaveBeenCalledWith('Document added.');
  });

  it('voids a document with a reason and the version it was opened at', async () => {
    const voidDocument = stub(() => of(documentOf({ ...plan, status: 'Void' })));
    const { root, harness } = await open({ api: { voidDocument } });

    const card = root.querySelector<HTMLElement>('.group .row-card')!;
    card.querySelector<HTMLButtonElement>('app-menu button')!.click();
    await settle(harness.fixture);
    buttonNamed(card, 'Void document').click();
    await settle(harness.fixture);

    submit();
    await settle(harness.fixture);
    expect(voidDocument).not.toHaveBeenCalled();
    expect(document.body.textContent).toContain('Give a reason.');

    type(document.body, 'textarea[formcontrolname="reason"]', 'Uploaded to the wrong project');
    submit();
    await settle(harness.fixture);
    expect(voidDocument).toHaveBeenCalledWith('d-plan', 'Uploaded to the wrong project', 3);
  });
});
