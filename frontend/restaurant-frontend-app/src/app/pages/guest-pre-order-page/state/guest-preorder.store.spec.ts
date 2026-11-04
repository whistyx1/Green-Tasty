import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { fakeAsync, TestBed, tick } from '@angular/core/testing';
import { API_BASE_URL } from '@app/shared/config/api-base-url.token';
import { GuestPreorderStore } from './guest-preorder.store';

describe('GuestPreorderStore removed session', () => {
  let store: GuestPreorderStore;
  let http: HttpTestingController;
  const invite = 'owned-invite';
  const prefix = `guestPreorder:${invite}:`;

  beforeEach(() => {
    localStorage.setItem(`${prefix}guestName`, 'Guest');
    localStorage.setItem(`${prefix}guestSessionId`, 'removed-session');
    localStorage.setItem(`${prefix}guestSessionToken`, 'old-token');
    localStorage.setItem(`${prefix}cart`, JSON.stringify([{ dishId: 'salad', title: 'Salad', price: 8, qty: 1 }]));
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(), provideHttpClientTesting(),
        { provide: API_BASE_URL, useValue: 'https://test.api' },
      ],
    });
    store = TestBed.inject(GuestPreorderStore);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    http.verify();
    for (const name of ['guestName', 'guestSessionId', 'guestSessionToken', 'cart', 'status'])
      localStorage.removeItem(`${prefix}${name}`);
  });

  function openVerifiedSession(): void {
    store.initContext(invite);
    http.expectOne('https://test.api/guest/invites/owned-invite').flush({ GroupReservationId: 'group' });
    http.expectOne('https://test.api/guest/sessions/removed-session/menu')
      .flush({ dishes: { MainCourse: [{ id: 'salad', name: 'Salad', price: 8 }] } });
    tick();
    http.expectOne('https://test.api/guest/sessions/removed-session').flush({
      name: 'Guest', items: [{ dish: { id: 'salad', name: 'Salad', price: 8 }, quantity: 1 }],
      total: 8, isSubmitted: false, orderingPreference: 'Online', revision: 0,
    });
    tick();
    expect(store.status()).toBe('active');
    expect(store.cartItems().length).toBe(1);
  }

  it('closes the old cart and clears saved credentials when polling detects removal', fakeAsync(() => {
    openVerifiedSession();

    void store.checkSession();
    const request = http.expectOne('https://test.api/guest/sessions/removed-session');
    expect(request.request.headers.get('Authorization')).toBe('Bearer old-token');
    request.flush({ error: 'removed' }, { status: 401, statusText: 'Unauthorized' });
    tick();

    expect(store.status()).toBe('inactive');
    expect(store.guestSessionId()).toBeNull();
    expect(store.guestSessionToken()).toBeNull();
    expect(store.cartItems()).toEqual([]);
    expect(store.hasMenu()).toBeFalse();
    expect(localStorage.getItem(`${prefix}guestSessionToken`)).toBeNull();
  }));

  it('closes a restored session when its first menu request is rejected', fakeAsync(() => {
    store.initContext(invite);
    http.expectOne('https://test.api/guest/invites/owned-invite').flush({ GroupReservationId: 'group' });
    http.expectOne('https://test.api/guest/sessions/removed-session/menu')
      .flush({ error: 'removed' }, { status: 401, statusText: 'Unauthorized' });
    tick();

    expect(store.status()).toBe('inactive');
    expect(store.guestSessionId()).toBeNull();
    expect(store.cartItems()).toEqual([]);
    expect(localStorage.getItem(`${prefix}guestSessionToken`)).toBeNull();
  }));

  it('keeps the removed guest inactive after reloading the same invitation', fakeAsync(() => {
    localStorage.setItem(`${prefix}status`, 'inactive');
    store.initContext(invite);
    http.expectOne('https://test.api/guest/invites/owned-invite').flush({ GroupReservationId: 'group' });
    tick();

    expect(store.status()).toBe('inactive');
    expect(store.guestSessionId()).toBeNull();
    expect(store.hasMenu()).toBeFalse();
    expect(localStorage.getItem(`${prefix}guestSessionToken`)).toBeNull();
    http.expectNone('https://test.api/guest/sessions/removed-session/menu');
  }));

  it('does not add a dish locally when the removed session is rejected', fakeAsync(() => {
    openVerifiedSession();

    void store.addDish({ dishId: 'pasta', title: 'Pasta', price: 12 });
    http.expectOne('https://test.api/guest/sessions/removed-session/dishes')
      .flush({ error: 'removed' }, { status: 401, statusText: 'Unauthorized' });
    tick();

    expect(store.status()).toBe('inactive');
    expect(store.cartItems()).toEqual([]);
    expect(localStorage.getItem(`${prefix}guestSessionId`)).toBeNull();
  }));

  it('keeps an active cart after a temporary check failure', fakeAsync(() => {
    openVerifiedSession();

    void store.checkSession();
    http.expectOne('https://test.api/guest/sessions/removed-session')
      .flush({ error: 'temporary' }, { status: 500, statusText: 'Server Error' });
    tick();

    expect(store.status()).toBe('active');
    expect(store.cartItems().length).toBe(1);
  }));

  it('preserves a draft and uses the new revision when switching ordering preference', fakeAsync(() => {
    openVerifiedSession();

    void store.addDish({ dishId: 'pasta', title: 'Pasta', price: 12 });
    http.expectOne('https://test.api/guest/sessions/removed-session/dishes').flush({});
    tick();
    expect(store.revision()).toBe(1);
    expect(store.cartItems().length).toBe(2);

    void store.setPreference('InRestaurant');
    const preference = http.expectOne('https://test.api/guest/sessions/removed-session/ordering-preference');
    expect(preference.request.method).toBe('PATCH');
    expect(preference.request.body).toEqual({ preference: 'InRestaurant', expectedRevision: 1 });
    preference.flush({
      name: 'Guest', items: [
        { dish: { id: 'salad', name: 'Salad', price: 8 }, quantity: 1 },
        { dish: { id: 'pasta', name: 'Pasta', price: 12 }, quantity: 1 },
      ], total: 20, isSubmitted: false, orderingPreference: 'InRestaurant', revision: 2,
    });
    tick();
    expect(store.orderingPreference()).toBe('InRestaurant');
    expect(store.cartItems().map(item => item.dishId)).toEqual(['salad', 'pasta']);
    expect(store.revision()).toBe(2);

    void store.addDish({ dishId: 'soup', title: 'Soup', price: 5 });
    tick();
    http.expectNone('https://test.api/guest/sessions/removed-session/dishes');

    void store.setPreference('Online');
    const restore = http.expectOne('https://test.api/guest/sessions/removed-session/ordering-preference');
    expect(restore.request.body).toEqual({ preference: 'Online', expectedRevision: 2 });
    restore.flush({
      name: 'Guest', items: [
        { dish: { id: 'salad', name: 'Salad', price: 8 }, quantity: 1 },
        { dish: { id: 'pasta', name: 'Pasta', price: 12 }, quantity: 1 },
      ], total: 20, isSubmitted: false, orderingPreference: 'Online', revision: 3,
    });
    tick();
    expect(store.orderingPreference()).toBe('Online');
    expect(store.cartItems().map(item => item.dishId)).toEqual(['salad', 'pasta']);
  }));

  it('closes the submitted success screen when the Owner removes its session', fakeAsync(() => {
    openVerifiedSession();
    store.submitSuccess();
    expect(store.status()).toBe('submitted');

    void store.checkSession();
    http.expectOne('https://test.api/guest/sessions/removed-session')
      .flush({ error: 'removed' }, { status: 401, statusText: 'Unauthorized' });
    tick();

    expect(store.status()).toBe('inactive');
    expect(store.guestSessionId()).toBeNull();
    expect(store.cartItems()).toEqual([]);
  }));

  it('checks the submitted session before creating another pre-order', fakeAsync(() => {
    openVerifiedSession();
    store.submitSuccess();

    void store.createNewPreorder();
    http.expectOne('https://test.api/guest/sessions/removed-session')
      .flush({ error: 'removed' }, { status: 401, statusText: 'Unauthorized' });
    tick();

    http.expectNone('https://test.api/guest/sessions');
    expect(store.status()).toBe('inactive');
  }));

  it('still creates a new pre-order when the submitted session exists', fakeAsync(() => {
    openVerifiedSession();
    store.submitSuccess();

    void store.createNewPreorder();
    http.expectOne('https://test.api/guest/sessions/removed-session').flush({ isSubmitted: true });
    tick();
    const create = http.expectOne('https://test.api/guest/sessions');
    expect(create.request.method).toBe('POST');
    expect(create.request.body).toEqual({ groupReservationId: 'group', guestName: 'Guest' });
    create.flush({ SessionId: 'fresh-session', SessionToken: 'fresh-token' });
    tick();
    http.expectOne('https://test.api/guest/sessions/fresh-session/menu')
      .flush({ dishes: { MainCourse: [{ id: 'salad', name: 'Salad', price: 8 }] } });
    tick();
    http.expectOne('https://test.api/guest/sessions/fresh-session').flush({
      name: 'Guest', items: [], total: 0, isSubmitted: false, orderingPreference: 'Online', revision: 0,
    });
    tick();

    expect(store.status()).toBe('active');
    expect(store.guestSessionId()).toBe('fresh-session');
    expect(store.hasMenu()).toBeTrue();
  }));
});
