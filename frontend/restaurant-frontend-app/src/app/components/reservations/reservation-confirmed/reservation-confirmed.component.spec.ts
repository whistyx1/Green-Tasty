import { ReservationStatus } from '@app/models/reservations/reservation.model';
import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ReservationConfirmedComponent } from './reservation-confirmed.component';

describe('ReservationConfirmedComponent', () => {
  let component: ReservationConfirmedComponent;
  let fixture: ComponentFixture<ReservationConfirmedComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ReservationConfirmedComponent],
    }).compileComponents();

    fixture = TestBed.createComponent(ReservationConfirmedComponent);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('reservation', {
      id: 'reservation-1', tableId: 'table-1', customerId: 'customer-1',
      startTime: new Date('2030-01-01T12:00:00Z'), endTime: new Date('2030-01-01T13:00:00Z'),
      guestCount: 2, status: ReservationStatus.Reserved, waiterId: 'waiter-1',
      locationAddress: 'Test address', restaurantName: 'Test restaurant', tableCapacity: 4,
    });
    fixture.detectChanges();
  });

  it('renders confirmation details for the supplied reservation', () => {
    expect(component).toBeTruthy();
    expect(fixture.nativeElement.querySelector('[role=dialog]').textContent).toContain('Test restaurant');
    expect(fixture.nativeElement.textContent).toContain('2 people');
  });
});
