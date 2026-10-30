import { HttpErrorResponse, provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { SKIP_ERROR_TOAST } from '@app/auth/skip-error-toast.context';
import { GroupSessionsResponse } from '@app/models/group-orders/group-sessions-response.model';
import { API_BASE_URL } from '@app/shared/config/api-base-url.token';
import { GroupOrdersService } from './group-orders.service';

const BASE = 'https://api.test';
const dish = { id: 'salad', name: 'Salad', price: 8, imageUrl: null };
const snapshot: GroupSessionsResponse = {
  participants: [
    { id: 'opaque-owner', type: 'Owner', name: 'Owner', isSubmitted: false, items: [], total: 0 },
    {
      id: 'guest',
      type: 'Guest',
      name: 'Guest',
      isSubmitted: true,
      items: [{ dish, quantity: 2 }],
      total: 16,
    },
  ],
  total: 16,
};

describe('Group orders real API read-model integration', () => {
  let service: GroupOrdersService;
  let http: HttpTestingController;
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: API_BASE_URL, useValue: BASE },
      ],
    });
    service = TestBed.inject(GroupOrdersService);
    http = TestBed.inject(HttpTestingController);
    service.getOwnerSessionId$('reservation').subscribe();
    http
      .expectOne(`${BASE}/reservations/reservation/guest-session-id`)
      .flush({ guestSessionId: 'opaque-owner', groupReservationId: 'group' });
  });
  afterEach(() => http.verify());

  function flushRead(status: 'Open' | 'Submitted' = 'Open', quantity = 1) {
    http.expectOne(`${BASE}/guest/groups/group/sessions`).flush(structuredClone(snapshot));
    http
      .expectOne(`${BASE}/orders/reservation`)
      .flush({ orderItems: [{ dish, quantity }], status: status === 'Open' ? 0 : 3 });
    http
      .expectOne(`${BASE}/guest/groups/group/status`)
      .flush({ currentParticipants: 2, maxCapacity: 4, status });
  }

  it('uses additive owner context and reservation metadata without parsing IDs or resolving a restricted invitation', () => {
    let groupId = '';
    service
      .getGroupInvite$('reservation')
      .subscribe((invite) => (groupId = invite.GroupReservationId));
    http.expectOne(`${BASE}/reservations/reservation`).flush({
      id: 'reservation',
      startTime: '2030-01-01T16:00:00Z',
      endTime: '2030-01-01T17:00:00Z',
      guestCount: 4,
      locationAddress: 'Street',
      restaurantName: 'Restaurant',
    });
    expect(groupId).toBe('group');
    http.expectNone(
      (request) => request.url.includes('invite-token') || request.url.includes('/guest/invites/')
    );
  });

  it('projects an ordinary Owner preorder without changing raw session submission flags or guest carts', () => {
    let result: GroupSessionsResponse | undefined;
    service.getSessions$('group').subscribe((value) => (result = value));
    flushRead();
    expect(result?.ownerPreOrder?.total).toBe(8);
    expect(result?.participants[0].isSubmitted).toBeFalse();
    expect(result?.participants[0].items).toEqual([]);
    expect(result?.participants[1].total).toBe(16);
    expect(result?.submittedOrder).toBeUndefined();
  });

  it('uses the final persisted order after submission instead of injecting the whole order into the Owner cart', () => {
    let result: GroupSessionsResponse | undefined;
    service.getSessions$('group').subscribe((value) => (result = value));
    flushRead('Submitted', 3);
    expect(result?.submittedOrder?.total).toBe(24);
    expect(result?.ownerPreOrder).toBeUndefined();
    expect(result?.participants[0].items).toEqual([]);
  });

  it('treats only missing order as empty and sends its read quietly', () => {
    let result: GroupSessionsResponse | undefined;
    service.getSessions$('group', { silent: true }).subscribe((value) => (result = value));
    http.expectOne(`${BASE}/guest/groups/group/sessions`).flush(snapshot);
    const order = http.expectOne(`${BASE}/orders/reservation`);
    expect(order.request.context.get(SKIP_ERROR_TOAST)).toBeTrue();
    order.flush({ error: 'Missing' }, { status: 404, statusText: 'Not Found' });
    http.expectOne(`${BASE}/guest/groups/group/status`).flush({ status: 'Open' });
    expect(result?.participants).toEqual(snapshot.participants);
    expect(result?.ownerPreOrder).toBeUndefined();
  });

  it('does not turn an unexpected ordinary-order failure into a misleading successful refresh', () => {
    let error: HttpErrorResponse | undefined;
    service
      .getSessions$('group', { silent: true })
      .subscribe({ error: (value: HttpErrorResponse) => (error = value) });
    const sessions = http.expectOne(`${BASE}/guest/groups/group/sessions`);
    const order = http.expectOne(`${BASE}/orders/reservation`);
    const status = http.expectOne(`${BASE}/guest/groups/group/status`);
    order.flush({ error: 'Unavailable' }, { status: 500, statusText: 'Error' });
    expect(error?.status).toBe(500);
    expect(sessions.cancelled).toBeTrue();
    expect(status.cancelled).toBeTrue();
  });

  it('keeps DELETE response membership and filters a removed guest from a later stale GSI snapshot', () => {
    service.getSessions$('group').subscribe();
    flushRead();
    let removed: GroupSessionsResponse | undefined;
    service.removeSession$('group', 'guest').subscribe((value) => (removed = value));
    http
      .expectOne(`${BASE}/guest/groups/group/sessions/guest`)
      .flush({ participants: [snapshot.participants[0]], total: 0 });
    expect(removed?.ownerPreOrder?.total).toBe(8);
    expect(removed?.participants.length).toBe(1);
    let refreshed: GroupSessionsResponse | undefined;
    service.getSessions$('group', { silent: true }).subscribe((value) => (refreshed = value));
    flushRead();
    expect(refreshed?.participants.map((person) => person.id)).toEqual(['opaque-owner']);
    expect(refreshed?.total).toBe(0);
  });
  it('enriches a successful mutation with one final persisted-order read', () => {
    let result: import('@app/models/group-orders/submit-group-pre-order.model').SubmittedDish[] =
      [];
    service.submit$('group', 'reservation').subscribe((value) => (result = value));
    http
      .expectOne(`${BASE}/group-reservations/group/submit`)
      .flush([{ dishId: 'salad', quantity: 3 }]);
    const order = http.expectOne(`${BASE}/orders/reservation`);
    expect(order.request.context.get(SKIP_ERROR_TOAST)).toBeTrue();
    order.flush({ orderItems: [{ dish, quantity: 3 }], status: 3 });
    expect(result[0].quantity).toBe(3);
    expect(result[0].dish?.price).toBe(8);
  });

  it('does not retry or reject an already committed mutation when the final read fails', () => {
    let result: import('@app/models/group-orders/submit-group-pre-order.model').SubmittedDish[] =
      [];
    let failed = false;
    service
      .submit$('group', 'reservation')
      .subscribe({ next: (value) => (result = value), error: () => (failed = true) });
    http
      .expectOne(`${BASE}/group-reservations/group/submit`)
      .flush([{ dishId: 'salad', quantity: 3 }]);
    http
      .expectOne(`${BASE}/orders/reservation`)
      .flush({ error: 'Unavailable' }, { status: 500, statusText: 'Error' });
    expect(failed).toBeFalse();
    expect(result[0].quantity).toBe(3);
    expect(result[0].summaryReadFailed).toBeTrue();
    http.expectNone((request) => request.method === 'POST');
  });
  it('does not present an old Created order as the final order when the group status is already Submitted', () => {
    let result: GroupSessionsResponse | undefined;
    service.getSessions$('group').subscribe((value) => (result = value));
    http.expectOne(`${BASE}/guest/groups/group/sessions`).flush(snapshot);
    http
      .expectOne(`${BASE}/orders/reservation`)
      .flush({ orderItems: [{ dish, quantity: 1 }], status: 0 });
    http
      .expectOne(`${BASE}/guest/groups/group/status`)
      .flush({ currentParticipants: 2, maxCapacity: 4, status: 'Submitted' });
    expect(result?.finalSummaryPending).toBeTrue();
    expect(result?.ownerPreOrder).toBeUndefined();
    expect(result?.submittedOrder).toBeUndefined();
  });
});
