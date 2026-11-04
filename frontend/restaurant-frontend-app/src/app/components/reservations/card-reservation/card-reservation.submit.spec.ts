import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { CardReservationComponent } from './card-reservation.component';
import { ReservationCustomer, ReservationStatusCustomer } from '@app/models/reservations/reservation.model';
import { Order } from '@app/models/orders/order.model';
import { Roles } from '@app/shared/constants/roles';

describe('CardReservationComponent single pre-order submission', () => {
  const order: Order = {
    orderId: 'order-1', reservationId: 'reservation-1', reservationAddress: 'Test',
    reservationTime: '2030-11-03', status: 0,
    orderItems: [{ dish: {
      id: 'dish-1', name: 'Salad', price: 8, imageUrl: '', state: 'Available',
      weight: '250 g', dishType: 'Appetizer', description: 'Test', calories: '0',
      proteins: '0', fats: '0', carbohydrates: '0', vitamins: '0',
    }, quantity: 2, state: 0 }],
  };
  const reservation: ReservationCustomer = {
    id: 'reservation-1', tableId: 'table-1', customerId: 'owner-1', waiterId: 'waiter-1',
    startTime: new Date('2030-11-03T12:00:00Z'), endTime: new Date('2030-11-03T13:30:00Z'),
    guestCount: 1, status: ReservationStatusCustomer.Reserved,
    restaurantName: 'Test', locationAddress: 'Test', preOrder: order,
  };

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [CardReservationComponent], providers: [provideRouter([])] });
  });

  function create(overrides: Partial<ReservationCustomer> = {}, role = Roles.CUSTOMER) {
    const fixture = TestBed.createComponent(CardReservationComponent);
    fixture.componentRef.setInput('r', { ...reservation, ...overrides });
    fixture.componentRef.setInput('ownerUserId', 'owner-1');
    fixture.componentRef.setInput('role', role);
    fixture.detectChanges();
    const button = [...(fixture.nativeElement as HTMLElement).querySelectorAll('button')]
      .find(item => item.textContent?.trim() === 'Submit Pre-order');
    return { fixture, button };
  }

  it('offers submission only for an eligible single guest reservation and emits its ID', () => {
    const { fixture, button } = create();
    const emit = spyOn(fixture.componentInstance.submitSinglePreOrder, 'emit');
    expect(button).toBeDefined();
    expect(button?.disabled).toBeFalse();
    button?.click();
    expect(emit).toHaveBeenCalledOnceWith('reservation-1');
  });

  it('excludes groups, non-owners, waiters, cancelled, expired, and submitted orders', () => {
    expect(create({ guestCount: 2 }).button).toBeUndefined();
    expect(create({ customerId: 'someone-else' }).button).toBeUndefined();
    expect(create({}, Roles.WAITER).button).toBeUndefined();
    expect(create({ status: ReservationStatusCustomer.Cancelled }).button).toBeUndefined();
    expect(create({ startTime: new Date('2020-01-01T12:00:00Z') }).button).toBeUndefined();
    expect(create({ preOrder: { ...order, status: 1 } }).button).toBeUndefined();
    expect(create({ preOrder: { ...order, status: 3 } }).button).toBeUndefined();
  });

  it('hides submission inside the 30-minute editing cutoff', () => {
    expect(create({ startTime: new Date(Date.now() + 29 * 60_000) }).button).toBeUndefined();
    expect(create({ startTime: new Date(Date.now() + 31 * 60_000) }).button).toBeDefined();
  });

  it('explains an empty cart without relying on hover and does not emit', () => {
    const { fixture, button } = create({ preOrder: { ...order, orderItems: [] } });
    const emit = spyOn(fixture.componentInstance.submitSinglePreOrder, 'emit');
    expect(button?.disabled).toBeTrue();
    const reason = (fixture.nativeElement as HTMLElement).querySelector('#submit-reason-reservation-1');
    expect(reason?.textContent).toContain('Add dishes');
    expect(button?.getAttribute('aria-describedby')).toBe(reason?.id);
    fixture.componentInstance.onSubmitSinglePreOrder();
    expect(emit).not.toHaveBeenCalled();
  });
});
