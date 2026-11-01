import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { SessionState } from '../../core/session/session-state';
import { settle, texts } from '../../testing/dom';
import { signInForTest } from '../../testing/session-fixture';
import { ProjectContext } from './project-context';
import { ProjectOverviewTab } from './project-overview-tab';
import { Project } from './projects.models';

const project: Project = {
  id: 'p1',
  name: 'Harbour Tower',
  client: 'Acme Realty',
  location: 'Cebu',
  contractPriceMinor: 125000000,
  startDate: '2026-12-03',
  targetEndDate: null,
  status: 'Active',
  archiveReason: null,
  archiveNote: null,
  archivedAtUtc: null,
  subProjects: [],
  version: 1,
};

async function open(signInOptions: Parameters<typeof signInForTest>[2]) {
  TestBed.configureTestingModule({
    providers: [
      provideRouter([]),
      provideHttpClient(),
      provideHttpClientTesting(),
      {
        provide: ProjectContext,
        useValue: {
          project: signal(project),
          subProjects: signal([]),
          canManage: signal(false),
          isArchived: signal(false),
          canEdit: signal(false),
        },
      },
    ],
  });
  await signInForTest(
    TestBed.inject(SessionState),
    TestBed.inject(HttpTestingController),
    signInOptions,
  );
  const fixture = TestBed.createComponent(ProjectOverviewTab);
  await settle(fixture);
  return fixture.nativeElement as HTMLElement;
}

describe('ProjectOverviewTab', () => {
  it('shows the contract price to someone who can see costs', async () => {
    const root = await open({});

    expect(texts(root, '.def-list dt')).toContain('Contract price');
    expect(root.textContent).toContain('₱1,250,000.00');
  });

  it('leaves the contract price row out, rather than saying "Not set", for someone who cannot see costs', async () => {
    const root = await open({
      roles: ['WarehouseKeeper'],
      permissions: ['projects.read', 'inventory.read'],
    });

    expect(texts(root, '.def-list dt')).toEqual([
      'Client',
      'Location',
      'Start date',
      'Target end date',
    ]);
    expect(root.textContent).not.toContain('1,250,000');
  });
});
