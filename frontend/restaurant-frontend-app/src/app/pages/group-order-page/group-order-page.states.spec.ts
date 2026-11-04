import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { Observable, of, Subject, throwError } from 'rxjs';
import { GroupOrdersService } from '@app/services/group-orders/group-orders.service';
import { ToastService } from '@app/services/toast/toast.service';
import { GroupSessionsResponse } from '@app/models/group-orders/group-sessions-response.model';
import { GuestInvite } from '@app/models/reservations/guest-invite.model';
import { GroupOrderPageComponent } from './group-order-page.component';

const INVITE: GuestInvite = {
  GroupReservationId: 'group',
  LocationId: 'location',
  RestaurantName: 'Test',
  LocationAddress: 'Test street',
  StartTime: '2030-01-01T16:00:00Z',
  EndTime: '2030-01-01T17:00:00Z',
  MaxGuestCount: 4,
  TableNumber: 1,
  InviterName: 'Owner',
};
const SESSIONS: GroupSessionsResponse = {
  participants: [
    {
      id: 'owner',
      type: 'Owner',
      name: 'Owner',
      isSubmitted: true,
      total: 0,
      items: [],
    },
  ],
  total: 8,
  ownerPreOrder: { items: [{ dish: { id: 'salad', name: 'Salad', price: 8 }, quantity: 1 }], total: 8, editable: true },
};

function button(root: ParentNode, text: string): HTMLButtonElement | undefined {
  return [...root.querySelectorAll('button')].find(
    (element) => element.textContent?.trim() === text
  );
}

describe('Group order page state and lifecycle', () => {
  let service: jasmine.SpyObj<GroupOrdersService>;
  let toast: jasmine.SpyObj<ToastService>;

  beforeEach(() => {
    service = jasmine.createSpyObj<GroupOrdersService>('GroupOrdersService', [
      'getGroupInvite$',
      'getOwnerSessionId$',
      'getSessions$',
      'getStatus$',
      'submit$',
      'setOwnerPreference$',
      'submitOwner$',
    ]);
    toast = jasmine.createSpyObj<ToastService>('ToastService', ['success', 'error', 'info']);
    service.getGroupInvite$.and.returnValue(of(INVITE));
    service.getOwnerSessionId$.and.returnValue(of('owner'));
    service.getSessions$.and.returnValue(of(structuredClone(SESSIONS)));
    service.getStatus$.and.returnValue(
      of({ currentParticipants: 1, maxCapacity: 4, status: 'Open' })
    );
  });

  function create(reservationId: string | null = 'reservation') {
    TestBed.configureTestingModule({
      imports: [GroupOrderPageComponent],
      providers: [
        { provide: GroupOrdersService, useValue: service },
        { provide: ToastService, useValue: toast },
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: { paramMap: convertToParamMap(reservationId ? { reservationId } : {}) },
          },
        },
      ],
    });
    const fixture = TestBed.createComponent(GroupOrderPageComponent);
    fixture.detectChanges();
    return { fixture, element: fixture.nativeElement as HTMLElement };
  }

  it('offers the owner a preference toggle and sends its current session revision', () => {
    const snapshot = structuredClone(SESSIONS);
    snapshot.participants[0].isSubmitted = false;
    snapshot.participants[0].revision = 2;
    service.getSessions$.and.returnValue(of(snapshot));
    service.setOwnerPreference$.and.returnValue(of({ orderingPreference: 'InRestaurant', isSubmitted: false, revision: 3 }));
    const { fixture, element } = create();

    button(element, 'In Restaurant')?.click();
    fixture.detectChanges();

    expect(service.setOwnerPreference$).toHaveBeenCalledOnceWith('reservation', 'InRestaurant', 2);
    expect(service.submitOwner$).not.toHaveBeenCalled();
  });

  it('confirms the owner draft once using its displayed order revision', () => {
    const snapshot = structuredClone(SESSIONS);
    snapshot.participants[0].isSubmitted = false;
    snapshot.ownerPreOrder!.revision = 4;
    service.getSessions$.and.returnValue(of(snapshot));
    const pending = new Subject<{ orderingPreference: 'Online'; isSubmitted: boolean; revision: number }>();
    service.submitOwner$.and.returnValue(pending);
    const { fixture, element } = create();

    button(element, 'Submit My Pre-order')?.click();
    fixture.detectChanges();
    const dialog = element.querySelector('[role="dialog"]')!;
    expect(dialog.textContent).toContain('Salad × 1');
    button(dialog, 'Confirm')?.click();
    button(dialog, 'Confirm')?.click();

    expect(service.submitOwner$).toHaveBeenCalledOnceWith('reservation', 4);
    pending.next({ orderingPreference: 'Online', isSubmitted: true, revision: 1 });
    pending.complete();
  });

  it('shows loading while the invite is pending, then displays the response', () => {
    const invitation = new Subject<GuestInvite>();
    service.getGroupInvite$.and.returnValue(invitation);
    const { fixture, element } = create();
    expect(element.textContent).toContain('Loading group pre-order');
    expect(element.querySelector('main')?.getAttribute('aria-busy')).toBe('true');
    invitation.next(INVITE);
    invitation.complete();
    fixture.detectChanges();
    expect(element.textContent).toContain('My Pre-Order');
    expect(element.querySelector('main')?.getAttribute('aria-busy')).toBe('false');
  });

  it('Cancel closes confirmation without sending an order', () => {
    const { fixture, element } = create();
    button(element, 'Submit Pre-Order')?.click();
    fixture.detectChanges();
    button(element.querySelector('[role="dialog"]')!, 'Cancel')?.click();
    fixture.detectChanges();
    expect(element.querySelector('[role="dialog"]')).toBeNull();
    expect(service.submit$).not.toHaveBeenCalled();
  });

  for (const status of ['Submitted', 'Closed'] as const) {
    it(`an initially ${status} group cannot submit`, () => {
      service.getStatus$.and.returnValue(of({ currentParticipants: 1, maxCapacity: 4, status }));
      const { fixture, element } = create();
      expect(button(element, 'Submit Pre-Order')).toBeUndefined();
      fixture.componentInstance.openConfirmation();
      fixture.componentInstance.submit();
      expect(service.submit$).not.toHaveBeenCalled();
    });
  }

  it('missing reservation ID gives a controlled error without calling the service', () => {
    expect(create(null).element.textContent).toContain('Group reservation not found');
    expect(service.getGroupInvite$).not.toHaveBeenCalled();
  });

  it('a failed submission can be retried and creates only one success toast', () => {
    service.submit$.and.returnValues(
      throwError(() => new HttpErrorResponse({ status: 400 })),
      of([{ dishId: 'salad', quantity: 1 }])
    );
    const { fixture, element } = create();
    for (let attempt = 0; attempt < 2; attempt++) {
      button(element, 'Submit Pre-Order')?.click();
      fixture.detectChanges();
      button(element.querySelector('[role="dialog"]')!, 'Confirm')?.click();
      fixture.detectChanges();
    }
    expect(service.submit$).toHaveBeenCalledTimes(2);
    expect(toast.error).not.toHaveBeenCalled();
    expect(toast.success).toHaveBeenCalledOnceWith('Pre-order sent to the restaurant');
    expect(element.textContent).toContain('Sent to Restaurant');
  });

  it('destroying the page cancels the pending load', () => {
    let cancelled = false;
    service.getGroupInvite$.and.returnValue(
      new Observable<GuestInvite>(() => () => {
        cancelled = true;
      })
    );
    const { fixture } = create();
    fixture.destroy();
    expect(cancelled).toBeTrue();
  });
});
