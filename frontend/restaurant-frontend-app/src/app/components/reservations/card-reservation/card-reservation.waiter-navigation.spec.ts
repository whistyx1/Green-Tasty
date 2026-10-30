import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { CardReservationComponent } from './card-reservation.component';
import { API_BASE_URL } from '@app/shared/config/api-base-url.token';
import { ReservationStatusCustomer } from '@app/models/reservations/reservation.model';
import { WaiterPreOrderState } from '@app/models/orders/order.model';

describe('Waiter preorder navigation metadata', () => {
  afterEach(() => sessionStorage.removeItem('waiterPreOrderData'));

  it('preserves the dashboard table label and original timestamps for navigation and reload', () => {
    TestBed.configureTestingModule({
      imports: [CardReservationComponent],
      providers: [provideRouter([]), { provide: API_BASE_URL, useValue: 'https://api.test' }],
    });
    const fixture = TestBed.createComponent(CardReservationComponent);
    fixture.componentRef.setInput('r', {
      id: 'reservation-1', tableId: 'table-1', guestCount: 3, waiterId: 'waiter-1',
      status: ReservationStatusCustomer.Reserved, restaurantName: 'Test', locationAddress: 'Test',
      startTime: new Date('2030-11-03T08:30:00Z'), endTime: new Date('2030-11-03T10:00:00Z'),
      preOrder: { orderItems: [{ quantity: 3 }] },
    });
    fixture.componentRef.setInput('selectedTableLabel', 'Table 1');
    const navigate = spyOn(TestBed.inject(Router), 'navigate').and.resolveTo(true);
    fixture.componentInstance.onPreOrderWaiter();
    const stored = JSON.parse(sessionStorage.getItem('waiterPreOrderData') ?? '{}') as WaiterPreOrderState;
    expect(stored.tableName).toBe('Table 1');
    expect(stored.startTime).toBe('2030-11-03T08:30:00.000Z');
    expect(stored.endTime).toBe('2030-11-03T10:00:00.000Z');
    expect(navigate).toHaveBeenCalledOnceWith(['/waiter-pre-order'], { state: stored });
  });
});
