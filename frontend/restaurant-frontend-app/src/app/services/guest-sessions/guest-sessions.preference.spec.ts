import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { API_BASE_URL } from '@app/shared/config/api-base-url.token';
import { GuestSessionsService } from './guest-sessions.service';

describe('GuestSessionsService ordering preference', () => {
  let service: GuestSessionsService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting(),
      { provide: API_BASE_URL, useValue: 'https://test.api' }] });
    service = TestBed.inject(GuestSessionsService);
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());

  it('uses the guest token and revision, without sending or clearing cart dishes', () => {
    service.setPreference('guest-1', 'opaque-token', 'InRestaurant', 2).subscribe();
    const req = http.expectOne('https://test.api/guest/sessions/guest-1/ordering-preference');
    expect(req.request.method).toBe('PATCH');
    expect(req.request.headers.get('Authorization')).toBe('Bearer opaque-token');
    expect(req.request.body).toEqual({ preference: 'InRestaurant', expectedRevision: 2 });
    req.flush({ name: 'Guest', items: [], total: 0, isSubmitted: false,
      orderingPreference: 'InRestaurant', revision: 3 });
  });
});
