import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { of } from 'rxjs';
import { AuthenticationService } from '@app/auth/services/authentication.service';
import { Order } from '@app/models/orders/order.model';
import { ReservationCustomer, ReservationStatusCustomer } from '@app/models/reservations/reservation.model';
import { API_BASE_URL } from '@app/shared/config/api-base-url.token';
import { ReservationDashboardComponent } from './reservation-dashboard.component';

describe('ReservationDashboardComponent filters', () => {
  const base = 'https://test.api';
  const makeOrder = (id: string, status: number): Order => ({
    orderId: `order-${id}`, reservationId: id, status,
    reservationAddress: 'Test', reservationTime: '2030-11-03', orderItems: [],
  });
  const makeReservation = (
    id: string, day: number, guestCount: number, status = ReservationStatusCustomer.Reserved,
    orderStatus?: number,
  ): ReservationCustomer => ({
    id, tableId: 'table-1', customerId: 'owner-1', waiterId: 'waiter-1',
    startTime: new Date(2030, 10, day, 12), endTime: new Date(2030, 10, day, 13, 30),
    guestCount, status, restaurantName: 'Test', locationAddress: id,
    ...(orderStatus === undefined ? {} : { preOrder: makeOrder(id, orderStatus) }),
  });
  const rows = [
    makeReservation('single-draft', 3, 1, ReservationStatusCustomer.Reserved, 0),
    makeReservation('single-submitted', 3, 1, ReservationStatusCustomer.Reserved, 3),
    makeReservation('group-open', 3, 3, ReservationStatusCustomer.Reserved, 0),
    makeReservation('group-final', 4, 3, ReservationStatusCustomer.Reserved, 3),
    makeReservation('cancelled', 3, 2, ReservationStatusCustomer.Cancelled),
  ];

  let fixture: ReturnType<typeof TestBed.createComponent<ReservationDashboardComponent>>;
  let component: ReservationDashboardComponent;
  let http: HttpTestingController;
  let signedIn: boolean;

  beforeEach(async () => {
    signedIn = false;
    await TestBed.configureTestingModule({
      imports: [ReservationDashboardComponent],
      providers: [
        provideRouter([]), provideHttpClient(), provideHttpClientTesting(),
        { provide: API_BASE_URL, useValue: base },
        { provide: AuthenticationService, useValue: {
          user$: of(null), isAuthorised$: of(false),
          getUser: () => signedIn ? { id: 'owner-1', role: 'Customer' } : null,
          getToken: () => signedIn ? 'test-token' : null,
        } },
      ],
    }).compileComponents();
    spyOn(TestBed.inject(Router), 'navigate').and.resolveTo(true);
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(ReservationDashboardComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
    component.customerId.set('owner-1');
    component.reservations.set(rows);
    component.reservationsLoaded.set(true);
    fixture.detectChanges();
  });

  afterEach(() => http.verify());

  function visibleIds(): string[] {
    return [...(fixture.nativeElement as HTMLElement).querySelectorAll('app-card-reservation .card')]
      .map(card => rows.find(row => card.textContent?.includes(row.locationAddress))?.id ?? 'unknown');
  }

  function change(id: string, value: string): void {
    const input = (fixture.nativeElement as HTMLElement).querySelector(`#${id}`) as HTMLInputElement | HTMLSelectElement;
    input.value = value;
    input.dispatchEvent(new Event('change'));
    fixture.detectChanges();
  }

  it('filters by reservation date in local calendar time', () => {
    change('reservation-date-filter', '2030-11-04');
    expect(visibleIds()).toEqual(['group-final']);
  });

  it('uses the displayed local date for reservations near midnight', () => {
    component.reservations.set([{ ...rows[0], startTime: new Date(2030, 10, 3, 0, 5) }]);
    fixture.detectChanges();
    change('reservation-date-filter', '2030-11-03');
    expect(visibleIds()).toEqual(['single-draft']);
    change('reservation-date-filter', '2030-11-04');
    expect(visibleIds()).toEqual([]);
  });

  it('filters by final submission status for ordinary and group reservations', () => {
    change('submission-status-filter', 'submitted');
    expect(visibleIds()).toEqual(['single-submitted', 'group-final']);
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Final group order:');
    change('submission-status-filter', 'not-submitted');
    expect(visibleIds()).toEqual(['single-draft', 'group-open', 'cancelled']);
    expect(component.submissionState({ ...rows[0], preOrder: makeOrder(rows[0].id, 1) })).toBe('submitted');
  });

  it('uses the active order when a cancelled historical order is returned first', () => {
    signedIn = true;
    component.ngOnInit();
    http.expectOne(`${base}/reservations`).flush([rows[3]]);
    http.expectOne(`${base}/orders`).flush([
      makeOrder('other-reservation', 3), makeOrder('group-final', 2), makeOrder('group-final', 3),
    ]);
    http.expectOne(`${base}/reservations/group-final/feedback`).flush(null);
    fixture.detectChanges();
    expect(component.reservations()[0].preOrder?.reservationId).toBe('group-final');
    expect(component.reservations()[0].preOrder?.status).toBe(3);
    change('submission-status-filter', 'submitted');
    expect(visibleIds()).toEqual(['group-final']);
  });

  it('filters by exact guest count', () => {
    change('guest-count-filter', '3');
    expect(visibleIds()).toEqual(['group-open', 'group-final']);
  });

  it('hides cancelled reservations only when enabled', () => {
    const toggle = (fixture.nativeElement as HTMLElement).querySelector('.filter-toggle input') as HTMLInputElement;
    toggle.click();
    fixture.detectChanges();
    expect(visibleIds()).toEqual(['single-draft', 'single-submitted', 'group-open', 'group-final']);
    toggle.click();
    fixture.detectChanges();
    expect(visibleIds()).toEqual(rows.map(row => row.id));
  });

  it('combines filters and Clear restores all reservations', () => {
    change('reservation-date-filter', '2030-11-03');
    change('submission-status-filter', 'not-submitted');
    change('guest-count-filter', '3');
    const toggle = (fixture.nativeElement as HTMLElement).querySelector('.filter-toggle input') as HTMLInputElement;
    toggle.click();
    fixture.detectChanges();
    expect(visibleIds()).toEqual(['group-open']);
    const clear = (fixture.nativeElement as HTMLElement).querySelector('.filter-clear') as HTMLButtonElement;
    clear.click();
    fixture.detectChanges();
    expect(visibleIds()).toEqual(rows.map(row => row.id));
    expect(clear.disabled).toBeTrue();
  });

  it('shows a filter-specific empty state without changing the source reservations', () => {
    change('reservation-date-filter', '2030-11-05');
    expect(visibleIds()).toEqual([]);
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('No reservations match these filters.');
    expect(component.reservations()).toEqual(rows);
  });

  it('keeps the loading and no-reservations states separate from filtered results', () => {
    component.reservationsLoaded.set(false);
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Please wait while we load your reservations.');
    component.reservations.set([]);
    component.reservationsLoaded.set(true);
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain("It looks like you don't have any reservations.");
    expect((fixture.nativeElement as HTMLElement).textContent).not.toContain('No reservations match these filters.');
  });

  it('does not claim an unavailable order status is not submitted', () => {
    component.orderStatusUnavailable.set(true);
    change('submission-status-filter', 'not-submitted');
    expect(visibleIds()).toEqual([]);
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Submission status could not be loaded.');
  });

  it('keeps existing card actions on matching reservations', () => {
    component.reservations.set([
      { ...rows[0], preOrder: { ...makeOrder('single-draft', 0), orderItems: [{
        dish: { id: 'dish-1', name: 'Salad', price: 8, imageUrl: '', state: 'Available',
          weight: '250 g', dishType: 'Appetizer', description: 'Test', calories: '0',
          proteins: '0', fats: '0', carbohydrates: '0', vitamins: '0' }, quantity: 1, state: 0,
      }] } },
      { ...rows[2], guestInviteLinkAvailable: true },
    ]);
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Submit Pre-order');
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Copy Invitation Link');
    change('guest-count-filter', '3');
    expect(visibleIds()).toEqual(['group-open']);
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Copy Invitation Link');
  });
});
