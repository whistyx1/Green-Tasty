import { ComponentFixture, TestBed } from '@angular/core/testing';

import { TimeSlotsModalComponent } from './time-slots-modal.component';

describe('TimeSlotsModalComponent', () => {
  let component: TimeSlotsModalComponent;
  let fixture: ComponentFixture<TimeSlotsModalComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [TimeSlotsModalComponent],
    }).compileComponents();

    fixture = TestBed.createComponent(TimeSlotsModalComponent);
    component = fixture.componentInstance;
    const timeRange = { start: '2030-01-01T12:00:00Z', end: '2030-01-01T13:00:00Z' };
    fixture.componentRef.setInput('table', {
      id: 'table-1', tableNumber: 3, capacity: 4, locationId: 'location-1', timeRanges: [timeRange],
    });
    fixture.componentRef.setInput('locationName', 'Test restaurant');
    fixture.componentRef.setInput('date', '2030-01-01');
    fixture.detectChanges();
  });

  it('renders available slots for the supplied table', () => {
    expect(component).toBeTruthy();
    expect(fixture.nativeElement.querySelectorAll('app-time-slot').length).toBe(1);
    expect(fixture.nativeElement.textContent).toContain('Test restaurant, Table 3');
  });
});
