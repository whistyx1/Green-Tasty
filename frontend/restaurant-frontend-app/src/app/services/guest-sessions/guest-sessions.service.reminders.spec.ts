import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { API_BASE_URL } from '@app/shared/config/api-base-url.token';
import { GuestSessionsService } from './guest-sessions.service';

describe('GuestSessionsService reminder access', () => {
  let service: GuestSessionsService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), { provide: API_BASE_URL, useValue: 'https://api.test' }],
    });
    service = TestBed.inject(GuestSessionsService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('saves an optional email only with explicit consent and the guest session bearer token', () => {
    let saved: unknown;
    service.saveReminderEmail('guest-1', 'guest-token', 'guest@example.test').subscribe(value => saved = value);
    const request = http.expectOne('https://api.test/guest/sessions/guest-1/email');
    expect(request.request.method).toBe('PUT');
    expect(request.request.headers.get('Authorization')).toBe('Bearer guest-token');
    expect(request.request.body).toEqual({ email: 'guest@example.test', consent: true });
    request.flush({ email: 'guest@example.test', consent: true, revision: 1 });
    expect(saved).toEqual({ email: 'guest@example.test', consent: true, revision: 1 });
  });

  it('removes the email and reads only the current guest session notifications', () => {
    service.removeReminderEmail('guest-1', 'guest-token').subscribe();
    const deletion = http.expectOne('https://api.test/guest/sessions/guest-1/email');
    expect(deletion.request.method).toBe('DELETE');
    expect(deletion.request.headers.get('Authorization')).toBe('Bearer guest-token');
    deletion.flush({ email: null, consent: false, revision: 2 });

    let notifications: unknown;
    service.getReminderNotifications('guest-1', 'guest-token').subscribe(value => notifications = value);
    const inbox = http.expectOne('https://api.test/guest/sessions/guest-1/notifications');
    expect(inbox.request.method).toBe('GET');
    expect(inbox.request.headers.get('Authorization')).toBe('Bearer guest-token');
    inbox.flush([{ id: 'notice-1', message: 'Reminder', createdAt: '2030-01-01T00:00:00Z', isRead: false }]);
    expect(notifications).toEqual([{ id: 'notice-1', message: 'Reminder', createdAt: '2030-01-01T00:00:00Z', isRead: false }]);
  });
});
