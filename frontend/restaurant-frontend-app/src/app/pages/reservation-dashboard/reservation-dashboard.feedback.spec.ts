import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, TestRequest, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { AuthenticationService } from '@app/auth/services/authentication.service';
import { Reservation, ReservationStatus } from '@app/models/reservations/reservation.model';
import { API_BASE_URL } from '@app/shared/config/api-base-url.token';
import { ReservationDashboardComponent } from './reservation-dashboard.component';

describe('ReservationDashboardComponent feedback loading', () => {
  const base = 'https://test.api';
  let fixture: ReturnType<typeof TestBed.createComponent<ReservationDashboardComponent>>;
  let http: HttpTestingController;

  const reservation = (id: string): Reservation => ({
    id, tableId: 'table-1', customerId: 'owner-1', waiterId: 'waiter-1',
    startTime: new Date(2030, 10, 3, 12), endTime: new Date(2030, 10, 3, 13),
    guestCount: 1, status: ReservationStatus.Reserved, tableCapacity: 2,
    restaurantName: 'Test', locationAddress: id,
  });

  const feedback = (id: string) => ({
    service: {
      userId: 'owner-1', locationId: 'location-1', reservationId: id,
      username: 'Test', rating: 4, comment: id, createdAt: '2030-11-03', type: 'Service',
    },
    cuisineExperience: null,
  });

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ReservationDashboardComponent],
      providers: [
        provideRouter([]), provideHttpClient(), provideHttpClientTesting(),
        { provide: API_BASE_URL, useValue: base },
        { provide: AuthenticationService, useValue: {
          user$: of(null), isAuthorised$: of(false),
          getUser: () => ({ id: 'owner-1', role: 'Customer' }),
          getToken: () => 'test-token',
        } },
      ],
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(ReservationDashboardComponent);
  });

  afterEach(() => http.verify());

  function start(count: number): void {
    fixture.detectChanges();
    http.expectOne(`${base}/reservations`).flush(
      Array.from({ length: count }, (_, index) => reservation(`r${index + 1}`)),
    );
    http.expectOne(`${base}/orders`).flush([]);
  }

  function pendingFeedback(): TestRequest[] {
    return http.match(request => request.url.endsWith('/feedback'));
  }

  function finish(requests: TestRequest[]): void {
    for (const request of requests) {
      const id = request.request.url.split('/').at(-2)!;
      request.flush(feedback(id));
    }
  }

  it('keeps at most three feedback requests active and restores card order after out-of-order responses', () => {
    start(7);
    const first = pendingFeedback();
    expect(first.map(request => request.request.url)).toEqual([
      `${base}/reservations/r1/feedback`, `${base}/reservations/r2/feedback`,
      `${base}/reservations/r3/feedback`,
    ]);

    first[2].flush(feedback('r3'));
    const next = pendingFeedback();
    expect(next.map(request => request.request.url)).toEqual([`${base}/reservations/r4/feedback`]);
    first[0].flush(feedback('r1'));
    first[1].flush(feedback('r2'));
    const middle = pendingFeedback();
    expect(middle).toHaveSize(2);
    finish([...next, ...middle]);
    finish(pendingFeedback());

    const rows = fixture.componentInstance.reservations();
    expect(rows.map(row => row.id)).toEqual(['r1', 'r2', 'r3', 'r4', 'r5', 'r6', 'r7']);
    expect(rows.map(row => row.feedback?.service.reservationId)).toEqual(
      ['r1', 'r2', 'r3', 'r4', 'r5', 'r6', 'r7'],
    );
  });

  it('retains all reservations when an individual feedback request fails', () => {
    spyOn(console, 'error');
    start(4);
    const first = pendingFeedback();
    first[1].flush({ error: 'unavailable' }, { status: 500, statusText: 'Server Error' });
    const next = pendingFeedback();
    expect(next).toHaveSize(1);
    finish([first[0], first[2], next[0]]);

    const rows = fixture.componentInstance.reservations();
    expect(rows.map(row => row.id)).toEqual(['r1', 'r2', 'r3', 'r4']);
    expect(rows[1].feedback).toBeUndefined();
    expect(rows[0].feedback?.service.reservationId).toBe('r1');
    expect(rows[2].feedback?.service.reservationId).toBe('r3');
    expect(rows[3].feedback?.service.reservationId).toBe('r4');
    expect(fixture.componentInstance.reservationsLoaded()).toBeTrue();
  });

  it('cancels an earlier feedback batch before starting a new dashboard load', () => {
    start(5);
    const oldRequests = pendingFeedback();
    expect(oldRequests).toHaveSize(3);

    const dashboard = fixture.componentInstance as unknown as { loadReservations(): void };
    dashboard.loadReservations();
    expect(oldRequests.every(request => request.cancelled)).toBeTrue();
    http.expectOne(`${base}/reservations`).flush(
      Array.from({ length: 5 }, (_, index) => reservation(`new-${index + 1}`)),
    );
    http.expectOne(`${base}/orders`).flush([]);
    const firstNewRequests = pendingFeedback();
    expect(firstNewRequests).toHaveSize(3);
    finish(firstNewRequests);
    finish(pendingFeedback());
    expect(fixture.componentInstance.reservations().map(row => row.id)).toEqual(
      ['new-1', 'new-2', 'new-3', 'new-4', 'new-5'],
    );
  });

  it('cancels active requests and never starts queued requests after destruction', () => {
    start(5);
    const active = pendingFeedback();
    expect(active).toHaveSize(3);
    fixture.destroy();
    expect(active.every(request => request.cancelled)).toBeTrue();
    http.expectNone(`${base}/reservations/r4/feedback`);
    http.expectNone(`${base}/reservations/r5/feedback`);
  });
});
