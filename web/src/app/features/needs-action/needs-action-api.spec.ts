import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { NeedsActionApi } from './needs-action-api';
import { everyGroup } from './needs-action.fixtures';

function setup() {
  TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
  return { api: TestBed.inject(NeedsActionApi), http: TestBed.inject(HttpTestingController) };
}

describe('NeedsActionApi', () => {
  afterEach(() => TestBed.inject(HttpTestingController).verify());

  it('reads the queue with no parameters: the server decides the groups from who is asking', () => {
    const { api, http } = setup();
    let groups = 0;

    api.get().subscribe((result) => (groups = result.groups.length));
    const request = http.expectOne('/api/needs-action');
    expect(request.request.method).toBe('GET');
    expect(request.request.params.keys()).toEqual([]);
    request.flush(everyGroup());

    expect(groups).toBe(11);
  });

  it('reads only the number for the badge', () => {
    const { api, http } = setup();
    let total = -1;

    api.count().subscribe((result) => (total = result.total));
    const request = http.expectOne('/api/needs-action/count');
    expect(request.request.method).toBe('GET');
    request.flush({ total: 6 });

    expect(total).toBe(6);
  });
});
