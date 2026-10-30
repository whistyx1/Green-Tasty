import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { of } from 'rxjs';
import { WaiterPreOrderPageComponent } from './waiter-pre-order-page.component';
import { PreOrdersService } from '@app/services/pre-orders/pre-orders.service';
import { WaiterPreOrderState } from '@app/models/orders/order.model';

const NAVIGATION: WaiterPreOrderState = {
  reservationId: 'reservation-1', createdByName: 'Test', createdByRole: 'Customer',
  tableName: 'Table 1', startTime: '2030-11-03T08:30:00Z', endTime: '2030-11-03T10:00:00Z',
};

describe('Waiter preorder booking metadata', () => {
  let service: jasmine.SpyObj<PreOrdersService>;
  const originalHistory = history.state;

  beforeEach(async () => {
    history.replaceState({}, '');
    sessionStorage.removeItem('waiterPreOrderData');
    service = jasmine.createSpyObj<PreOrdersService>('PreOrdersService', ['getByReservationId']);
    service.getByReservationId.and.returnValue(of({
      orderId: 'order-1', reservationId: 'reservation-1', reservationAddress: '',
      reservationTime: '8:30 AM - 10:00 AM', status: 1, orderItems: [],
    }));
    await TestBed.configureTestingModule({
      imports: [WaiterPreOrderPageComponent],
      providers: [provideRouter([]), { provide: PreOrdersService, useValue: service }],
    }).compileComponents();
    spyOn(TestBed.inject(Router), 'getCurrentNavigation').and.returnValue(null);
  });

  afterEach(() => {
    history.replaceState(originalHistory, '');
    sessionStorage.removeItem('waiterPreOrderData');
  });

  async function create(data: WaiterPreOrderState, session = false) {
    if (session) sessionStorage.setItem('waiterPreOrderData', JSON.stringify(data));
    else history.replaceState(data, '');
    const fixture = TestBed.createComponent(WaiterPreOrderPageComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    return fixture;
  }

  function useNewYorkTimezone() {
    const OriginalDateTimeFormat = Intl.DateTimeFormat;
    function browserFormatter(locales?: Intl.LocalesArgument, options?: Intl.DateTimeFormatOptions) {
      return new OriginalDateTimeFormat(locales, { ...options, timeZone: 'America/New_York' });
    }
    spyOn(Intl, 'DateTimeFormat').and.callFake(browserFormatter as Intl.DateTimeFormatConstructor);
  }

  it('retains the selected table label after a history-state reload', async () => {
    const fixture = await create(NAVIGATION);
    const heading = fixture.nativeElement.querySelector('h2').textContent as string;
    expect(heading).toContain('Table 1');
    expect(heading).not.toContain('Table Table');
    expect(service.getByReservationId).toHaveBeenCalledOnceWith('reservation-1');
  });

  it('formats ISO timestamps locally across the daylight-saving boundary', async () => {
    useNewYorkTimezone();
    const fixture = await create({ ...NAVIGATION,
      startTime: '2030-11-03T05:30:00Z', endTime: '2030-11-03T06:30:00Z' });
    expect(fixture.componentInstance.reservationTime).toBe('Nov 3, 2030, 1:30 AM EDT – 1:30 AM EST');
  });

  it('includes both dates when local reservation times cross midnight', async () => {
    useNewYorkTimezone();
    const fixture = await create({ ...NAVIGATION,
      startTime: '2030-11-04T04:30:00Z', endTime: '2030-11-04T05:30:00Z' });
    expect(fixture.componentInstance.reservationTime).toBe('Nov 3, 2030, 11:30 PM EST – Nov 4, 2030, 12:30 AM EST');
  });

  it('restores the selected table and ISO timestamps from existing session storage', async () => {
    useNewYorkTimezone();
    const fixture = await create(NAVIGATION, true);
    expect(fixture.componentInstance.tableName).toBe('Table 1');
    expect(fixture.componentInstance.reservationTime).toBe('Nov 3, 2030, 3:30 AM EST – 5:00 AM EST');
  });

  it('labels legacy server times as UTC without duplicating unknown-table text', async () => {
    const fixture = await create({ ...NAVIGATION, tableName: 'Unknown table',
      startTime: undefined, endTime: undefined }, true);
    expect(fixture.componentInstance.reservationTime).toBe('8:30 AM - 10:00 AM UTC');
    expect(fixture.componentInstance.tableName).toBe('Table unavailable');
  });

  it('ignores malformed stored navigation without fetching another booking', () => {
    sessionStorage.setItem('waiterPreOrderData', '{invalid');
    const fixture = TestBed.createComponent(WaiterPreOrderPageComponent);
    expect(() => fixture.detectChanges()).not.toThrow();
    expect(service.getByReservationId).not.toHaveBeenCalled();
  });
});
