import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { Observable, of } from 'rxjs';
import type { Mock } from 'vitest';
import { Permission } from '../../../core/session/permissions';
import { SessionState } from '../../../core/session/session-state';
import { Viewport } from '../../../shared/ui/viewport/viewport';
import { settle, text, texts } from '../../../testing/dom';
import { signInForTest } from '../../../testing/session-fixture';
import { PROJECT_ID, projectFixture } from '../../costing/testing/costing-fixtures';
import { ProjectContext } from '../../projects/project-context';
import { ProjectsApi } from '../../projects/projects-api';
import { Project } from '../../projects/projects.models';
import { BillingApi } from '../billing-api';
import { listBill, projectBilling } from '../billing.fixtures';
import { ProjectBilling } from '../billing.models';
import { ProjectBillingTab } from './project-billing-tab';

type Call = (...args: unknown[]) => Observable<unknown>;
const stub = (implementation: Call): Mock<Call> => vi.fn<Call>(implementation);

const READER = [Permission.projectsRead, Permission.billingRead];
const MANAGER = [...READER, Permission.billingManage];

async function open(
  billing: ProjectBilling,
  permissions: readonly string[],
  project: Partial<Project> = {},
) {
  const api = { projectBilling: stub(() => of(billing)) };
  TestBed.configureTestingModule({
    providers: [
      provideRouter([{ path: 'projects/:id/billing', component: ProjectBillingTab }]),
      provideHttpClient(),
      provideHttpClientTesting(),
      ProjectContext,
      { provide: ProjectsApi, useValue: {} },
      { provide: BillingApi, useValue: api },
      { provide: Viewport, useValue: { isTablet: signal(false), isDesktop: signal(false) } },
    ],
  });
  await signInForTest(TestBed.inject(SessionState), TestBed.inject(HttpTestingController), {
    roles: ['Custom'],
    permissions,
  });
  TestBed.inject(ProjectContext).replace(projectFixture(project));
  const harness = await RouterTestingHarness.create();
  await harness.navigateByUrl(`/projects/${PROJECT_ID}/billing`, ProjectBillingTab);
  await settle(harness.fixture);
  return { root: harness.fixture.nativeElement as HTMLElement, api, fixture: harness.fixture };
}

