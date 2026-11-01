import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ChangeDetectionStrategy, Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { Observable, of, throwError } from 'rxjs';
import type { Mock } from 'vitest';
import { ApiError } from '../../../core/api/api-error';
import { ToastService } from '../../../core/notifications/toast-service';
import { SessionState } from '../../../core/session/session-state';
import { Viewport } from '../../../shared/ui/viewport/viewport';
import { buttonNamed, flushMicrotasks, settle, text, texts } from '../../../testing/dom';
import { VIEWER_PERMISSIONS, pageOf, signInForTest } from '../../../testing/session-fixture';
import { ProjectContext } from '../../projects/project-context';
import { Project } from '../../projects/projects.models';
import { InventoryApi } from '../inventory-api';
import {
  PROJECT_LOOKUP,
  RIVERSIDE_ID,
  excavator,
  hammers,
  projectStockOf,
  ruleViolation,
  warehouse,
} from '../inventory.fixtures';
import { ProjectStock } from '../inventory.models';
import { ProjectStockTab } from './project-stock-tab';

@Component({ template: '', changeDetection: ChangeDetectionStrategy.OnPush })
class Blank {}

type Call = (...args: unknown[]) => Observable<unknown>;
const stub = (implementation: Call): Mock<Call> => vi.fn<Call>(implementation);

