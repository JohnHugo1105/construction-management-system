import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { Observable, of } from 'rxjs';
import type { Mock } from 'vitest';
import { Permission } from '../../../core/session/permissions';
import { SessionState } from '../../../core/session/session-state';
import { buttonNamed, settle, text, texts } from '../../../testing/dom';
import { pageOf, signInForTest } from '../../../testing/session-fixture';
import { CostingApi } from '../../costing/costing-api';
import { PROJECT_ID, projectFixture } from '../../costing/testing/costing-fixtures';
import { ProjectContext } from '../../projects/project-context';
import { ProjectsApi } from '../../projects/projects-api';
import { Project } from '../../projects/projects.models';
import { ChangeOrdersApi } from '../change-orders-api';
import {
  baselineFixture,
  changeOrderFixture,
  listItemFixture,
  openBaselineFixture,
} from '../change-orders.fixtures';
import { BudgetBaseline, ChangeOrder, ChangeOrderListItem } from '../change-orders.models';
import { ProjectChangesTab } from './project-changes-tab';

type Call = (...args: unknown[]) => Observable<unknown>;
const stub = (implementation: Call): Mock<Call> => vi.fn<Call>(implementation);

const READ = [Permission.projectsRead, Permission.costsRead, Permission.changeOrdersRead];
const MANAGE = [...READ, Permission.changeOrdersManage];
const APPROVE = [...READ, Permission.changeOrdersApprove];

interface Options {
  readonly url?: string;
  readonly permissions?: readonly string[];
  readonly baseline?: BudgetBaseline;
  readonly orders?: readonly ChangeOrderListItem[];
  readonly order?: ChangeOrder;
  readonly project?: Project;
}

async function open(options: Options = {}) {
  const api = {
    baseline: stub(() => of(options.baseline ?? baselineFixture())),
    list: stub(() => of(pageOf(options.orders ?? [listItemFixture()]))),
    get: stub(() => of(options.order ?? changeOrderFixture())),
    lock: stub(() => of(baselineFixture({ approvedCount: 0, pendingCount: 0 }))),
    submit: stub(() => of(changeOrderFixture({ status: 'Submitted' }))),
    approve: stub(() =>
      of(
        changeOrderFixture({
          status: 'Approved',
          decidedBy: 'Ana',
          decidedAtUtc: '2026-10-03T03:00:00Z',
        }),
      ),
    ),
  };
  TestBed.configureTestingModule({
    providers: [
      provideRouter([{ path: 'projects/:id/changes', component: ProjectChangesTab }]),
      provideHttpClient(),
      provideHttpClientTesting(),
      ProjectContext,
      { provide: ProjectsApi, useValue: { get: () => of(options.project ?? projectFixture()) } },
      { provide: CostingApi, useValue: {} },
      { provide: ChangeOrdersApi, useValue: api },
    ],
  });
  await signInForTest(TestBed.inject(SessionState), TestBed.inject(HttpTestingController), {
    roles: ['Custom'],
    permissions: options.permissions ?? [...MANAGE, Permission.changeOrdersApprove],
  });
  TestBed.inject(ProjectContext).replace(
    options.project ?? projectFixture({ budgetBaselineLocked: true }),
  );
  const harness = await RouterTestingHarness.create();
  await harness.navigateByUrl(options.url ?? `/projects/${PROJECT_ID}/changes`, ProjectChangesTab);
  await settle(harness.fixture);
  return { harness, root: harness.fixture.nativeElement as HTMLElement, api };
}