describe('ProjectBillingTab', () => {
  it('leads with what is still to collect and shows the server’s money line and bills', async () => {
    const { root, api } = await open(projectBilling(), MANAGER);

    expect(api.projectBilling).toHaveBeenCalledWith(PROJECT_ID, 1, 25);
    expect(texts(root, '.money-line__item')).toEqual([
      'Still to collect₱976,000.00₱976,000.00 overdue on 1 bill',
      'Billed to date₱2,240,000.0020% of the contract',
      'Collected₱1,000,000.00',
      'Contract price₱11,200,000.00',
      'Retention held₱224,000.00',
    ]);
    expect(text(root, '.terms__summary')).toBe(
      'Billing terms: 10% retention, 2% withholding, payment due in 30 days',
    );
    expect(texts(root, '.row-card .row-card__title')).toEqual(['BILL-0001']);
    expect(text(root, '.row-card app-status-chip')).toBe('Overdue');
    expect(texts(root, '.toolbar .btn')).toEqual(['New bill']);
    expect(texts(root, '.money-line__item dt')).not.toContain('Down payment still to recover');
  });

  it('offers nothing to a person who may only read, and nothing on an archived project', async () => {
    const reader = await open(projectBilling(), READER);
    expect(reader.root.querySelector('.toolbar')).toBeNull();
    expect(texts(reader.root, 'button')).not.toContain('Change billing terms');
    TestBed.resetTestingModule();

    const archived = await open(projectBilling({ projectStatus: 'Archived' }), MANAGER, {
      status: 'Archived',
    });
    expect(archived.root.querySelector('.toolbar')).toBeNull();
    expect(text(archived.root, 'p.text-muted')).toContain('This project is archived');
  });

  it('asks for the contract price before anything can be billed', async () => {
    const { root } = await open(
      projectBilling({
        contractPriceMinor: null,
        percentBilledBps: null,
        remainingToBillMinor: null,
        billedToDateMinor: 0,
        bills: { items: [], total: 0, page: 1, pageSize: 25 },
      }),
      MANAGER,
    );

    expect(text(root, 'app-empty-state h2')).toBe('Add the contract price to bill the client');
    expect(root.querySelector('.money-line')).toBeNull();
  });

  it('puts "New bill" in the empty list, and offers releasing retention once the project is completed', async () => {
    const empty = await open(
      projectBilling({
        billedToDateMinor: 0,
        collectedMinor: 0,
        outstandingMinor: 0,
        overdueMinor: 0,
        overdueCount: 0,
        retentionWithheldMinor: 0,
        retentionHeldMinor: 0,
        bills: { items: [], total: 0, page: 1, pageSize: 25 },
      }),
      MANAGER,
    );
    expect(text(empty.root, 'app-empty-state h2')).toBe('No bills yet');
    expect(texts(empty.root, 'app-empty-state button')).toEqual(['New bill']);
    expect(empty.root.querySelector('.toolbar')).toBeNull();
    TestBed.resetTestingModule();

    const done = await open(
      projectBilling({
        projectStatus: 'Completed',
        canReleaseRetention: true,
        remainingToBillMinor: 0,
        bills: {
          items: [listBill({ outstandingMinor: 0, isOverdue: false })],
          total: 1,
          page: 1,
          pageSize: 25,
        },
      }),
      MANAGER,
      { status: 'Completed' },
    );
    expect(texts(done.root, '.toolbar .btn')).toEqual(['Release retention']);
  });

  const NOTHING_BILLED: Partial<ProjectBilling> = {
    billedToDateMinor: 0,
    percentBilledBps: 0,
    collectedMinor: 0,
    outstandingMinor: 0,
    overdueMinor: 0,
    overdueCount: 0,
    retentionWithheldMinor: 0,
    retentionHeldMinor: 0,
    advanceToBillMinor: 168_000_000,
    terms: { ...projectBilling().terms, advanceBps: 1_500 },
    bills: { items: [], total: 0, page: 1, pageSize: 25 },
  };

  it('leads with billing the down payment while the terms take one and none exists', async () => {
    const { root } = await open(projectBilling(NOTHING_BILLED), MANAGER);

    expect(text(root, 'app-empty-state h2')).toBe('Start with the down payment');
    expect(text(root, 'app-empty-state p')).toContain(
      'down payment of ₱1,680,000.00, 15% of the contract price',
    );
    expect(texts(root, 'app-empty-state button')).toEqual([
      'Bill the down payment',
      'New progress bill',
    ]);
    expect(root.querySelector('app-empty-state .btn--primary')?.textContent?.trim()).toBe(
      'Bill the down payment',
    );
    expect(text(root, '.terms__summary')).toBe(
      'Billing terms: 10% retention, 2% withholding, 15% down payment, payment due in 30 days',
    );
    TestBed.resetTestingModule();

    // A draft progress bill exists: the down payment still leads, "New bill" steps back.
    const withDraft = await open(
      projectBilling({
        ...NOTHING_BILLED,
        bills: {
          items: [listBill({ id: 'b9', number: null, status: 'Draft', outstandingMinor: 0 })],
          total: 1,
          page: 1,
          pageSize: 25,
        },
      }),
      MANAGER,
    );
    expect(texts(withDraft.root, '.toolbar .btn')).toEqual(['New bill', 'Bill the down payment']);
    expect(withDraft.root.querySelector('.toolbar .btn--primary')?.textContent?.trim()).toBe(
      'Bill the down payment',
    );
    TestBed.resetTestingModule();

    // A reader is told nothing they can't act on.
    const reader = await open(projectBilling(NOTHING_BILLED), READER);
    expect(text(reader.root, 'app-empty-state h2')).toBe('No bills yet');
    expect(reader.root.querySelector('app-empty-state button')).toBeNull();
  });

  it('shows the down payment still to recover only once one is billed', async () => {
    const { root } = await open(
      projectBilling({
        advanceBilledMinor: 168_000_000,
        advanceRecoveredMinor: 33_600_000,
        advanceToRecoverMinor: 134_400_000,
        advanceBillId: 'b0',
      }),
      MANAGER,
    );

    expect(texts(root, '.money-line__item')).toContain(
      'Down payment still to recover₱1,344,000.00of the ₱1,680,000.00 down payment',
    );
    expect(texts(root, '.toolbar .btn')).toEqual(['New bill']);
  });
});