const riverside: Project = {
  id: RIVERSIDE_ID,
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

const onSite: readonly ProjectStock[] = [
  projectStockOf(hammers(), 10),
  projectStockOf(excavator(), 1),
];

interface Options {
  readonly project?: Project;
  readonly rows?: readonly ProjectStock[];
  readonly total?: number;
  readonly permissions?: readonly string[];
  readonly tablet?: boolean;
  readonly url?: string;
  readonly api?: Record<string, Mock<Call>>;
}

async function open(options: Options = {}) {
  const projectStock = stub((_projectId, page) =>
    of(
      pageOf(
        options.rows ?? onSite,
        options.total ?? (options.rows ?? onSite).length,
        page as number,
      ),
    ),
  );
  const api: Record<string, Mock<Call>> = {
    projectStock,
    get: stub(() => of(hammers())),
    projectLookup: stub(() => of(PROJECT_LOOKUP)),
    ...options.api,
  };
  TestBed.configureTestingModule({
    providers: [
      provideRouter([
        { path: 'projects/:id/stock', component: ProjectStockTab },
        { path: 'stock', component: Blank },
      ]),
      provideHttpClient(),
      provideHttpClientTesting(),
      ProjectContext,
      { provide: InventoryApi, useValue: api },
      {
        provide: Viewport,
        useValue: { isTablet: signal(options.tablet ?? true), isDesktop: signal(true) },
      },
    ],
  });
  await signInForTest(
    TestBed.inject(SessionState),
    TestBed.inject(HttpTestingController),
    options.permissions ? { roles: ['Custom'], permissions: options.permissions } : {},
  );
  TestBed.inject(ProjectContext).replace(options.project ?? riverside);
  const harness = await RouterTestingHarness.create();
  await harness.navigateByUrl(options.url ?? `/projects/${RIVERSIDE_ID}/stock`, ProjectStockTab);
  await settle(harness.fixture);
  return {
    harness,
    root: harness.fixture.nativeElement as HTMLElement,
    api,
    projectStock,
    router: TestBed.inject(Router),
    toast: TestBed.inject(ToastService),
  };
}

const readOnly = { permissions: VIEWER_PERMISSIONS };
const archived = { ...riverside, status: 'Archived' as const };

describe('ProjectStockTab: what is on the project', () => {
  it('asks for the stock of this project, first page', async () => {
    const { projectStock } = await open();

    expect(projectStock).toHaveBeenCalledWith(RIVERSIDE_ID, 1, 25);
  });

  it('lists each item with its category and quantity with unit, linking to the item', async () => {
    const { root } = await open();

    const rows = root.querySelectorAll('tbody tr');
    expect(rows).toHaveLength(2);
    expect(text(rows[0]!, 'th a')).toBe('Claw hammer 16 oz');
    expect(rows[0]!.querySelector('th a')!.getAttribute('href')).toBe('/stock/item-hammer');
    expect(text(rows[0]!, 'th .cell-sub')).toBe('Stanley');
    expect(text(rows[0]!, 'app-status-chip')).toBe('Tool');
    expect(text(rows[0]!, 'td.num')).toBe('10 pc');
    expect(text(rows[1]!, 'th .cell-sub')).toBe('Komatsu · Serial PC200-8-A41127');
    expect(text(rows[1]!, 'app-status-chip')).toBe('Machinery');
    expect(text(rows[1]!, 'td.num')).toBe('1 unit');
  });

  it('shows cards on a phone, with the shortcut full width', async () => {
    const { root } = await open({ tablet: false });

    expect(root.querySelector('table')).toBeNull();
    const cards = root.querySelectorAll('.row-card');
    expect(cards).toHaveLength(2);
    expect(text(cards[0]!, '.row-card__quantity')).toBe('10 pc');
    expect(text(cards[0]!, '.row-card__action .btn')).toBe('Move to the warehouse');
  });

  it('pages on the server', async () => {
    const view = await open({ total: 60 });
    expect(text(view.root, '.pager__summary')).toBe('Showing 1 to 25 of 60 items');

    buttonNamed(view.root, 'Next').click();
    await settle(view.harness.fixture);

    expect(view.router.url).toBe(`/projects/${RIVERSIDE_ID}/stock?page=2`);
    expect(view.projectStock).toHaveBeenLastCalledWith(RIVERSIDE_ID, 2, 25);
  });

  it('says calmly when the list cannot load, and Try again asks again', async () => {
    let fail = true;
    const projectStock = stub(() =>
      fail
        ? throwError(
            () =>
              new ApiError({
                status: 500,
                title: 'Something went wrong',
                detail: "We couldn't finish that. Try again in a moment.",
                traceId: 'ref-5',
              }),
          )
        : of(pageOf(onSite)),
    );
    const { harness, root } = await open({ api: { projectStock } });
    expect(text(root, 'app-api-error-alert')).toContain('Reference: ref-5');

    fail = false;
    buttonNamed(root, 'Try again').click();
    await settle(harness.fixture);

    expect(root.querySelectorAll('tbody tr')).toHaveLength(2);
  });
});

describe('ProjectStockTab: who can move stock', () => {
  it('gives people who manage stock a "Move to the warehouse" shortcut on every row', async () => {
    const { root } = await open();

    expect(texts(root, 'tbody .btn')).toEqual(['Move to the warehouse', 'Move to the warehouse']);
    expect(root.querySelector('tbody .btn')!.getAttribute('aria-label')).toBe(
      'Move Claw hammer 16 oz to the warehouse',
    );
  });

  it('shows people who can only read stock the list without any shortcut', async () => {
    const { root } = await open({ ...readOnly });

    expect(root.querySelectorAll('tbody tr')).toHaveLength(2);
    expect(root.querySelector('tbody .btn')).toBeNull();
    expect(root.querySelector('thead th .visually-hidden')).toBeNull();
    expect(root.textContent).not.toContain('Move to the warehouse');
  });

  it('opens the shortcut with both places fixed and the whole quantity ready', async () => {
    const { harness, root, api } = await open();

    buttonNamed(root.querySelector('tbody tr')!, 'Move to the warehouse').click();
    await settle(harness.fixture);

    expect(api['get']).toHaveBeenCalledWith('item-hammer');
    const dialog = root.querySelector('dialog')!;
    expect(text(dialog, 'h2')).toBe('Move to the warehouse');
    expect(texts(dialog, '.def-list dd')).toEqual(['Riverside Residences (10 pc)', 'Warehouse']);
    expect(dialog.querySelector('select')).toBeNull();
    expect((dialog.querySelector('app-number-input input') as HTMLInputElement).value).toBe('10');
  });

  it('moves the stock back with the item version it just fetched, then refreshes the list and says so', async () => {
    const transfer = stub(() => of(hammers({ version: 4, balances: [warehouse(24)] })));
    const { harness, root, api, projectStock, toast } = await open({
      api: { transfer, get: stub(() => of(hammers({ version: 3 }))) },
    });

    buttonNamed(root.querySelector('tbody tr')!, 'Move to the warehouse').click();
    await settle(harness.fixture);
    root.querySelector<HTMLButtonElement>('dialog button[type="submit"]')!.click();
    await settle(harness.fixture);
    await flushMicrotasks(6);
    await settle(harness.fixture);

    expect(api['transfer']).toHaveBeenCalledWith(
      'item-hammer',
      expect.objectContaining({
        fromLocation: { kind: 'Project', projectId: RIVERSIDE_ID },
        toLocation: { kind: 'Warehouse' },
        quantity: 10,
        chargeToProject: false,
        version: 3,
      }),
    );
    expect(root.querySelector('dialog')).toBeNull();
    expect(projectStock).toHaveBeenCalledTimes(2);
    expect(toast.toasts().at(-1)?.message).toBe('Moved to the warehouse.');
  });

  it("shows the server's rule in the dialog and leaves the list alone", async () => {
    const message = 'Only 6 pc are there, but you asked for 10.';
    const transfer = stub(() => throwError(() => ruleViolation(message)));
    const { harness, root, projectStock } = await open({ api: { transfer } });

    buttonNamed(root.querySelector('tbody tr')!, 'Move to the warehouse').click();
    await settle(harness.fixture);
    root.querySelector<HTMLButtonElement>('dialog button[type="submit"]')!.click();
    await settle(harness.fixture);
    await flushMicrotasks(6);
    await settle(harness.fixture);

    expect(text(root, 'dialog app-api-error-alert')).toContain(message);
    expect(projectStock).toHaveBeenCalledTimes(1);
  });

  it('says so when the item cannot be fetched for the shortcut', async () => {
    const get = stub(() =>
      throwError(
        () =>
          new ApiError({
            status: 404,
            title: 'Not found',
            detail: "We couldn't find that item.",
            code: 'not_found',
          }),
      ),
    );
    const { harness, root } = await open({ api: { get } });

    buttonNamed(root.querySelector('tbody tr')!, 'Move to the warehouse').click();
    await settle(harness.fixture);

    expect(root.querySelector('dialog')).toBeNull();
    expect(text(root, 'app-api-error-alert')).toContain("We couldn't find that item.");
  });
});

describe('ProjectStockTab: an archived project', () => {
  it('says stock can come off an archived project but not go onto it, and keeps the shortcut', async () => {
    const { root } = await open({ project: archived });

    // The project page already says it is archived; this adds the one rule that matters for stock.
    expect(text(root, 'app-alert .alert__title')).toBe('Stock on an archived project');
    expect(text(root, 'app-alert')).toContain(
      'Nothing can be moved onto it until it is restored. Stock can still be moved off it to the warehouse.',
    );
    expect(texts(root, 'tbody .btn')).toEqual(['Move to the warehouse', 'Move to the warehouse']);
  });

  it('shows no banner on a project that is not archived', async () => {
    const { root } = await open();

    expect(root.querySelector('app-alert')).toBeNull();
  });

  it('still shows the list to someone who can only read, with the banner', async () => {
    const { root } = await open({ project: archived, ...readOnly });

    expect(text(root, 'app-alert .alert__title')).toBe('Stock on an archived project');
    expect(root.querySelectorAll('tbody tr')).toHaveLength(2);
    expect(root.querySelector('tbody .btn')).toBeNull();
  });
});

describe('ProjectStockTab: nothing on the project', () => {
  it('points a manager to where stock is moved from, with one button', async () => {
    const { root } = await open({ rows: [] });

    expect(text(root, 'app-empty-state')).toContain('Nothing is on this project yet');
    expect(text(root, 'app-empty-state')).toContain(
      'Move tools, equipment or machinery here from an item in stock.',
    );
    expect(
      buttonNamed(root.querySelector('app-empty-state')!, 'Go to stock').getAttribute('href'),
    ).toBe('/stock');
  });

  it('only says what will appear to a reader, with no button', async () => {
    const { root } = await open({ rows: [], ...readOnly });

    expect(text(root, 'app-empty-state')).toContain(
      'Stock appears here once it is moved to this project.',
    );
    expect(root.querySelector('app-empty-state a')).toBeNull();
  });

  it('does not promise stock will arrive on an archived project, and offers no button even to a manager', async () => {
    const { root } = await open({ rows: [], project: archived });

    expect(text(root, 'app-empty-state')).toContain('No stock is on this project');
    expect(text(root, 'app-empty-state')).toContain(
      'No tools, equipment or machinery are on this archived project.',
    );
    expect(root.querySelector('app-empty-state a')).toBeNull();
  });
});

describe('ProjectStockTab: items that are not tools', () => {
  it('moves a single piece back as one, with no quantity to type', async () => {
    const transfer = stub(() => of(excavator({ balances: [warehouse(1)], version: 6 })));
    const { harness, root, api } = await open({
      api: { transfer, get: stub(() => of(excavator())) },
    });

    buttonNamed(root.querySelectorAll('tbody tr')[1]!, 'Move to the warehouse').click();
    await settle(harness.fixture);
    expect(root.querySelector('dialog app-number-input')).toBeNull();
    root.querySelector<HTMLButtonElement>('dialog button[type="submit"]')!.click();
    await settle(harness.fixture);

    expect(api['transfer']).toHaveBeenCalledWith(
      'item-excavator',
      expect.objectContaining({ quantity: 1, version: 5 }),
    );
  });
});
