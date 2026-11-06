import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Observable, of } from 'rxjs';
import type { Mock } from 'vitest';
import { SessionState } from '../../core/session/session-state';
import { settle, texts, type } from '../../testing/dom';
import { signInForTest } from '../../testing/session-fixture';
import { DocumentAddDialog } from './document-add-dialog';
import { DocumentsApi } from './documents-api';
import {
  MANAGER,
  PROJECT_ID,
  READER,
  UPLOADS_OK,
  documentItem,
  documentOf,
  permit,
} from './documents.fixtures';
import { DocumentListItem, DocumentUploads } from './documents.models';

type Call = (...args: unknown[]) => Observable<unknown>;
const stub = (implementation: Call): Mock<Call> => vi.fn<Call>(implementation);

async function open(
  options: {
    document?: DocumentListItem;
    uploads?: DocumentUploads;
    permissions?: readonly string[];
    api?: Record<string, Mock<Call>>;
  } = {},
) {
  const saved = vi.fn();
  TestBed.configureTestingModule({
    providers: [
      provideHttpClient(),
      provideHttpClientTesting(),
      { provide: DocumentsApi, useValue: { ...options.api } },
    ],
  });
  await signInForTest(TestBed.inject(SessionState), TestBed.inject(HttpTestingController), {
    roles: ['Custom'],
    permissions: options.permissions ?? MANAGER,
  });
  const fixture = TestBed.createComponent(DocumentAddDialog);
  fixture.componentRef.setInput('projectId', PROJECT_ID);
  fixture.componentRef.setInput('uploads', options.uploads ?? UPLOADS_OK);
  if (options.document) fixture.componentRef.setInput('document', options.document);
  fixture.componentInstance.saved.subscribe(saved);
  await settle(fixture);
  return { fixture, saved };
}

function chooseFile(file: File): void {
  const input = document.body.querySelector<HTMLInputElement>('input[type="file"]')!;
  Object.defineProperty(input, 'files', { value: [file], configurable: true });
  input.dispatchEvent(new Event('change'));
}

const submit = () => document.body.querySelector<HTMLButtonElement>('button[type="submit"]')!.click();
const pdf = (size: number, name = 'Ground floor plan.pdf') => new File([new Uint8Array(size)], name);

describe('DocumentAddDialog', () => {
  it('uploads a file, using its name as the first title, with the fields the server needs', async () => {
    const createFile = stub(() => of(documentOf(documentItem())));
    const { fixture, saved } = await open({ api: { createFile } });

    expect(document.body.querySelector('input[type="file"]')!.getAttribute('accept')).toBe(
      '.pdf,.jpg,.png,.docx',
    );
    const file = pdf(2048);
    chooseFile(file);
    await settle(fixture);
    expect(
      document.body.querySelector<HTMLInputElement>('input[formcontrolname="title"]')!.value,
    ).toBe('Ground floor plan');

    submit();
    await settle(fixture);

    expect(createFile).toHaveBeenCalledWith(
      PROJECT_ID,
      {
        title: 'Ground floor plan',
        description: null,
        category: 'Drawing',
        validUntil: null,
        note: null,
        file,
      },
      expect.any(String),
    );
    expect(saved).toHaveBeenCalled();
  });

  it('refuses a wrong type or a file over the limit before anything is sent', async () => {
    const createFile = stub(() => of(documentOf(documentItem())));
    const { fixture } = await open({
      api: { createFile },
      uploads: { ...UPLOADS_OK, maximumBytes: 1024 },
    });
    type(document.body, 'input[formcontrolname="title"]', 'Plan');

    chooseFile(pdf(10, 'page.html'));
    submit();
    await settle(fixture);
    expect(document.body.textContent).toContain("This type of file can't be added. Use PDF, JPG, PNG or DOCX.");

    chooseFile(pdf(2048));
    submit();
    await settle(fixture);
    expect(document.body.textContent).toContain('The file is larger than 1 KB.');
    expect(createFile).not.toHaveBeenCalled();
  });

  it('asks for a file when none was chosen', async () => {
    const { fixture } = await open();
    type(document.body, 'input[formcontrolname="title"]', 'Plan');
    submit();
    await settle(fixture);

    expect(document.body.textContent).toContain('Choose the file to upload.');
  });

  it('offers contracts and insurance only to people who can see costs', async () => {
    await open({ permissions: READER });

    expect(texts(document.body, 'select[formcontrolname="category"] option')).not.toContain('Contract');
  });

  it('adds a new version of a permit with its renewed date, sending the version it was opened at', async () => {
    const addVersion = stub(() => of(documentOf(permit())));
    const { fixture } = await open({ document: permit({ version: 4 }), api: { addVersion } });

    expect(document.body.querySelector('input[formcontrolname="title"]')).toBeNull();
    expect(document.body.textContent).toContain('Leave it empty to keep 15 Dec 2026.');
    const file = pdf(4096, 'renewed permit.pdf');
    chooseFile(file);
    type(document.body, 'input[formcontrolname="validUntil"]', '2027-12-15');
    type(document.body, 'textarea[formcontrolname="note"]', 'Renewed for a year');
    submit();
    await settle(fixture);

    expect(addVersion).toHaveBeenCalledWith(
      'd-permit',
      { file, url: null, note: 'Renewed for a year', validUntil: '2027-12-15' },
      4,
    );
  });

  it('adds a link version without asking for a date on an undated document', async () => {
    const addVersion = stub(() => of(documentOf(documentItem())));
    const { fixture } = await open({ document: documentItem(), api: { addVersion } });

    document.body.querySelector<HTMLInputElement>('input[value="link"]')!.click();
    await settle(fixture);
    expect(document.body.querySelector('input[formcontrolname="validUntil"]')).toBeNull();
    type(document.body, 'input[formcontrolname="url"]', 'http://files.example.com/plan');
    submit();
    await settle(fixture);
    expect(document.body.textContent).toContain('Enter a full link that starts with https://');

    type(document.body, 'input[formcontrolname="url"]', 'https://files.example.com/plan-rev2');
    submit();
    await settle(fixture);
    expect(addVersion).toHaveBeenCalledWith(
      'd-plan',
      { file: null, url: 'https://files.example.com/plan-rev2', note: null, validUntil: null },
      1,
    );
  });
});
