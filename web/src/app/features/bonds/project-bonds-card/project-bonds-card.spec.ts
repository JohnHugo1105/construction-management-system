import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { Observable, of, throwError } from 'rxjs';
import { SessionState } from '../../../core/session/session-state';
import { Viewport } from '../../../shared/ui/viewport/viewport';
import { buttonNamed, settle, text, texts, type } from '../../../testing/dom';
import { signInForTest } from '../../../testing/session-fixture';
import { BondsApi } from '../bonds-api';
import { MANAGER, READER, bond, listItem } from '../bonds.fixtures';
import { BondListItem } from '../bonds.models';
import { ProjectBondsCard } from './project-bonds-card';

type Call = (...args: unknown[]) => Observable<unknown>;

async function open(
  items: readonly BondListItem[],
  permissions: readonly string[],
  archived = false,
  extra: Record<string, Call> = {},
) {
  const forProject = vi.fn((projectId: string) => {
    void projectId;
    return of(items);
  });
  TestBed.configureTestingModule({
    providers: [
      provideRouter([]),
      provideHttpClient(),
      provideHttpClientTesting(),
      { provide: BondsApi, useValue: { forProject, projects: () => of([]), ...extra } },
      { provide: Viewport, useValue: { isTablet: signal(false), isDesktop: signal(false) } },
    ],
  });
  await signInForTest(TestBed.inject(SessionState), TestBed.inject(HttpTestingController), {
    roles: ['Custom'],
    permissions,
  });
  const fixture = TestBed.createComponent(ProjectBondsCard);
  fixture.componentRef.setInput('projectId', 'p1');
  fixture.componentRef.setInput('projectArchived', archived);
  await settle(fixture);
  return { fixture, root: fixture.nativeElement as HTMLElement, forProject };
}

const released = listItem({ id: 'b3', status: 'Released', expiry: null, daysLeft: null });

describe('ProjectBondsCard', () => {
  it('lists the project’s active bonds with their dates', async () => {
    const { root, forProject } = await open(
      [
        listItem(),
        listItem({
          id: 'b2',
          kind: 'Warranty',
          bondNumber: 'WB-9',
          expiry: 'ExpiringSoon',
          daysLeft: 8,
        }),
      ],
      READER,
    );

    expect(forProject).toHaveBeenCalledWith('p1');
    expect(text(root, 'h2')).toBe('Bonds');
    expect(texts(root, '.row-card__title')).toEqual([
      'Performance bond PB-2026-014 · Metro Surety',
      'Warranty bond WB-9 · Metro Surety',
    ]);
    expect(texts(root, '.row-card app-status-chip')).toEqual(['Valid', 'Expires in 8 days']);
    // The project is named by the page around the card, not by each row.
    expect(texts(root, '.row-card p.text-muted')).toEqual([
      'Posted to the client',
      'Posted to the client',
    ]);
    expect(root.textContent).not.toContain('Add bond');
  });

  it('shows nothing at all, not even a heading, when there are no bonds and the person cannot add one', async () => {
    const { root } = await open([], READER);

    expect(root.textContent?.trim()).toBe('');
    expect(root.querySelector('h2')).toBeNull();
  });

  it('offers a manager the one way to add the first bond', async () => {
    const { root } = await open([], MANAGER);

    expect(text(root, 'h2')).toBe('Bonds');
    expect(text(root, '.text-muted')).toBe('No bond is active on this project.');
    expect(texts(root, 'button')).toEqual(['Add bond']);
  });

  it('shows no heading and no add button for an archived project with no bonds', async () => {
    const { root } = await open([], MANAGER, true);

    expect(root.querySelector('h2')).toBeNull();
  });

  it('says an archived project cannot take changes and offers no add button', async () => {
    const { root } = await open([listItem()], MANAGER, true);

    expect(root.querySelectorAll('.row-card')).toHaveLength(1);
    expect(root.textContent).toContain(
      "This project is archived, so bonds can't be added or changed.",
    );
    expect(root.textContent).not.toContain('Add bond');
  });

  it('points to the register when the project also has closed bonds', async () => {
    const { root } = await open([listItem(), released], READER);

    expect(root.querySelectorAll('.row-card')).toHaveLength(1);
    const link = root.querySelector<HTMLAnchorElement>('a[href*="state=All"]')!;
    expect(link.textContent?.trim()).toBe('See every bond on this project');
    expect(link.getAttribute('href')).toBe('/bonds?project=p1&state=All');
  });

  it('shows no empty card and a note to a manager when only closed bonds exist', async () => {
    const { root } = await open([released], MANAGER);

    expect(text(root, '.text-muted')).toBe('No bond is active on this project.');
    expect(root.querySelectorAll('.row-card')).toHaveLength(0);
  });

  it('stays out of the way when the bonds cannot be read', async () => {
    const forProject = () => throwError(() => new Error('down'));
    const { root } = await open([], MANAGER, false, { forProject });

    expect(root.textContent?.trim()).toBe('');
  });

  it('adds a bond to this project without asking which project and refreshes the list', async () => {
    const create = vi.fn(() => of(bond()));
    const { fixture, root, forProject } = await open([], MANAGER, false, { create });

    buttonNamed(root, 'Add bond').click();
    await settle(fixture);
    expect(document.body.textContent).not.toContain('Choose a project');
    type(document.body, 'input[formcontrolname="issuerName"]', 'Metro Surety');
    type(document.body, 'input[formcontrolname="bondNumber"]', 'PB-1');
    type(document.body, 'app-money-input input', '5,000,000');
    type(document.body, 'input[formcontrolname="issuedOn"]', '2026-06-01');
    type(document.body, 'input[formcontrolname="validUntil"]', '2027-06-01');
    document.body.querySelector<HTMLButtonElement>('button[type="submit"]')!.click();
    await settle(fixture);

    expect(create).toHaveBeenCalledWith(
      'p1',
      {
        kind: 'Performance',
        issuerName: 'Metro Surety',
        bondNumber: 'PB-1',
        amountMinor: 500_000_000,
        issuedOn: '2026-06-01',
        validUntil: '2027-06-01',
        otherPartyName: null,
        note: null,
        direction: 'Posted',
      },
      expect.any(String),
    );
    expect(forProject).toHaveBeenCalledTimes(2);
  });
});
