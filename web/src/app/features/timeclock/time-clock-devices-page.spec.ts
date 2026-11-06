import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { Observable, of } from 'rxjs';
import { SessionState } from '../../core/session/session-state';
import { buttonNamed, flushMicrotasks, settle, text, texts } from '../../testing/dom';
import { signInForTest } from '../../testing/session-fixture';
import { TimeClockApi } from './timeclock-api';
import { TimeClockDevicesPage } from './time-clock-devices-page';
import { TimeClockDevice, TimeClockDeviceList } from './timeclock.models';

type Call = (...args: unknown[]) => Observable<unknown>;

const device = (overrides: Partial<TimeClockDevice> = {}): TimeClockDevice => ({
  id: 'd1',
  name: 'Main gate',
  projectId: 'p1',
  projectName: 'Riverside Residences',
  kind: 'Kiosk',
  status: 'Active',
  lastSeenAtUtc: null,
  createdAtUtc: '2026-11-01T00:00:00Z',
  version: 1,
  ...overrides,
});

async function open(list: TimeClockDeviceList) {
  const api = {
    devices: vi.fn<Call>(() => of(list)),
    projects: vi.fn<Call>(() => of([{ id: 'p1', name: 'Riverside Residences', status: 'Active' }])),
    registerDevice: vi.fn<Call>(() =>
      of({ device: device({ id: 'd9', name: 'Back gate' }), key: 'THE-ONE-TIME-KEY' }),
    ),
    rotateKey: vi.fn<Call>(() => of({ device: device(), key: 'A-NEW-KEY' })),
    revoke: vi.fn<Call>(() => of(device({ status: 'Revoked' }))),
  };
  TestBed.configureTestingModule({
    providers: [
      provideRouter([]),
      provideHttpClient(),
      provideHttpClientTesting(),
      { provide: TimeClockApi, useValue: api },
    ],
  });
  await signInForTest(TestBed.inject(SessionState), TestBed.inject(HttpTestingController));
  const fixture = TestBed.createComponent(TimeClockDevicesPage);
  await settle(fixture);
  return { fixture, root: fixture.nativeElement as HTMLElement, api };
}

describe('TimeClockDevicesPage', () => {
  it('says the time clock needs setup until a device connects, and that attendance is typed by hand until then', async () => {
    const { root } = await open({
      items: [device()],
      capability: {
        status: 'NeedsSetup',
        message:
          "Time clock: Needs setup (no device has connected yet). Register a time clock below and enter its key on the machine. Until then, record attendance on the project's Team tab.",
      },
    });

    expect(text(root, '.alert__title')).toBe('Time clock: Needs setup');
    expect(text(root, '.alert__text')).toBe(
      "Register a time clock below and enter its key on the machine. Until then, record attendance on the project's Team tab.",
    );
    expect(texts(root, '.chip')).toEqual(['Not connected yet']);
  });

  it('shows a connected and a revoked device, and offers no action on a revoked one', async () => {
    const { root } = await open({
      items: [
        device({ lastSeenAtUtc: '2026-11-02T00:05:00Z' }),
        device({ id: 'd2', name: 'Old gate', status: 'Revoked', kind: 'Terminal' }),
      ],
      capability: {
        status: 'Available',
        message:
          'Time clock: Available. Scans come in from your registered machines; confirm each day on the review page.',
      },
    });

    expect(text(root, '.alert__title')).toBe('Time clock: Available');
    expect(texts(root, '.chip')).toEqual(['Connected', 'Revoked']);
    expect(root.textContent).toContain('Last sent scans 2 Nov 2026, 8:05 am');
    expect(root.querySelectorAll('.devices__actions')).toHaveLength(1);
  });

  it('registers a device and shows its key once, with a copy button', async () => {
    const { fixture, root, api } = await open({
      items: [],
      capability: { status: 'NeedsSetup', message: 'Time clock: Needs setup.' },
    });

    buttonNamed(root, 'Add a time clock').click();
    await settle(fixture);
    const name = document.querySelector<HTMLInputElement>('[formControlName="name"]')!;
    name.value = 'Back gate';
    name.dispatchEvent(new Event('input', { bubbles: true }));
    const project = document.querySelector<HTMLSelectElement>('[formControlName="projectId"]')!;
    project.value = 'p1';
    project.dispatchEvent(new Event('change', { bubbles: true }));
    buttonNamed(document.body, 'Add time clock').click();
    await settle(fixture);
    await flushMicrotasks();
    await settle(fixture);

    expect(api.registerDevice.mock.calls[0]!.slice(0, 3)).toEqual(['Back gate', 'p1', 'Kiosk']);
    expect(text(root, '.device-key code')).toBe('THE-ONE-TIME-KEY');
    expect(texts(root, '.alert__title')).toContain('Key for Back gate: shown only now');
    buttonNamed(root, "I've entered it").click();
    await settle(fixture);
    expect(root.textContent).not.toContain('THE-ONE-TIME-KEY');
  });

  it('gives a device a new key and revokes one after a confirmation', async () => {
    const { fixture, root, api } = await open({
      items: [device()],
      capability: { status: 'NeedsSetup', message: 'Time clock: Needs setup.' },
    });

    buttonNamed(root, 'Give it a new key').click();
    await settle(fixture);
    expect(api.rotateKey).toHaveBeenCalledWith('d1', 1);
    expect(text(root, '.device-key code')).toBe('A-NEW-KEY');

    buttonNamed(root, 'Revoke').click();
    await settle(fixture);
    expect(document.body.textContent).toContain('Main gate stops sending scans at once, for good.');
    buttonNamed(document.body, 'Revoke time clock').click();
    await settle(fixture);
    expect(api.revoke).toHaveBeenCalledWith('d1', 1);
  });
});
