import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { fakeAsync, TestBed, tick } from '@angular/core/testing';
import { API_BASE_URL } from '@app/shared/config/api-base-url.token';
import { GuestPreorderStore } from './guest-preorder.store';

describe('GuestPreorderStore reminder contact and inbox', () => {
  let store: GuestPreorderStore;
  let http: HttpTestingController;
  const prefix = 'guestPreorder:invite:';

  beforeEach(() => {
    localStorage.setItem(`${prefix}guestSessionId`, 'guest-1');
    localStorage.setItem(`${prefix}guestSessionToken`, 'guest-token');
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), { provide: API_BASE_URL, useValue: 'https://api.test' }],
    });
    store = TestBed.inject(GuestPreorderStore);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    http.verify();
    for (const suffix of ['guestSessionId', 'guestSessionToken', 'guestName', 'cart', 'status'])
      localStorage.removeItem(`${prefix}${suffix}`);
  });

  function open(email: string | null = null): void {
    store.initContext('invite');
    http.expectOne('https://api.test/guest/invites/invite').flush({ GroupReservationId: 'group' });
    http.expectOne('https://api.test/guest/sessions/guest-1/menu').flush({ dishes: {} });
    tick();
    http.expectOne('https://api.test/guest/sessions/guest-1').flush({
      name: 'Guest', items: [], total: 0, isSubmitted: false,
      orderingPreference: 'Online', revision: 0, email, emailConsent: !!email,
    });
    tick();
  }

  it('hydrates an existing reminder email after reload without persisting it in local storage', fakeAsync(() => {
    open('guest@example.test');
    expect(store.reminderEmail()).toBe('guest@example.test');
    expect(store.emailInput()).toBe('guest@example.test');
    expect(store.emailConsentInput()).toBeTrue();
    expect(Object.keys(localStorage).filter(key => key.startsWith(prefix) && key.toLowerCase().includes('email'))).toEqual([]);
  }));

  it('requires explicit consent and preserves a draft while the session is polled', fakeAsync(() => {
    open();
    store.editReminderEmail('guest@example.test');
    void store.saveReminderEmail();
    tick();
    expect(store.emailError()).toContain('agree');
    http.expectNone('https://api.test/guest/sessions/guest-1/email');

    void store.checkSession();
    http.expectOne('https://api.test/guest/sessions/guest-1').flush({
      name: 'Guest', items: [], total: 0, isSubmitted: false, email: null, emailConsent: false,
    });
    tick();
    expect(store.emailInput()).toBe('guest@example.test');
  }));

  it('updates and removes email only after successful server responses', fakeAsync(() => {
    open();
    store.editReminderEmail('guest@example.test');
    store.editReminderConsent(true);
    void store.saveReminderEmail();
    const saving = http.expectOne('https://api.test/guest/sessions/guest-1/email');
    expect(saving.request.body).toEqual({ email: 'guest@example.test', consent: true });
    saving.flush({ email: 'guest@example.test', consent: true, revision: 1 });
    tick();
    expect(store.reminderEmail()).toBe('guest@example.test');
    expect(store.revision()).toBe(1);

    void store.removeReminderEmail();
    const removing = http.expectOne('https://api.test/guest/sessions/guest-1/email');
    expect(removing.request.method).toBe('DELETE');
    removing.flush({ email: null, consent: false, revision: 2 });
    tick();
    expect(store.reminderEmail()).toBeNull();
    expect(store.revision()).toBe(2);
    expect(store.emailConsentInput()).toBeFalse();
    expect(store.emailInput()).toBe('');

    void store.setPreference('InRestaurant');
    const preference = http.expectOne('https://api.test/guest/sessions/guest-1/ordering-preference');
    expect(preference.request.body).toEqual({ preference: 'InRestaurant', expectedRevision: 2 });
    preference.flush({ name: 'Guest', items: [], total: 0, isSubmitted: false,
      orderingPreference: 'InRestaurant', revision: 3, email: null, emailConsent: false });
    tick();
    expect(store.orderingPreference()).toBe('InRestaurant');
  }));

  it('loads persistent notifications and does not replace them after a failed refresh', fakeAsync(() => {
    open();
    void store.loadReminderNotifications();
    const first = http.expectOne('https://api.test/guest/sessions/guest-1/notifications');
    expect(first.request.headers.get('Authorization')).toBe('Bearer guest-token');
    first.flush([{ id: 'notice-1', message: 'Submit your pre-order', createdAt: '2030-01-01T00:00:00Z', isRead: false }]);
    tick();
    expect(store.notifications().map(item => item.id)).toEqual(['notice-1']);

    void store.loadReminderNotifications();
    http.expectOne('https://api.test/guest/sessions/guest-1/notifications')
      .flush({ error: 'temporary' }, { status: 500, statusText: 'Server Error' });
    tick();
    expect(store.notificationsError()).toContain('Could not load');
    expect(store.notifications().map(item => item.id)).toEqual(['notice-1']);
  }));

  it('keeps email writes separate from cart and preference mutations', fakeAsync(() => {
    open();
    store.editReminderEmail('guest@example.test');
    store.editReminderConsent(true);
    store.cartItems.set([{ dishId: 'salad', title: 'Salad', price: 8, qty: 1 }]);
    void store.saveReminderEmail();
    const saving = http.expectOne('https://api.test/guest/sessions/guest-1/email');

    void store.addDish({ dishId: 'pasta', title: 'Pasta', price: 12 });
    void store.setPreference('InRestaurant');
    void store.submitPreorder();
    tick();
    http.expectNone('https://api.test/guest/sessions/guest-1/dishes');
    http.expectNone('https://api.test/guest/sessions/guest-1/ordering-preference');
    http.expectNone('https://api.test/guest/sessions/guest-1/submit');

    saving.flush({ email: 'guest@example.test', consent: true, revision: 1 });
    tick();
    expect(store.revision()).toBe(1);
    expect(store.emailBusy()).toBeFalse();

    store.cartBusy.set(true);
    void store.removeReminderEmail();
    tick();
    http.expectNone('https://api.test/guest/sessions/guest-1/email');
    expect(store.reminderEmail()).toBe('guest@example.test');
  }));
});
