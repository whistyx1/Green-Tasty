import { HttpErrorResponse } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { Observable, Observer, of, throwError } from 'rxjs';

import { AuthenticationService } from '@app/auth/services/authentication.service';
import { Order } from '@app/models/orders/order.model';
import { Reservation, ReservationStatus } from '@app/models/reservations/reservation.model';
import { LocationsService } from '@app/services/locations/locations.service';
import { PreOrdersService } from '@app/services/pre-orders/pre-orders.service';
import { ReservationService } from '@app/services/reservation/reservation.service';
import { TableService } from '@app/services/table/table.service';
import { ToastService } from '@app/services/toast/toast.service';
import { UsersService } from '@app/services/users/users.service';
import { WaitersService } from '@app/services/waiters/waiters.service';
import { API_BASE_URL } from '@app/shared/config/api-base-url.token';

import { WaiterDashboardComponent } from './waiter-dashboard.component';

function reservation(id: string): Reservation {
  return {
    id,
    tableId: `table-${id}`,
    customerId: 'customer-1',
    startTime: new Date('2026-11-10T13:00:00Z'),
    endTime: new Date('2026-11-10T14:30:00Z'),
    guestCount: 2,
    status: ReservationStatus.Reserved,
    locationAddress: `Test restaurant ${id}`,
    tableCapacity: 4,
    waiterId: 'waiter-1',
  };
}

function order(reservationId: string): Order {
  return {
    orderId: `order-${reservationId}`,
    reservationId,
    reservationAddress: 'Test restaurant',
    reservationTime: '13:00',
    status: 1,
    orderItems: [
      { dish: { id: 'salad' } as Order['orderItems'][number]['dish'], quantity: 2 },
      { dish: { id: 'pasta' } as Order['orderItems'][number]['dish'], quantity: 1 },
    ],
  };
}

