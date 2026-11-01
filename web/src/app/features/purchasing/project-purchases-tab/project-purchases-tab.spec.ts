import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { Permission } from '../../../core/session/permissions';
import { SessionState } from '../../../core/session/session-state';
import { buttonNamed, settle, text, texts } from '../../../testing/dom';
import { pageOf, signInForTest } from '../../../testing/session-fixture';
import { ProjectContext } from '../../projects/project-context';
import { ProjectsApi } from '../../projects/projects-api';
import { projectFixture } from '../../costing/testing/costing-fixtures';
import { PurchasingApi } from '../purchasing-api';
import { listItem } from '../purchasing.fixtures';
import { PurchaseOrderListParams } from '../purchasing.models';
import { ProjectPurchasesTab } from './project-purchases-tab';

async function open(options: { items?: ReturnType<typeof listItem>[]; permissions?: readonly string[]; archived?: boolean } = {}) {
  const listOrders = vi.fn((params: PurchaseOrderListParams) => {
    void params;
    return of(pageOf(options.items ?? [listItem(), listItem({ id: 'order-2', number: 'PO-0002', status: 'Draft', supplierName: 'Tilemart Cebu' })]));
  });
  TestBed.configureTestingModule({
    providers: [
      provideRouter([]),
      provideHttpClient(),
      provideHttpClientTesting(),
      ProjectContext,
      { provide: ProjectsApi, useValue: {} },
      { provide: PurchasingApi, useValue: { listOrders } },
    ],
  });
  await signInForTest(TestBed.inject(SessionState), TestBed.inject(HttpTestingController), options.permissions ? { permissions: options.permissions } : {});
  const project = projectFixture();
  TestBed.inject(ProjectContext).replace(options.archived ? { ...project, status: 'Archived' } : project);
  const fixture = TestBed.createComponent(ProjectPurchasesTab);
  await settle(fixture);
  return { fixture, root: fixture.nativeElement as HTMLElement, listOrders, projectId: project.id };
}

describe('ProjectPurchasesTab', () => {
  it("lists this project's orders only, each linking to the order", async () => {
    const { root, listOrders, projectId } = await open();

    expect(listOrders.mock.calls[0]![0]).toMatchObject({ projectId, page: 1 });
    expect(root.querySelectorAll('.row-card')).toHaveLength(2);
    expect(text(root.querySelector('.row-card')!, '.row-card__title')).toBe('PO-0001 · Visayas Steel Trading');
    expect(root.querySelector('.row-card a')!.getAttribute('href')).toBe('/purchasing/orders/order-1');
    expect(texts(root, 'app-status-chip')).toEqual(['Approved', 'Draft']);
  });

  it('offers "New purchase order" for this project to people who draft orders', async () => {
    const { root, projectId } = await open();

    expect(buttonNamed(root, 'New purchase order').getAttribute('href')).toBe(`/purchasing/orders/new?project=${projectId}`);
  });

  it('offers nothing to draft to a reader, and nothing on an archived project, saying why', async () => {
    const reader = await open({ permissions: [Permission.purchasingRead, Permission.projectsRead] });
    expect(reader.root.textContent).not.toContain('New purchase order');
    TestBed.resetTestingModule();

    const archived = await open({ items: [], archived: true });
    expect(archived.root.textContent).not.toContain('New purchase order');
    expect(archived.root.textContent).toContain('This project is archived, so no new orders can be raised for it.');
  });

  it('invites the first order when there is none', async () => {
    const { root } = await open({ items: [] });

    expect(root.textContent).toContain('No purchase orders for this project');
    expect(root.textContent).toContain('Raise one to a supplier.');
  });
});
