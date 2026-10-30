import { HttpClient, HttpContext, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { AuthenticationService } from './services/authentication.service';
import { SKIP_ERROR_TOAST } from './skip-error-toast.context';
import { tokenInterceptor } from './token.interceptor';
import { ToastService } from '@app/services/toast/toast.service';

describe('Owner group and guest token isolation', () => {
  let http: HttpClient;
  let requests: HttpTestingController;
  let toast: jasmine.SpyObj<ToastService>;

  beforeEach(() => {
    toast = jasmine.createSpyObj<ToastService>('ToastService', ['error']);
    const auth = jasmine.createSpyObj<AuthenticationService>('AuthenticationService', ['getTokens']);
    auth.getTokens.and.returnValue({ idToken: 'cognito-test-token', accessToken: 'access-test-token' });
    TestBed.configureTestingModule({ providers: [
      provideHttpClient(withInterceptors([tokenInterceptor])), provideHttpClientTesting(),
      { provide: AuthenticationService, useValue: auth }, { provide: ToastService, useValue: toast },
    ] });
    http = TestBed.inject(HttpClient);
    requests = TestBed.inject(HttpTestingController);
  });

  afterEach(() => requests.verify());

  for (const suffix of ['/sessions', '/status', '/sessions/guest-id']) {
    it(`attaches Cognito to Owner group ${suffix}`, () => {
      const url = `https://api.test/develop/guest/groups/group-id${suffix}`;
      const method = suffix.endsWith('guest-id') ? 'DELETE' : 'GET';
      http.request(method, url).subscribe();
      const request = requests.expectOne(url);
      expect(request.request.headers.get('Authorization')).toBe('Bearer cognito-test-token');
      request.flush({});
    });
  }

  it('preserves the explicit guest token and cart body', () => {
    const url = 'https://api.test/guest/sessions/guest-id/dishes';
    const body = { dishId: 'salad', quantity: 2 };
    http.post(url, body, { headers: { Authorization: 'Bearer guest-test-token' } }).subscribe();
    const request = requests.expectOne(url);
    expect(request.request.headers.get('Authorization')).toBe('Bearer guest-test-token');
    expect(request.request.body).toEqual(body);
    request.flush({});
  });

  it('does not attach a Customer token when creating an anonymous guest session', () => {
    http.post('https://api.test/guest/sessions', { groupReservationId: 'group-id', guestName: 'Test' }).subscribe();
    const request = requests.expectOne('https://api.test/guest/sessions');
    expect(request.request.headers.has('Authorization')).toBeFalse();
    request.flush({});
  });

  for (const path of ['/guest/groups/group-id/sessions', '/guest/groups/group-id/status', '/guest/sessions/guest-id/menu']) {
    it(`keeps silent failure quiet and propagates status for ${path}`, () => {
      let status = 0;
      http.get(`https://api.test${path}`, { context: new HttpContext().set(SKIP_ERROR_TOAST, true) })
        .subscribe({ error: error => { status = error.status; } });
      requests.expectOne(`https://api.test${path}`).flush({ error: 'Unavailable' }, { status: 500, statusText: 'Error' });
      expect(status).toBe(500);
      expect(toast.error).not.toHaveBeenCalled();
    });
  }

  it('shows the API submit error once', () => {
    http.post('https://api.test/group-reservations/group-id/submit', { reservationId: 'reservation-id' })
      .subscribe({ error: () => undefined });
    requests.expectOne('https://api.test/group-reservations/group-id/submit')
      .flush({ error: 'No order found for this reservation.' }, { status: 400, statusText: 'Bad Request' });
    expect(toast.error).toHaveBeenCalledOnceWith('No order found for this reservation.', 'Error');
  });
});
