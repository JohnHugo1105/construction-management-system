import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { apiErrorInterceptor } from '../../core/api/api-error-interceptor';
import { authInterceptor } from '../../core/api/auth-interceptor';
import { KioskApi, KioskSendResult } from './kiosk-api';
import { TimeClockApi } from './timeclock-api';

function setup() {
  TestBed.configureTestingModule({
    providers: [
      provideRouter([]),
      provideHttpClient(withInterceptors([authInterceptor, apiErrorInterceptor])),
      provideHttpClientTesting(),
    ],
  });
  return {
    api: TestBed.inject(TimeClockApi),
    kiosk: TestBed.inject(KioskApi),
    http: TestBed.inject(HttpTestingController),
  };
}

describe('TimeClockApi', () => {
  afterEach(() => TestBed.inject(HttpTestingController).verify());

  it('reads a project day with the date and page, or the earliest day waiting without a date', () => {
    const { api, http } = setup();

    api.days('p1', '2026-11-01', 2).subscribe();
    const dated = http.expectOne((request) => request.url === '/api/timeclock/days');
    expect(dated.request.params.get('projectId')).toBe('p1');
    expect(dated.request.params.get('date')).toBe('2026-11-01');
    expect(dated.request.params.get('page')).toBe('2');
    dated.flush({});

    api.days('p1', null).subscribe();
    const earliest = http.expectOne((request) => request.url === '/api/timeclock/days');
    expect(earliest.request.params.has('date')).toBe(false);
    earliest.flush({});

    api.summary('p1').subscribe();
    expect(http.expectOne('/api/timeclock/summary?projectId=p1').request.method).toBe('GET');
  });

  it('confirms rows, adds and removes scans, and imports a file as text', () => {
    const { api, http } = setup();
    const confirm = {
      projectId: 'p1',
      workDate: '2026-11-01',
      rows: [{ workerId: 'w1', approveOvertime: true }],
      allClean: false,
    };

    api.confirm(confirm).subscribe();
    const confirmed = http.expectOne('/api/timeclock/days/confirm');
    expect([confirmed.request.method, confirmed.request.body]).toEqual(['POST', confirm]);
    confirmed.flush({ confirmed: [], skipped: [] });

    const manual = {
      projectId: 'p1',
      workerId: 'w1',
      at: '2026-11-01T17:00',
      direction: 'Out' as const,
      reason: 'Badge at home',
      clientRequestId: 'r1',
    };
    api.addManual(manual).subscribe();
    expect(http.expectOne('/api/timeclock/punches/manual').request.body).toEqual(manual);

    api.voidPunch('s1', 'Wrong badge', 3).subscribe();
    expect(http.expectOne('/api/timeclock/punches/s1/void').request.body).toEqual({
      reason: 'Wrong badge',
      version: 3,
    });

    api.importFile('p1', 'code,timestamp\nABCDEFGHJKMN,2026-11-01 07:00').subscribe();
    const imported = http.expectOne('/api/timeclock/import?projectId=p1');
    expect(imported.request.body).toEqual({ csv: 'code,timestamp\nABCDEFGHJKMN,2026-11-01 07:00' });
  });

  it('reads badges for the chosen workers, issues codes, and manages devices', () => {
    const { api, http } = setup();

    api.badges(['w1', 'w2']).subscribe();
    const badges = http.expectOne((request) => request.url === '/api/timeclock/badges');
    expect(badges.request.params.getAll('workerIds')).toEqual(['w1', 'w2']);
    badges.flush({ companyName: 'Acme', items: [], activeWithoutCode: 0 });

    api.issueMissing().subscribe();
    expect(http.expectOne('/api/timeclock/badges/issue-missing').request.method).toBe('POST');
    api.issueCode('w1', 4).subscribe();
    expect(http.expectOne('/api/workers/w1/punch-code').request.body).toEqual({ version: 4 });

    api.devices().subscribe();
    expect(http.expectOne('/api/timeclock/devices').request.method).toBe('GET');
    api.registerDevice('Main gate', 'p1', 'Kiosk', 'r1').subscribe();
    expect(http.expectOne('/api/timeclock/devices').request.body).toEqual({
      name: 'Main gate',
      projectId: 'p1',
      kind: 'Kiosk',
      clientRequestId: 'r1',
    });
    api.rotateKey('d1', 2).subscribe();
    expect(http.expectOne('/api/timeclock/devices/d1/rotate-key').request.body).toEqual({
      version: 2,
    });
    api.revoke('d1', 3).subscribe();
    expect(http.expectOne('/api/timeclock/devices/d1/revoke').request.body).toEqual({ version: 3 });

    api.projects().subscribe();
    http.expectOne('/api/projects/lookup');
    api.findWorkers('Juan').subscribe();
    http.expectOne('/api/workers/lookup?search=Juan');
  });
});

describe('KioskApi', () => {
  afterEach(() => TestBed.inject(HttpTestingController).verify());

  const scan = {
    clientPunchId: 'c1',
    workerCode: 'ABCDEFGHJKMN',
    punchedAt: '2026-11-02T00:02:00.000Z',
  };

  it('sends the scans with the device key in its header and no cookie-based token', async () => {
    const { kiosk, http } = setup();

    const sent = firstValueFrom(kiosk.send('the-key', [scan]));
    const request = http.expectOne('/api/timeclock/punches');
    expect(request.request.headers.get('X-Device-Key')).toBe('the-key');
    expect(request.request.headers.has('X-XSRF-TOKEN')).toBe(false);
    expect(request.request.body).toEqual({ punches: [scan] });
    request.flush({ results: [] });

    expect(await sent).toEqual<KioskSendResult>({ kind: 'answered', result: { results: [] } });
  });

  it('tells a refused key from a server that cannot be reached, and never signs anyone out', async () => {
    const { kiosk, http } = setup();

    const refused = firstValueFrom(kiosk.send('old-key', [scan]));
    http.expectOne('/api/timeclock/punches').flush({}, { status: 401, statusText: 'Unauthorized' });
    expect(await refused).toEqual({ kind: 'key-refused' });

    const offline = firstValueFrom(kiosk.send('key', [scan]));
    http.expectOne('/api/timeclock/punches').error(new ProgressEvent('error'));
    expect(await offline).toEqual({ kind: 'unreachable' });

    const busy = firstValueFrom(kiosk.send('key', [scan]));
    http
      .expectOne('/api/timeclock/punches')
      .flush({}, { status: 429, statusText: 'Too Many Requests' });
    expect(await busy).toEqual({ kind: 'unreachable' });
  });

  it('checks a key with an empty batch: refused as empty means the key works', async () => {
    const { kiosk, http } = setup();

    const good = firstValueFrom(kiosk.checkKey('good'));
    const request = http.expectOne('/api/timeclock/punches');
    expect(request.request.body).toEqual({ punches: [] });
    request.flush({}, { status: 422, statusText: 'Unprocessable Entity' });
    expect(await good).toBe(true);

    const bad = firstValueFrom(kiosk.checkKey('bad'));
    http.expectOne('/api/timeclock/punches').flush({}, { status: 401, statusText: 'Unauthorized' });
    expect(await bad).toBe(false);

    const offline = firstValueFrom(kiosk.checkKey('later'));
    http.expectOne('/api/timeclock/punches').error(new ProgressEvent('error'));
    expect(await offline).toBeNull();
  });
});
