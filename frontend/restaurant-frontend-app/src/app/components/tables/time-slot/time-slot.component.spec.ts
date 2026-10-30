import { ComponentFixture, TestBed } from '@angular/core/testing';

import { TimeSlotComponent } from './time-slot.component';

describe('TimeSlotComponent', () => {
  let component: TimeSlotComponent;
  let fixture: ComponentFixture<TimeSlotComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [TimeSlotComponent],
    }).compileComponents();

    fixture = TestBed.createComponent(TimeSlotComponent);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('timeRange', { start: '2030-01-01T12:00:00Z', end: '2030-01-01T13:00:00Z' });
    fixture.componentRef.setInput('label', 'Lunch slot');
    fixture.detectChanges();
  });

  it('renders the supplied time slot label', () => {
    expect(component).toBeTruthy();
    expect(fixture.nativeElement.querySelector('.body-bold').textContent).toContain('Lunch slot');
  });
});
