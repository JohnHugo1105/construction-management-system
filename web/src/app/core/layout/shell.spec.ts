import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ChangeDetectionStrategy, Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Title } from '@angular/platform-browser';
import { Router, provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { PageHeader } from '../../shared/ui/page-header/page-header';
import { Viewport } from '../../shared/ui/viewport/viewport';
import { buttonNamed, flushMicrotasks, settle, text, texts } from '../../testing/dom';
import { signInForTest } from '../../testing/session-fixture';
import { Permission } from '../session/permissions';
import { SessionState } from '../session/session-state';
import { Shell } from './shell';

@Component({
  imports: [PageHeader],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: '<app-page-header title="Projects" /><p>Project list</p>',
})
class ProjectsPage {}

@Component({
  imports: [PageHeader],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: '<app-page-header title="Users" />',
})
class UsersPage {}

@Component({ template: '', changeDetection: ChangeDetectionStrategy.OnPush })
class Blank {}

interface Options {
  readonly desktop?: boolean;
  readonly permissions?: readonly string[];
  readonly mustChangePassword?: boolean;
}

async function open(options: Options = {}) {
  TestBed.configureTestingModule({
    providers: [
      provideRouter([
        {
          path: '',
          component: Shell,
          children: [
            { path: 'projects', component: ProjectsPage },
            { path: 'admin', component: UsersPage },
            { path: 'stock', component: Blank },
            { path: 'change-password', component: Blank },
          ],
        },
        { path: 'sign-in', component: Blank },
      ]),
      provideHttpClient(),
      provideHttpClientTesting(),
      {
        provide: Viewport,
        useValue: { isTablet: signal(true), isDesktop: signal(options.desktop ?? true) },
      },
    ],
  });
  const backend = TestBed.inject(HttpTestingController);
  await signInForTest(TestBed.inject(SessionState), backend, {
    ...(options.permissions ? { roles: ['Custom'], permissions: options.permissions } : {}),
    mustChangePassword: options.mustChangePassword ?? false,
  });
  const harness = await RouterTestingHarness.create();
  await harness.navigateByUrl(options.mustChangePassword ? '/change-password' : '/projects');
  await settle(harness.fixture);
  return {
    harness,
    backend,
    root: harness.fixture.nativeElement as HTMLElement,
    router: TestBed.inject(Router),
  };
}

const mainNav = (root: HTMLElement) => root.querySelector<HTMLElement>('nav[aria-label="Main"]');
const COUNT = '/api/needs-action/count';
const needsActionLink = (root: HTMLElement) =>
  mainNav(root)!.querySelector<HTMLAnchorElement>('a[href="/needs-action"]')!;

describe('Shell on a wide screen', () => {
  it('shows a navigation rail with every section the person may use, in order', async () => {
    const { root } = await open();

    expect(root.querySelector('.bottom-nav')).toBeNull();
    expect(texts(mainNav(root)!, 'a')).toEqual([
      'Dashboard',
      'Needs action',
      'Tasks',
      'Projects',
      'Workers',
      'Payroll',
      'Stock',
      'Receiving',
      'Purchasing',
      'Subcontracts',
      'Billing',
      'Admin',
    ]);
    expect(text(root, '.rail__brand')).toBe('Acme Builders');
  });

  it('marks the current section for assistive technology', async () => {
    const { root } = await open();

    const current = mainNav(root)!.querySelector('[aria-current="page"]');
    expect(current?.textContent?.trim()).toBe('Projects');
  });

  it('hides sections the person cannot use, instead of greying them out', async () => {
    const { root } = await open({
      permissions: [Permission.projectsRead, Permission.inventoryRead],
    });

    expect(texts(mainNav(root)!, 'a')).toEqual(['Projects', 'Stock']);
    expect(
      root.querySelectorAll('[aria-disabled="true"], a[disabled], button[disabled]'),
    ).toHaveLength(0);
  });

  it('shows Admin to someone who can only read the activity log', async () => {
    const { root } = await open({ permissions: [Permission.projectsRead, Permission.auditRead] });

    expect(texts(mainNav(root)!, 'a')).toEqual(['Projects', 'Admin']);
  });

  it('puts the page title in the header and in the browser tab, and follows the person to the next page', async () => {
    const { harness, root, router } = await open();
    expect(text(root, '.header__title')).toBe('Projects');
    expect(TestBed.inject(Title).getTitle()).toBe('Projects - Acme Builders');

    await router.navigateByUrl('/admin');
    await settle(harness.fixture);

    expect(text(root, '.header__title')).toBe('Users');
    expect(TestBed.inject(Title).getTitle()).toBe('Users - Acme Builders');
  });

  it('has exactly one top-level heading, the page title', async () => {
    const { root } = await open();

    expect(root.querySelectorAll('h1')).toHaveLength(1);
  });

  it('lets a keyboard user skip the navigation', async () => {
    const { root } = await open();

    const skip = root.querySelector<HTMLAnchorElement>('.skip-link')!;
    expect(skip.textContent?.trim()).toBe('Skip to main content');
    skip.click();

    expect(document.activeElement).toBe(root.querySelector('#main'));
  });
});

describe('Shell on a phone', () => {
  it('shows a bottom bar of four sections plus More, and no rail', async () => {
    const { root } = await open({ desktop: false });

    expect(root.querySelector('.rail')).toBeNull();
    expect(texts(mainNav(root)!, 'a, button')).toEqual([
      'Dashboard',
      'Needs action',
      'Tasks',
      'Projects',
      'More',
    ]);
    expect(mainNav(root)!.querySelectorAll('li')).toHaveLength(5);
  });

  it('opens a sheet with the rest, and a choice there goes to the page and closes the sheet', async () => {
    const { harness, root, router } = await open({ desktop: false });

    buttonNamed(mainNav(root)!, 'More').click();
    await settle(harness.fixture);
    const sheet = root.querySelector<HTMLDialogElement>('dialog')!;
    expect(sheet.hasAttribute('open')).toBe(true);
    expect(texts(sheet, '.more-list__link')).toEqual([
      'Workers',
      'Payroll',
      'Stock',
      'Receiving',
      'Purchasing',
      'Subcontracts',
      'Billing',
      'Admin',
    ]);

    buttonNamed(sheet, 'Stock').click();
    await settle(harness.fixture);

    expect(router.url).toBe('/stock');
    expect(root.querySelector('dialog')).toBeNull();
  });

  it('needs no More when everything fits', async () => {
    const { root } = await open({
      desktop: false,
      permissions: [Permission.projectsRead, Permission.inventoryRead],
    });

    expect(texts(mainNav(root)!, 'a, button')).toEqual(['Projects', 'Stock']);
  });
});

describe('Shell Needs action badge', () => {
  it('reads the count once when the shell starts, and shows no badge while nothing waits', async () => {
    const { harness, backend, root } = await open();

    backend.expectOne(COUNT).flush({ total: 0 });
    await settle(harness.fixture);

    expect(root.querySelector('.nav-badge')).toBeNull();
    expect(needsActionLink(root).getAttribute('aria-label')).toBeNull();
    expect(needsActionLink(root).textContent?.trim()).toBe('Needs action');
  });

  it('shows how many things wait, with the number in the link’s accessible name', async () => {
    const { harness, backend, root } = await open();

    backend.expectOne(COUNT).flush({ total: 6 });
    await settle(harness.fixture);

    expect(text(needsActionLink(root), '.nav-badge')).toBe('6');
    expect(needsActionLink(root).querySelector('.nav-badge')!.getAttribute('aria-hidden')).toBe(
      'true',
    );
    expect(needsActionLink(root).getAttribute('aria-label')).toBe('Needs action, 6 waiting');
    expect(root.querySelectorAll('.nav-badge')).toHaveLength(1);
  });

  it('caps the badge at 99+ but says the real number to assistive technology', async () => {
    const { harness, backend, root } = await open();

    backend.expectOne(COUNT).flush({ total: 132 });
    await settle(harness.fixture);

    expect(text(needsActionLink(root), '.nav-badge')).toBe('99+');
    expect(needsActionLink(root).getAttribute('aria-label')).toBe('Needs action, 132 waiting');
  });

  it('shows the badge on the phone bottom bar too', async () => {
    const { harness, backend, root } = await open({ desktop: false });

    backend.expectOne(COUNT).flush({ total: 3 });
    await settle(harness.fixture);

    expect(text(needsActionLink(root), '.nav-badge')).toBe('3');
    expect(needsActionLink(root).getAttribute('aria-label')).toBe('Needs action, 3 waiting');
  });

  it('never asks for someone whose role acts on nothing on that page', async () => {
    const { backend, root } = await open({
      permissions: [Permission.projectsRead, Permission.inventoryRead, Permission.actionsRead],
    });

    backend.expectNone(COUNT);
    expect(mainNav(root)!.querySelector('a[href="/needs-action"]')).toBeNull();
  });

  it('reads the count again when the browser tab becomes visible', async () => {
    const { harness, backend, root } = await open();
    backend.expectOne(COUNT).flush({ total: 1 });

    Object.defineProperty(document, 'visibilityState', {
      configurable: true,
      get: () => 'visible',
    });
    document.dispatchEvent(new Event('visibilitychange'));
    backend.expectOne(COUNT).flush({ total: 4 });
    await settle(harness.fixture);

    expect(text(needsActionLink(root), '.nav-badge')).toBe('4');
  });
});

describe('Shell while a new password is required', () => {
  it('shows no navigation, only the way to sign out', async () => {
    const { harness, root, backend } = await open({ mustChangePassword: true });

    backend.expectNone(COUNT);
    expect(mainNav(root)).toBeNull();
    expect(root.querySelector('.rail')).toBeNull();
    expect(root.querySelector('.shell--minimal')).not.toBeNull();

    root.querySelector<HTMLButtonElement>('app-user-menu button')!.click();
    await settle(harness.fixture);
    expect(texts(root, '.menu__panel .menu__item')).toEqual(['Sign out']);
  });
});

describe('Shell user menu', () => {
  it('shows who is signed in, with roles and company', async () => {
    const { harness, root } = await open();

    const trigger = root.querySelector<HTMLButtonElement>('app-user-menu button')!;
    expect(trigger.getAttribute('aria-label')).toBe('Account menu for Ana Reyes');
    expect(text(trigger, '.avatar')).toBe('AR');
    trigger.click();
    await settle(harness.fixture);

    expect(text(root, '.menu__title')).toBe('Ana Reyes');
    expect(texts(root, '.menu__meta')).toEqual(['Administrator', 'Acme Builders']);
    expect(texts(root, '.menu__panel .menu__item')).toEqual(['Change password', 'Sign out']);
    expect(
      buttonNamed(root.querySelector('.menu__panel')!, 'Change password').getAttribute('href'),
    ).toBe('/change-password');
  });

  it('signs out on the server, then goes to sign-in', async () => {
    const { harness, backend, root, router } = await open();
    root.querySelector<HTMLButtonElement>('app-user-menu button')!.click();
    await settle(harness.fixture);

    buttonNamed(root, 'Sign out').click();
    await settle(harness.fixture);
    backend
      .expectOne('/api/identity/sign-out')
      .flush(null, { status: 204, statusText: 'No Content' });
    await flushMicrotasks(8);
    await harness.fixture.whenStable();

    expect(TestBed.inject(SessionState).isSignedIn()).toBe(false);
    expect(router.url).toBe('/sign-in');
  });

  it('stays signed in and says so when sign-out could not reach the server', async () => {
    const { harness, backend, root, router } = await open();
    root.querySelector<HTMLButtonElement>('app-user-menu button')!.click();
    await settle(harness.fixture);

    buttonNamed(root, 'Sign out').click();
    await settle(harness.fixture);
    backend
      .expectOne('/api/identity/sign-out')
      .flush(null, { status: 0, statusText: 'Unknown Error' });
    await flushMicrotasks(8);
    await settle(harness.fixture);

    expect(TestBed.inject(SessionState).isSignedIn()).toBe(true);
    expect(router.url).toBe('/projects');
    expect(text(root, '.toast__message')).toBe(
      "We couldn't sign you out. Check your connection and try again.",
    );
  });
});