describe('ProjectChangesTab', () => {
  it('shows the baseline as original, changes and revised from the server, with what is waiting', async () => {
    const { root } = await open();

    const rows = Array.from(root.querySelectorAll('.baseline-table tbody tr')).map((row) =>
      texts(row, 'th, td').join(' '),
    );
    expect(rows).toEqual([
      'Budget ₱7,120.00 +₱999.99 ₱8,119.99',
      'Contract price ₱500,000.00 +₱1,500.00 ₱501,500.00',
    ]);
    expect(root.textContent).toContain(
      '1 change order waiting for approval: Budget -₱560.00 if approved.',
    );
    expect(root.textContent).toContain('by Paolo Project-Manager');
    // Unlocking is offered only while nothing is approved.
    expect(texts(root, 'button')).not.toContain('Unlock the budget');
  });

  it('lists the change orders with their number, state and effect, and leads with New change order for people who draft', async () => {
    const { root } = await open({
      permissions: MANAGE,
      orders: [
        listItemFixture({
          id: 'co-2',
          number: null,
          title: 'Extra slab',
          status: 'Draft',
          budgetEffectGrossMinor: 0,
          contractPriceDeltaMinor: 0,
        }),
        listItemFixture(),
      ],
    });

    expect(texts(root, '.order-row .row-card__title')).toEqual([
      'Draft · Extra slab',
      'CO-001 · Basement waterproofing',
    ]);
    expect(texts(root, '.order-row app-status-chip')).toEqual(['Draft', 'Approved']);
    expect(texts(root, '.order-row__effect')).toEqual([
      'No change to the budget or the price',
      'Budget +₱999.99 · Price +₱1,500.00',
    ]);
    expect(texts(root, 'button').filter((label) => label === 'New change order')).toHaveLength(1);
  });

  it('explains locking before the budget is locked and offers it to approvers only', async () => {
    const approver = await open({
      permissions: APPROVE,
      baseline: openBaselineFixture(),
      orders: [],
      project: projectFixture({ budgetBaselineLocked: false }),
    });
    expect(text(approver.root, '#baseline-heading')).toBe('The budget is not locked yet');
    buttonNamed(approver.root, 'Lock the budget').click();
    await settle(approver.harness.fixture);
    expect(document.body.textContent).toContain(
      'The budget of ₱7,120.00 with VAT (3 lines) and the contract price of ₱500,000.00 become the original.',
    );
    // Nothing to list, and no "New change order" before the budget is locked.
    expect(approver.root.textContent).not.toContain('New change order');
    TestBed.resetTestingModule();

    const reader = await open({
      permissions: READ,
      baseline: openBaselineFixture(),
      orders: [],
      project: projectFixture({ budgetBaselineLocked: false }),
    });
    expect(texts(reader.root, 'button')).not.toContain('Lock the budget');
    expect(reader.root.textContent).toContain('A project manager can lock it.');
  });

  it('opens a change order on the same tab and offers the approver Approve and Reject', async () => {
    const { root, api } = await open({
      url: `/projects/${PROJECT_ID}/changes?order=co-1`,
      permissions: APPROVE,
    });

    expect(api.get).toHaveBeenCalledWith('co-1');
    expect(text(root, '#co-heading')).toBe('CO-001: Basement waterproofing');
    expect(text(root, 'app-change-order-page app-status-chip')).toBe('Waiting for approval');
    expect(texts(root, '.co-effect dd.money')).toEqual(['+₱1,000.00', '+₱1,500.00']);
    expect(text(root, '.row-card')).toContain('From 10 m³ × ₱500.00 to 12 m³ × ₱500.00');
    expect(texts(root, '.co-actions button')).toEqual(['Approve change order', 'Reject']);
  });

  it('offers a draft to its author to submit, edit and discard, and approves only through the confirmation', async () => {
    const draft = changeOrderFixture({
      status: 'Draft',
      number: null,
      submittedAtUtc: null,
      submittedBy: null,
    });
    const { root, api, harness } = await open({
      url: `/projects/${PROJECT_ID}/changes?order=co-1`,
      permissions: MANAGE,
      order: draft,
    });

    expect(texts(root, '.co-actions button')).toEqual([
      'Submit for approval',
      'Edit details',
      'Discard',
    ]);
    expect(text(root, '#co-heading')).toBe('Draft change order: Basement waterproofing');
    buttonNamed(root, 'Submit for approval').click();
    await settle(harness.fixture);
    expect(api.submit).toHaveBeenCalledWith('co-1', 2);
    expect(api.approve).not.toHaveBeenCalled();
  });

  it('names the order to void first instead of offering Void when a later one stands', async () => {
    const approved = changeOrderFixture({
      status: 'Approved',
      voidBlockedBy: 'CO-002',
      decidedAtUtc: '2026-10-03T03:00:00Z',
      decidedBy: 'Paolo',
    });
    const { root } = await open({
      url: `/projects/${PROJECT_ID}/changes?order=co-1`,
      permissions: APPROVE,
      order: approved,
    });

    expect(texts(root, 'button')).not.toContain('Void change order');
    expect(root.textContent).toContain('To void this one, void CO-002 first');
  });

  it('keeps an archived project read-only', async () => {
    const { root } = await open({
      url: `/projects/${PROJECT_ID}/changes?order=co-1`,
      project: projectFixture({ status: 'Archived', budgetBaselineLocked: true }),
    });

    expect(root.querySelector('.co-actions')).toBeNull();
    expect(root.textContent).toContain(
      "This project is archived, so its change orders can't change.",
    );
  });
});