describe('WaiterDashboardComponent order loading', () => {
  let fixture: ComponentFixture<WaiterDashboardComponent>;
  let reservations: Reservation[];
  let getReservations: jasmine.Spy;
  let getOrder: jasmine.Spy;

  beforeEach(async () => {
    reservations = [];
    getReservations = jasmine.createSpy('getReservationsForUser$').and.callFake(() => of(reservations));
    getOrder = jasmine.createSpy('getByReservationId');
    await TestBed.configureTestingModule({
      imports: [WaiterDashboardComponent],
      providers: [
        provideRouter([]),
        { provide: API_BASE_URL, useValue: 'https://test.api' },
        { provide: AuthenticationService, useValue: {
          user$: of(null), isAuthorised$: of(false),
          getToken: () => 'test-token',
          getUser: () => ({ id: 'waiter-1', firstName: 'Test', lastName: 'Waiter', role: 'Waiter' }),
        } },
        { provide: ReservationService, useValue: {
          getReservationsForUser$: getReservations,
          getReservationFeedback: () => of(null),
        } },
        { provide: PreOrdersService, useValue: { getByReservationId: getOrder } },
        { provide: UsersService, useValue: { getUserName: () => of('Customer') } },
        { provide: WaitersService, useValue: { getWaiterLocationId: () => of('location-1') } },
        { provide: LocationsService, useValue: {
          getLocationById: () => of({ id: 'location-1', address: 'Test restaurant' }),
        } },
        { provide: TableService, useValue: { getAvailableTablesWithoutStartTime$: () => of([]) } },
        { provide: ToastService, useValue: { error: jasmine.createSpy('error') } },
      ],
    }).compileComponents();
  });

  async function showDashboard(): Promise<WaiterDashboardComponent> {
    fixture = TestBed.createComponent(WaiterDashboardComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    return fixture.componentInstance;
  }

  function card(): HTMLElement {
    return fixture.nativeElement.querySelector('app-card-reservation') as HTMLElement;
  }

  it('shows zero dishes for a genuine HTTP 404 without a retry action', async () => {
    reservations = [reservation('r1')];
    getOrder.and.returnValue(throwError(() => new HttpErrorResponse({ status: 404 })));

    const dashboard = await showDashboard();

    expect(dashboard.reservations()[0].preOrderLoadError).toBeFalse();
    expect(card().textContent).toContain('Pre-order: 0 dishes');
    expect(card().querySelector('[role="alert"]')).toBeNull();
    expect(card().textContent).not.toContain('Retry');
  });

  for (const status of [500, 429, 0]) {
    it(`shows a distinct error and retry for order HTTP ${status}`, async () => {
      reservations = [reservation('r1')];
      getOrder.and.returnValue(throwError(() => new HttpErrorResponse({ status })));

      const dashboard = await showDashboard();

      expect(dashboard.reservations()[0].preOrderLoadError).toBeTrue();
      expect(card().querySelector('[role="alert"]')?.textContent).toContain('Pre-order unavailable.');
      expect(card().textContent).not.toContain('Pre-order: 0 dishes');
      expect(Array.from(card().querySelectorAll('button')).map((button) => button.textContent?.trim()))
        .toContain('Retry');
    });
  }

  it('keeps an unexpected lookup error distinct from an empty pre-order', async () => {
    reservations = [reservation('r1')];
    getOrder.and.returnValue(throwError(() => new Error('Lookup failed')));

    const dashboard = await showDashboard();

    expect(dashboard.reservations()[0].preOrderLoadError).toBeTrue();
    expect(card().querySelector('[role="alert"]')?.textContent).toContain('Pre-order unavailable.');
    expect(card().textContent).not.toContain('Pre-order: 0 dishes');
  });

  it('retries only the failed order and restores the dishes and action', async () => {
    reservations = [reservation('r1'), reservation('r2')];
    let firstRequest = true;
    getOrder.and.callFake((id: string) => {
      if (id === 'r1' && firstRequest) {
        firstRequest = false;
        return throwError(() => new HttpErrorResponse({ status: 500 }));
      }
      return of(order(id));
    });
    const dashboard = await showDashboard();
    expect(dashboard.reservations()[0].preOrderLoadError).toBeTrue();

    const retryButton = Array.from(card().querySelectorAll('button'))
      .find((button) => button.textContent?.trim() === 'Retry');
    expect(retryButton).toBeTruthy();
    retryButton!.click();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(getOrder.calls.allArgs()).toEqual([['r1'], ['r2'], ['r1']]);
    expect(getReservations).toHaveBeenCalledTimes(1);
    expect(dashboard.reservations()[0].preOrderLoadError).toBeFalse();
    expect(card().textContent).toContain('Pre-order: 2 dishes');
    expect(card().textContent).not.toContain('Retry');
    expect(Array.from(card().querySelectorAll('button')).map((button) => button.textContent?.trim()))
      .toContain('Pre-order');
  });

  it('caps enrichment at three reservations and restores original card order', async () => {
    reservations = ['r1', 'r2', 'r3', 'r4', 'r5'].map(reservation);
    const pending = new Map<string, Observer<Order>>();
    let active = 0;
    let peak = 0;
    getOrder.and.callFake((id: string) => new Observable<Order>((observer) => {
      active++;
      peak = Math.max(peak, active);
      pending.set(id, observer);
      return () => { active--; };
    }));

    const dashboard = await showDashboard();
    expect(getOrder.calls.allArgs()).toEqual([['r1'], ['r2'], ['r3']]);
    expect(active).toBe(3);

    pending.get('r2')!.next(order('r2'));
    pending.get('r2')!.complete();
    expect(getOrder.calls.allArgs()).toEqual([['r1'], ['r2'], ['r3'], ['r4']]);
    pending.get('r3')!.next(order('r3'));
    pending.get('r3')!.complete();
    expect(getOrder.calls.allArgs()).toEqual([['r1'], ['r2'], ['r3'], ['r4'], ['r5']]);
    for (const id of ['r5', 'r4', 'r1']) {
      pending.get(id)!.next(order(id));
      pending.get(id)!.complete();
    }
    await fixture.whenStable();
    fixture.detectChanges();

    expect(peak).toBe(3);
    expect(dashboard.reservations().map((item) => item.id)).toEqual(['r1', 'r2', 'r3', 'r4', 'r5']);
    const cards = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('app-card-reservation'));
    expect(cards.length).toBe(5);
    cards.forEach((element, index) =>
      expect(element.textContent).toContain(`Test restaurant r${index + 1}`));
  });
});
