import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { CardReservationComponent } from './card-reservation.component';
import { ReservationCustomer, ReservationStatusCustomer } from '@app/models/reservations/reservation.model';
import { API_BASE_URL } from '@app/shared/config/api-base-url.token';
import { ToastService } from '@app/services/toast/toast.service';
import { Roles } from '@app/shared/constants/roles';

describe('CardReservationComponent invitation link', () => {
  const future = new Date('2030-11-03T10:00:00Z');
  const booking: ReservationCustomer = {
    id: 'reservation-1', tableId: 'table-1', guestCount: 3, waiterId: 'waiter-1',
    customerId: 'owner-user', guestInviteLinkAvailable: true,
    status: ReservationStatusCustomer.Reserved, restaurantName: 'Test', locationAddress: 'Test',
    startTime: new Date('2030-11-03T08:30:00Z'), endTime: future,
  };
  let http: HttpTestingController;
  let writeText: jasmine.Spy;
  let previousClipboard: PropertyDescriptor | undefined;
  const toast = jasmine.createSpyObj<ToastService>('ToastService', ['success', 'error']);

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [CardReservationComponent],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting(),
        { provide: API_BASE_URL, useValue: 'https://test.api' },
        { provide: ToastService, useValue: toast }],
    });
    http = TestBed.inject(HttpTestingController);
    writeText = jasmine.createSpy('writeText').and.resolveTo();
    previousClipboard = Object.getOwnPropertyDescriptor(navigator, 'clipboard');
    Object.defineProperty(navigator, 'clipboard', { configurable: true, value: { writeText } });
  });

  afterEach(() => {
    http.verify();
    if (previousClipboard) Object.defineProperty(navigator, 'clipboard', previousClipboard);
    else delete (navigator as { clipboard?: Clipboard }).clipboard;
    toast.success.calls.reset();
    toast.error.calls.reset();
  });

  function create(overrides: Partial<ReservationCustomer> = {}, ownerId = 'owner-user', role = Roles.CUSTOMER) {
    const fixture = TestBed.createComponent(CardReservationComponent);
    fixture.componentRef.setInput('r', { ...booking, ...overrides });
    fixture.componentRef.setInput('role', role);
    fixture.componentRef.setInput('ownerUserId', ownerId);
    fixture.detectChanges();
    const button = [...(fixture.nativeElement as HTMLElement).querySelectorAll('button')]
      .find(item => item.textContent?.trim() === 'Copy Invitation Link');
    return { fixture, button };
  }

  it('copies the existing token link and retrieves the same token after a new card loads', async () => {
    const first = create();
    first.button?.click();
    http.expectOne('https://test.api/reservations/reservation-1/invite-token')
      .flush({ token: 'existing-token' });
    await Promise.resolve();
    await Promise.resolve();
    expect(writeText).toHaveBeenCalledOnceWith(`${window.location.origin}/join/existing-token`);
    first.fixture.destroy();

    writeText.calls.reset();
    const reloaded = create();
    reloaded.button?.click();
    http.expectOne('https://test.api/reservations/reservation-1/invite-token')
      .flush({ token: 'existing-token' });
    await Promise.resolve();
    await Promise.resolve();
    expect(writeText).toHaveBeenCalledOnceWith(`${window.location.origin}/join/existing-token`);
    expect(toast.success).toHaveBeenCalledTimes(2);
    reloaded.fixture.destroy();
  });

  it('hides the action from non-owners, waiters, and ordinary reservations', () => {
    expect(create({}, 'other-user').button).toBeUndefined();
    expect(create({}, 'owner-user', Roles.WAITER).button).toBeUndefined();
    expect(create({ guestCount: 1 }).button).toBeUndefined();
  });

  it('disables submitted groups with an accessible explanation', () => {
    const { fixture, button } = create({ guestInviteLinkAvailable: false });
    expect(button?.disabled).toBeTrue();
    const reason = (fixture.nativeElement as HTMLElement).querySelector('#invite-reason-reservation-1');
    expect(reason?.textContent).toContain('submitted or closed');
    expect(button?.getAttribute('aria-describedby')).toBe(reason?.id);
    button?.click();
    http.expectNone('https://test.api/reservations/reservation-1/invite-token');
  });

  it('disables expired and cancelled reservations', () => {
    const expired = create({ endTime: new Date('2020-01-01T00:00:00Z') });
    expect(expired.button?.disabled).toBeTrue();
    expect((expired.fixture.nativeElement as HTMLElement).textContent).toContain('reservation has ended');
    expired.fixture.destroy();

    const cancelled = create({ status: ReservationStatusCustomer.Cancelled });
    expect(cancelled.button?.disabled).toBeTrue();
    expect((cancelled.fixture.nativeElement as HTMLElement).textContent).toContain('no longer active');
  });

  it('does not issue two token requests for repeated clicks', async () => {
    const { fixture, button } = create();
    button?.click();
    button?.click();
    const requests = http.match('https://test.api/reservations/reservation-1/invite-token');
    expect(requests.length).toBe(1);
    requests[0].flush({ token: 'existing-token' });
    await Promise.resolve();
    await Promise.resolve();
    fixture.destroy();
  });
});
