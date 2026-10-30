import { TestBed, fakeAsync, tick } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, ParamMap } from '@angular/router';
import { BehaviorSubject, Observable, of, Subject } from 'rxjs';
import { GroupOrdersService } from '@app/services/group-orders/group-orders.service';
import { ToastService } from '@app/services/toast/toast.service';
import { GroupSessionsResponse } from '@app/models/group-orders/group-sessions-response.model';
import { GuestInvite } from '@app/models/reservations/guest-invite.model';
import { GroupOrderPageComponent } from './group-order-page.component';

const dish = { id: 'salad', name: 'Salad', price: 8 };
const data: GroupSessionsResponse = {
  participants: [
    {
      id: 'owner',
      type: 'Owner',
      name: 'Owner',
      isSubmitted: true,
      items: [{ dish, quantity: 1 }],
      total: 8,
    },
    {
      id: 'guest',
      type: 'Guest',
      name: 'Guest',
      isSubmitted: false,
      items: [{ dish, quantity: 1 }],
      total: 8,
    },
  ],
  total: 16,
};
const invite = {
  GroupReservationId: 'group',
  MaxGuestCount: 4,
  StartTime: '2030-01-01T16:00:00Z',
  EndTime: '2030-01-01T17:00:00Z',
  LocationAddress: 'Street',
} as GuestInvite;

