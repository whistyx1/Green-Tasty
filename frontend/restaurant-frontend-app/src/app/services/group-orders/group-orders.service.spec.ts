// Рівень 2 · сервіс GroupOrdersService: URL, метод, тіло кожного запиту.
// Запуск: npm run test:hw
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { API_BASE_URL } from '@app/shared/config/api-base-url.token';
import { GroupOrdersService } from './group-orders.service';

const BASE_URL = 'https://api.test';

describe('Рівень 2 · GroupOrdersService', () => {
  let service: GroupOrdersService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), { provide: API_BASE_URL, useValue: BASE_URL }],
    });
    service = TestBed.inject(GroupOrdersService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('getGroupInvite$: reservationId → invite-token → invite', () => {
    let groupId = '';
    service.getGroupInvite$('res-1').subscribe((invite) => (groupId = invite.GroupReservationId));

    http.expectOne(`${BASE_URL}/reservations/res-1/invite-token`).flush({ token: 'tok' });
    http.expectOne(`${BASE_URL}/guest/invites/tok`).flush({ GroupReservationId: 'grp-1' });

    expect(groupId).toBe('grp-1');
  });

  it('getSessions$: GET /guest/groups/{id}/sessions', () => {
    service.getSessions$('grp-1').subscribe();
    const req = http.expectOne(`${BASE_URL}/guest/groups/grp-1/sessions`);
    expect(req.request.method).toBe('GET');
    req.flush({ participants: [], total: 0 });
  });

  it('getOwnerSessionId$: повертає рядок guestSessionId', () => {
    let id = '';
    service.getOwnerSessionId$('res-1').subscribe((value) => (id = value));
    const req = http.expectOne(`${BASE_URL}/reservations/res-1/guest-session-id`);
    expect(req.request.method).toBe('GET');
    req.flush({ guestSessionId: 'owner-1' });
    expect(id).toBe('owner-1');
  });

  it('getStatus$: GET /guest/groups/{id}/status', () => {
    service.getStatus$('grp-1').subscribe();
    const req = http.expectOne(`${BASE_URL}/guest/groups/grp-1/status`);
    expect(req.request.method).toBe('GET');
    req.flush({ currentParticipants: 3, maxCapacity: 5, status: 'Open' });
  });

  it('sends a reminder to the selected guest session without guest email in the request', () => {
    let result: unknown;
    service.remindGuest$('group', 'guest-1').subscribe(value => result = value);
    const request = http.expectOne(`${BASE_URL}/guest/groups/group/sessions/guest-1/reminder`);
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({});
    request.flush({ channel: 'InApp', nextAllowedAt: '2030-01-01T12:00:00Z' });
    expect(result).toEqual({ channel: 'InApp', nextAllowedAt: '2030-01-01T12:00:00Z' });
  });

  it('submit$: POST /group-reservations/{id}/submit з тілом { reservationId }', () => {
    service.submit$('grp-1', 'res-1').subscribe();
    const req = http.expectOne(`${BASE_URL}/group-reservations/grp-1/submit`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ reservationId: 'res-1' });
    req.flush([]);
  });

  it('sets only the authenticated owner preference with the displayed revision', () => {
    service.setOwnerPreference$('res-1', 'InRestaurant', 4).subscribe();
    const req = http.expectOne(`${BASE_URL}/reservations/res-1/group-ordering-preference`);
    expect(req.request.method).toBe('PATCH');
    expect(req.request.body).toEqual({ preference: 'InRestaurant', expectedRevision: 4 });
    req.flush({ orderingPreference: 'InRestaurant', isSubmitted: false, revision: 5 });
  });

  it('submits the owner draft with the revision shown in confirmation', () => {
    service.submitOwner$('res-1', 7).subscribe();
    const req = http.expectOne(`${BASE_URL}/reservations/res-1/group-owner-submit`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ expectedOrderRevision: 7 });
    req.flush({ orderingPreference: 'Online', isSubmitted: true, revision: 3 });
  });
});
