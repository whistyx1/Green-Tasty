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

describe('ReservationDashboardComponent direct submission', () => {
  const base = 'https://test.api';
  const dish = {
    id: 'dish-1', name: 'Salad', price: 8, imageUrl: '', state: 'Available',
    weight: '250 g', dishType: 'Appetizer', description: 'Test', calories: '0',
    proteins: '0', fats: '0', carbohydrates: '0', vitamins: '0',
  };
  const reservation = (id: string): ReservationCustomer => ({
    id, tableId: 'table-1', customerId: 'owner-1', waiterId: 'waiter-1',
    startTime: new Date('2030-11-03T12:00:00Z'), endTime: new Date('2030-11-03T13:30:00Z'),
    guestCount: 1, status: ReservationStatusCustomer.Reserved,
    restaurantName: 'Test', locationAddress: 'Test', preOrder: order(id),
  });
  const order = (reservationId: string): Order => ({
    orderId: `order-${reservationId}`, reservationId, status: 0,
    reservationAddress: 'Test', reservationTime: '2030-11-03',
    orderItems: [{ dish, quantity: 2, state: 0 }],
  });
  let fixture: ReturnType<typeof TestBed.createComponent<ReservationDashboardComponent>>;
  let component: ReservationDashboardComponent;
  let http: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ReservationDashboardComponent],
      providers: [
        provideRouter([]), provideHttpClient(), provideHttpClientTesting(),
        { provide: API_BASE_URL, useValue: base },
        { provide: AuthenticationService, useValue: {
          user$: of(null), isAuthorised$: of(false), getUser: () => null, getToken: () => null,
        } },
      ],
    }).compileComponents();
    spyOn(TestBed.inject(Router), 'navigate').and.resolveTo(true);
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(ReservationDashboardComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
    component.customerId.set('owner-1');
    component.reservations.set([reservation('reservation-1'), reservation('reservation-2')]);
    component.reservationsLoaded.set(true);
    fixture.detectChanges();
  });

  afterEach(() => http.verify());

  function openSecond(): void {
    const cards = (fixture.nativeElement as HTMLElement).querySelectorAll('.card');
    const button = [...cards[1].querySelectorAll('button')]
      .find(item => item.textContent?.trim() === 'Submit Pre-order');
    expect(button).toBeDefined();
    button?.click();
    http.expectOne(`${base}/orders/reservation-2`).flush(order('reservation-2'));
    fixture.detectChanges();
  }

  function flushRefresh(): void {
    http.expectOne(`${base}/reservations`).flush([reservation('reservation-1'), reservation('reservation-2')]);
    http.expectOne(`${base}/orders`).flush([order('reservation-1'), { ...order('reservation-2'), status: 3 }]);
    http.expectOne(`${base}/reservations/reservation-1/feedback`).flush(null);
    http.expectOne(`${base}/reservations/reservation-2/feedback`).flush(null);
    fixture.detectChanges();
  }

  it('shows the selected order in F6 confirmation and Cancel sends nothing', () => {
    openSecond();
    const dialog = (fixture.nativeElement as HTMLElement).querySelector('[role="dialog"]');
    expect(dialog?.textContent).toContain('Salad × 2');
    expect(dialog?.textContent).toContain('$16.00');
    expect(dialog?.textContent).toContain('unsubmit and edit');
    const cancel = [...dialog!.querySelectorAll('button')]
      .find(item => item.textContent?.trim() === 'Cancel');
    cancel?.click();
    fixture.detectChanges();
    expect(component.submissionOrder()).toBeNull();
    http.expectNone(request => request.method === 'POST' && request.url.includes('/orders/'));
  });

  it('submits only the selected reservation once and refreshes its card', () => {
    openSecond();
    component.confirmSubmission();
    component.confirmSubmission();
    http.expectOne(`${base}/reservations/reservation-2`).flush(reservation('reservation-2'));
    http.expectOne(`${base}/orders/reservation-2`).flush(order('reservation-2'));
    http.expectNone(`${base}/orders/reservation-1/submit`);
    http.expectOne(`${base}/orders/reservation-2/submit`).flush({ message: 'Submitted' });
    expect(component.reservations()[1].preOrder?.status).toBe(3);
    flushRefresh();
    expect(component.reservations()[1].hasSubmittedPreOrder).toBeTrue();
    expect(component.reservations()[0].preOrder?.status).toBe(0);
    const cards = (fixture.nativeElement as HTMLElement).querySelectorAll('.card');
    expect(cards[1].textContent).not.toContain('Submit Pre-order');
    expect(cards[0].textContent).toContain('Submit Pre-order');
  });

  it('rejects an order that changed before confirmation without submitting', () => {
    openSecond();
    component.confirmSubmission();
    http.expectOne(`${base}/reservations/reservation-2`).flush(reservation('reservation-2'));
    http.expectOne(`${base}/orders/reservation-2`).flush({
      ...order('reservation-2'), orderItems: [{ dish, quantity: 3 }],
    });
    http.expectNone(`${base}/orders/reservation-2/submit`);
    expect(component.submissionOrder()).toBeNull();
    expect(component.submissionError()).toContain('no longer ready');
    flushRefresh();
  });

  it('rejects a mismatched order before opening confirmation', () => {
    component.openSubmission('reservation-2');
    http.expectOne(`${base}/orders/reservation-2`).flush(order('reservation-1'));
    expect(component.submissionOrder()).toBeNull();
    expect(component.submissionError()).toContain('no longer ready');
    flushRefresh();
  });

  it('rejects an order already submitted elsewhere before opening confirmation', () => {
    component.openSubmission('reservation-2');
    http.expectOne(`${base}/orders/reservation-2`).flush({ ...order('reservation-2'), status: 3 });
    expect(component.submissionOrder()).toBeNull();
    http.expectNone(`${base}/orders/reservation-2/submit`);
    flushRefresh();
  });

  it('handles an invalid order response without opening confirmation', () => {
    component.openSubmission('reservation-2');
    http.expectOne(`${base}/orders/reservation-2`).flush({
      ...order('reservation-2'), orderItems: [{ quantity: 2 }],
    });
    expect(component.submissionOrder()).toBeNull();
    expect(component.submissionError()).toContain('no longer ready');
    flushRefresh();
  });
});
