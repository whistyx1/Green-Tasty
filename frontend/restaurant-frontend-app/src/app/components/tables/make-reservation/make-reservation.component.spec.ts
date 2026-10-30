import { ComponentFixture, TestBed } from '@angular/core/testing';

import { MakeReservationComponent } from './make-reservation.component';

describe('MakeReservationComponent', () => {
  let component: MakeReservationComponent;
  let fixture: ComponentFixture<MakeReservationComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [MakeReservationComponent],
    }).compileComponents();

    fixture = TestBed.createComponent(MakeReservationComponent);
    component = fixture.componentInstance;
    const timeRange = { start: '2030-01-01T12:00:00Z', end: '2030-01-01T13:00:00Z' };
    fixture.componentRef.setInput('table', {
      id: 'table-1', tableNumber: 3, capacity: 4, locationId: 'location-1', timeRanges: [timeRange],
    });
    fixture.componentRef.setInput('locationName', 'Test restaurant');
    fixture.componentRef.setInput('selectedTimeRange', timeRange);
    fixture.detectChanges();
  });

  it('renders the selected table and guest capacity', () => {
    expect(component).toBeTruthy();
    expect(fixture.nativeElement.textContent).toContain('Test restaurant, Table 3');
    expect(fixture.nativeElement.textContent).toContain('Table seating capacity: 4 people');
  });
});