describe('Group order synchronization and mutation reliability', () => {
  let service: jasmine.SpyObj<GroupOrdersService>;
  beforeEach(() => {
    service = jasmine.createSpyObj<GroupOrdersService>('GroupOrdersService', [
      'getOwnerSessionId$',
      'getGroupInvite$',
      'getSessions$',
      'getStatus$',
      'submit$',
      'removeSession$',
    ]);
    service.getOwnerSessionId$.and.returnValue(of('owner'));
    service.getGroupInvite$.and.returnValue(of(invite));
    service.getSessions$.and.returnValue(of(structuredClone(data)));
    service.getStatus$.and.returnValue(
      of({ currentParticipants: 2, maxCapacity: 4, status: 'Open' })
    );
  });
  function create(params?: BehaviorSubject<ParamMap>) {
    TestBed.configureTestingModule({
      imports: [GroupOrderPageComponent],
      providers: [
        { provide: GroupOrdersService, useValue: service },
        {
          provide: ToastService,
          useValue: jasmine.createSpyObj('ToastService', ['success', 'error']),
        },
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: { paramMap: convertToParamMap({ reservationId: 'reservation' }) },
            ...(params ? { paramMap: params } : {}),
          },
        },
      ],
    });
    const fixture = TestBed.createComponent(GroupOrderPageComponent);
    fixture.detectChanges();
    return fixture;
  }

  it('manual and timed refresh share one in-flight guard and never overlap', fakeAsync(() => {
    const fixture = create();
    const pending = new Subject<GroupSessionsResponse>();
    service.getSessions$.and.returnValue(pending);
    fixture.componentInstance.refresh();
    fixture.componentInstance.refresh();
    tick(30_000);
    expect(service.getSessions$).toHaveBeenCalledTimes(2);
    pending.next(data);
    pending.complete();
    tick(15_000);
    expect(service.getSessions$).toHaveBeenCalledTimes(3);
    fixture.destroy();
  }));

  it('reloading twice replaces the old timer instead of multiplying timed requests', fakeAsync(() => {
    const fixture = create();
    fixture.componentInstance.load();
    fixture.componentInstance.load();
    expect(service.getSessions$).toHaveBeenCalledTimes(3);
    tick(15_000);
    expect(service.getSessions$).toHaveBeenCalledTimes(4);
    fixture.destroy();
  }));

  it('remote Submitted status closes an open confirmation and stops polling', fakeAsync(() => {
    const fixture = create();
    fixture.componentInstance.openConfirmation();
    expect(fixture.componentInstance.confirmationOpen()).toBeTrue();
    service.getStatus$.and.returnValue(
      of({ currentParticipants: 2, maxCapacity: 4, status: 'Submitted' })
    );
    tick(15_000);
    expect(fixture.componentInstance.confirmationOpen()).toBeFalse();
    tick(30_000);
    expect(service.getSessions$).toHaveBeenCalledTimes(2);
    fixture.destroy();
  }));

  it('route ID changes cancel a pending old load and start the new reservation', () => {
    let cancelled = 0;
    service.getGroupInvite$.and.returnValue(new Observable<GuestInvite>(() => () => cancelled++));
    const params = new BehaviorSubject(convertToParamMap({ reservationId: 'first' }));
    const fixture = create(params);
    params.next(convertToParamMap({ reservationId: 'second' }));
    expect(cancelled).toBe(1);
    expect(service.getOwnerSessionId$.calls.mostRecent().args[0]).toBe('second');
    fixture.destroy();
    expect(cancelled).toBe(2);
  });

  it('DELETE cancels a pending refresh before applying its exact returned membership', fakeAsync(() => {
    const fixture = create();
    let cancelled = false;
    service.getSessions$.and.returnValue(
      new Observable<GroupSessionsResponse>(() => () => (cancelled = true))
    );
    fixture.componentInstance.refresh();
    service.removeSession$.and.returnValue(of({ participants: [data.participants[0]], total: 8 }));
    fixture.componentInstance.openRemoval(data.participants[1]);
    fixture.componentInstance.removeGuest();
    expect(cancelled).toBeTrue();
    expect(fixture.componentInstance.participants().map((person) => person.id)).toEqual(['owner']);
    fixture.destroy();
  }));

  it('ordinary Owner preorder is included once without relabeling raw session submission', () => {
    const response = structuredClone(data);
    response.participants[0] = {
      ...response.participants[0],
      items: [],
      total: 0,
      isSubmitted: false,
    };
    response.participants[1].isSubmitted = true;
    response.ownerPreOrder = { items: [{ dish, quantity: 1 }], total: 8, editable: true };
    response.groupStatus = { currentParticipants: 2, maxCapacity: 4, status: 'Open' };
    service.getSessions$.and.returnValue(of(response));
    const fixture = create();
    expect(fixture.componentInstance.consolidated().total).toBe(16);
    expect(fixture.componentInstance.participants()[0].isSubmitted).toBeFalse();
    expect(fixture.nativeElement.textContent).toContain('Saved pre-order');
    expect(service.getStatus$).not.toHaveBeenCalled();
    fixture.destroy();
  });

  it('successful POST quantities replace the last preview rather than freezing stale quantities', () => {
    service.submit$.and.returnValue(of([{ dishId: 'salad', quantity: 5 }]));
    const fixture = create();
    fixture.componentInstance.openConfirmation();
    fixture.componentInstance.submit();
    expect(fixture.componentInstance.consolidated().items[0].quantity).toBe(5);
    expect(fixture.componentInstance.consolidated().total).toBe(40);
    expect(fixture.componentInstance.status()).toBe('Submitted');
    fixture.destroy();
  });

  it('unknown final dish metadata leaves a committed order read-only and hides an unverified total', () => {
    service.submit$.and.returnValue(of([{ dishId: 'new-dish', quantity: 1 }]));
    const fixture = create();
    fixture.componentInstance.openConfirmation();
    fixture.componentInstance.submit();
    fixture.detectChanges();
    expect(fixture.componentInstance.status()).toBe('Submitted');
    expect(fixture.nativeElement.querySelector('.grand-total')).toBeNull();
    expect(fixture.nativeElement.textContent).toContain("Couldn't load the complete final summary");
    expect(fixture.componentInstance.canSubmit()).toBeFalse();
    fixture.destroy();
  });

  it('an already processed ordinary Owner order cannot be sent again as part of a group', () => {
    service.getSessions$.and.returnValue(
      of({ ...data, ownerPreOrder: { items: [{ dish, quantity: 1 }], total: 8, editable: false } })
    );
    const fixture = create();
    expect(fixture.componentInstance.canSubmit()).toBeFalse();
    expect(fixture.nativeElement.textContent).toContain('Unsubmit it before sending');
    fixture.destroy();
  });
});
