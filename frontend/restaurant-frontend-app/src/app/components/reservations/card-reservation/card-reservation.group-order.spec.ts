import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { CardReservationComponent } from './card-reservation.component';
import { API_BASE_URL } from '@app/shared/config/api-base-url.token';
import { ReservationCustomer, ReservationStatusCustomer } from '@app/models/reservations/reservation.model';
import { Roles } from '@app/shared/constants/roles';

describe('Group preorder navigation from a booking', () => {
  function create(guestCount = 2, role: string = Roles.CUSTOMER) {
    TestBed.configureTestingModule({
      imports: [CardReservationComponent],
      providers: [provideRouter([]), { provide: API_BASE_URL, useValue: 'https://api.test' }],
    });
    const booking: ReservationCustomer = {
      id: 'reservation-id', tableId: 'table-id', guestCount, waiterId: 'waiter-id',
      status: ReservationStatusCustomer.Reserved, restaurantName: 'Test', locationAddress: 'Test address',
      startTime: new Date('2030-01-01T12:00:00Z'), endTime: new Date('2030-01-01T13:00:00Z'),
    };
    const fixture = TestBed.createComponent(CardReservationComponent);
    fixture.componentRef.setInput('r', booking);
    fixture.componentRef.setInput('role', role);
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;
    const button = [...element.querySelectorAll('button')].find(item => item.textContent?.trim() === 'Group Pre-Order');
    return { fixture, button };
  }

  it('opens the selected group booking without changing its normal preorder', () => {
    const { button } = create();
    const navigate = spyOn(TestBed.inject(Router), 'navigate').and.resolveTo(true);
    button?.click();
    expect(button).toBeDefined();
    expect(navigate).toHaveBeenCalledOnceWith(['/reservations', 'reservation-id', 'group-order']);
  });

  it('does not offer a group screen for a single-person booking', () => {
    expect(create(1).button).toBeUndefined();
  });

  it('does not offer the Owner action on Waiter cards', () => {
    expect(create(2, Roles.WAITER).button).toBeUndefined();
  });
});
