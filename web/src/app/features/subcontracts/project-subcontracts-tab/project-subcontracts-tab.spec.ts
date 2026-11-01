import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { Observable, of } from 'rxjs';
import { SessionState } from '../../../core/session/session-state';
import { Viewport } from '../../../shared/ui/viewport/viewport';
import { settle, text, texts } from '../../../testing/dom';
import { pageOf, signInForTest } from '../../../testing/session-fixture';
import { ProjectContext } from '../../projects/project-context';
import { Project } from '../../projects/projects.models';
import { SubcontractsApi } from '../subcontracts-api';
import { DRAFTER, READER, listItem } from '../subcontracts.fixtures';
import { SubcontractListItem } from '../subcontracts.models';
import { ProjectSubcontractsTab } from './project-subcontracts-tab';

type Call = (...args: unknown[]) => Observable<unknown>;

const project: Project = {
  id: 'p1',
  name: 'Riverside Residences',
  client: null,
  location: null,
  contractPriceMinor: null,
  startDate: '2026-03-01',
  targetEndDate: null,
  status: 'Active',
  archiveReason: null,
  archiveNote: null,
  archivedAtUtc: null,
  subProjects: [],
  version: 1,
};

async function open(items: readonly SubcontractListItem[], permissions: readonly string[], current: Project = project) {
  const list = vi.fn<Call>(() => of(pageOf(items)));
  TestBed.configureTestingModule({
    providers: [
      provideRouter([]),
      provideHttpClient(),
      provideHttpClientTesting(),
      ProjectContext,
      { provide: SubcontractsApi, useValue: { list } },
      { provide: Viewport, useValue: { isTablet: signal(false), isDesktop: signal(false) } },
    ],
  });
  await signInForTest(TestBed.inject(SessionState), TestBed.inject(HttpTestingController), { roles: ['Custom'], permissions });
  TestBed.inject(ProjectContext).replace(current);
  const fixture = TestBed.createComponent(ProjectSubcontractsTab);
  await settle(fixture);
  return { root: fixture.nativeElement as HTMLElement, list };
}

describe('ProjectSubcontractsTab', () => {
  it('lists this project’s subcontracts with what is certified and owed', async () => {
    const { root, list } = await open([listItem()], READER);

    expect(list).toHaveBeenCalledWith(expect.objectContaining({ projectId: 'p1', page: 1 }));
    expect(text(root, '.row-card__title')).toBe('SC-0001 Electrical works');
    expect(texts(root, '.row-card p')).toEqual([
      'Dela Cruz Electrical',
      'Certified ₱250,000.00 of ₱1,000,000.00 before VAT (25%)',
      'Owed ₱247,000.00',
    ]);
    expect(root.textContent).not.toContain('New subcontract');
  });

  it('offers a drafter one way to start, with the project chosen', async () => {
    const { root } = await open([], DRAFTER);

    expect(text(root, 'app-empty-state h2, app-empty-state h3')).toContain('No subcontracts on this project');
    const start = root.querySelector<HTMLAnchorElement>('app-empty-state a')!;
    expect(start.textContent?.trim()).toBe('New subcontract');
    expect(start.getAttribute('href')).toBe('/subcontracts/new?projectId=p1');
  });

  it('adds nothing on an archived project', async () => {
    const { root } = await open([listItem()], DRAFTER, { ...project, status: 'Archived' });

    expect(root.textContent).toContain('This project is archived');
    expect(root.textContent).not.toContain('New subcontract');
  });
});
