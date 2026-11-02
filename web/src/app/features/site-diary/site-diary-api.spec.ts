import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { SiteDiaryApi } from './site-diary-api';
import { DiaryDetailsInput } from './site-diary.models';

const details: DiaryDetailsInput = {
  weather: 'Rain',
  weatherNote: null,
  workDone: 'Poured the slab.',
  issuesAndDelays: null,
  visitorsAndInspections: null,
  safetyNotes: null,
  subcontractorWorkers: 4,
};

function setup() {
  TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
  return { api: TestBed.inject(SiteDiaryApi), http: TestBed.inject(HttpTestingController) };
}

describe('SiteDiaryApi', () => {
  afterEach(() => TestBed.inject(HttpTestingController).verify());

  it('lists a project’s reports a page at a time', () => {
    const { api, http } = setup();

    api.list('p1', 2, 25).subscribe();
    const request = http.expectOne((req) => req.url === '/api/projects/p1/diary');

    expect(request.request.method).toBe('GET');
    expect(request.request.params.get('page')).toBe('2');
    expect(request.request.params.get('pageSize')).toBe('25');
    request.flush({ items: [], total: 0, page: 2, pageSize: 25 });
  });

  it('reads one day’s report and the day’s attendance and deliveries', () => {
    const { api, http } = setup();

    api.get('p1', '2026-11-02').subscribe();
    api.filledIn('p1', '2026-11-02').subscribe();

    expect(http.expectOne('/api/projects/p1/diary/2026-11-02').request.method).toBe('GET');
    expect(http.expectOne('/api/projects/p1/diary/2026-11-02/filled-in').request.method).toBe('GET');
  });

  it('writes a report with its day and one request id', () => {
    const { api, http } = setup();

    api.create('p1', '2026-11-02', details, 'req-1').subscribe();
    const request = http.expectOne('/api/projects/p1/diary');

    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ ...details, reportDate: '2026-11-02', clientRequestId: 'req-1' });
  });

  it('corrects a report with the version the person opened', () => {
    const { api, http } = setup();

    api.correct('p1', '2026-11-02', details, 3).subscribe();
    const request = http.expectOne('/api/projects/p1/diary/2026-11-02');

    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual({ ...details, version: 3 });
  });

  it('voids a report with a reason and the version', () => {
    const { api, http } = setup();

    api.voidReport('p1', '2026-11-02', 'Wrong day', 3).subscribe();
    const request = http.expectOne('/api/projects/p1/diary/2026-11-02/void');

    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ reason: 'Wrong day', version: 3 });
  });
});
