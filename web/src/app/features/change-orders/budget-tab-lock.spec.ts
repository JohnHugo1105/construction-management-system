import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { of } from 'rxjs';
import { Permission } from '../../core/session/permissions';
import { SessionState } from '../../core/session/session-state';
import { Viewport } from '../../shared/ui/viewport/viewport';
import { settle, texts } from '../../testing/dom';
import { signInForTest } from '../../testing/session-fixture';
import { CostingApi } from '../costing/costing-api';
import { ProjectBudgetTab } from '../costing/project-budget-tab/project-budget-tab';
import {
  PROJECT_ID,
  budgetLineFixture,
  pageWithTotals,
  projectFixture,
  referenceFixture,
  summaryFixture,
} from '../costing/testing/costing-fixtures';
import { ProjectContext } from '../projects/project-context';
import { ProjectsApi } from '../projects/projects-api';

/** The Budget tab's part of the baseline (slice C): the change order chip on lines, and the one line that replaces the controls when locked. */
async function open(locked: boolean, tablet: boolean) {
  TestBed.configureTestingModule({
    providers: [
      provideRouter([{ path: 'projects/:id/budget', component: ProjectBudgetTab }]),
      provideHttpClient(),
      provideHttpClientTesting(),
      ProjectContext,
      { provide: ProjectsApi, useValue: {} },
      {
        provide: CostingApi,
        useValue: {
          reference: () => of(referenceFixture()),
          costSummary: () => of(summaryFixture()),
          listBudgetLines: () =>
            of(
              pageWithTotals([
                budgetLineFixture(),
                budgetLineFixture({
                  id: 'bl-9',
                  description: 'Waterproofing membrane',
                  changeOrderId: 'co-1',
                  changeOrderNumber: 'CO-001',
                }),
              ]),
            ),
        },
      },
      { provide: Viewport, useValue: { isTablet: signal(tablet), isDesktop: signal(tablet) } },
    ],
  });
  await signInForTest(TestBed.inject(SessionState), TestBed.inject(HttpTestingController), {
    roles: ['Custom'],
    permissions: [
      Permission.projectsRead,
      Permission.costsRead,
      Permission.costsWrite,
      Permission.changeOrdersRead,
    ],
  });
  TestBed.inject(ProjectContext).replace(projectFixture({ budgetBaselineLocked: locked }));
  const harness = await RouterTestingHarness.create();
  await harness.navigateByUrl(`/projects/${PROJECT_ID}/budget`, ProjectBudgetTab);
  await settle(harness.fixture);
  return harness.fixture.nativeElement as HTMLElement;
}

describe('Budget tab with a budget baseline', () => {
  it('marks the line a change order added, on the table and on the cards', async () => {
    const wide = await open(false, true);
    expect(texts(wide, '.line__change-order')).toEqual(['Change order CO-001']);
    TestBed.resetTestingModule();

    const phone = await open(false, false);
    expect(texts(phone, '.line__change-order')).toEqual(['Change order CO-001']);
  });

  it('replaces add, edit and remove with one line pointing to Changes while the budget is locked', async () => {
    const root = await open(true, true);

    expect(texts(root, 'button')).not.toContain('Add budget line');
    expect(texts(root, 'button').filter((label) => label === 'Edit' || label === 'Remove')).toEqual(
      [],
    );
    expect(texts(root, '.budget-locked')).toEqual([
      'The budget is locked. Record a change order to change it. Go to Changes',
    ]);
    expect(root.querySelector<HTMLAnchorElement>('.budget-locked a')!.getAttribute('href')).toBe(
      `/projects/${PROJECT_ID}/changes`,
    );
  });

  it('keeps the controls while the budget is open', async () => {
    const root = await open(false, true);

    expect(texts(root, 'button')).toContain('Add budget line');
    expect(root.querySelector('.budget-locked')).toBeNull();
  });
});
