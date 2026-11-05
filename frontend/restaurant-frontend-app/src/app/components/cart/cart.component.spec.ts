import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { of, Subject, throwError } from 'rxjs';

import { Order } from '@app/models/orders/order.model';
import { PreOrdersService } from '@app/services/pre-orders/pre-orders.service';

import { CartComponent } from './cart.component';

describe('CartComponent', () => {
  let component: CartComponent;
  let fixture: ComponentFixture<CartComponent>;

  let preOrdersServiceMock: jasmine.SpyObj<PreOrdersService>;
  let routerMock: jasmine.SpyObj<Router>;

  beforeEach(async () => {
    preOrdersServiceMock =
      jasmine.createSpyObj<PreOrdersService>(
        'PreOrdersService',
        [
          'getPreOrders',
          'cancelPreOrderByReservationId',
          'unsubmitPreOrderByReservationId',
          'submitPreOrderByReservationId',
          'editPreOrderByReservationId',
        ],
      );

    routerMock = jasmine.createSpyObj<Router>(
      'Router',
      ['navigate'],
    );

    preOrdersServiceMock.getPreOrders.and.returnValue(of([]));

    await TestBed.configureTestingModule({
      imports: [CartComponent],
      providers: [
        {
          provide: PreOrdersService,
          useValue: preOrdersServiceMock,
        },
        {
          provide: Router,
          useValue: routerMock,
        },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(CartComponent);
    component = fixture.componentInstance;

    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should emit closeCartEvent when closeCart is called', () => {
    spyOn(component.closeCartEvent, 'emit');

    component.closeCart();

    expect(
      component.closeCartEvent.emit,
    ).toHaveBeenCalled();
  });

  it('should return submitted orders with statuses 1 and 3', () => {
    component.orders.set([
      {
        orderId: 'order-1',
        status: 1,
      },
      {
        orderId: 'order-2',
        status: 2,
      },
      {
        orderId: 'order-3',
        status: 3,
      },
      {
        orderId: 'order-4',
        status: 0,
      },
    ] as Order[]);

    expect(
      component
        .submittedOrders()
        .map((order) => order.status),
    ).toEqual([1, 3]);
  });

  it('should return unsubmitted orders with status 0', () => {
    component.orders.set([
      {
        orderId: 'order-1',
        status: 0,
      },
      {
        orderId: 'order-2',
        status: 1,
      },
      {
        orderId: 'order-3',
        status: 3,
      },
    ] as Order[]);

    expect(
      component
        .unsubmittedOrders()
        .map((order) => order.status),
    ).toEqual([0]);
  });

  it('should navigate to reservations when goToReservations is called', () => {
    component.goToReservations();

    expect(routerMock.navigate).toHaveBeenCalledWith([
      '/reservations',
    ]);
  });
  const draft = {
    orderId: 'order-1', reservationId: 'reservation-1', status: 0,
    reservationAddress: 'Street', reservationTime: '2030-01-01T12:00:00Z',
    orderItems: [{ dish: { id: 'salad', name: 'Salad', price: 8 }, quantity: 2 }],
  } as Order;

  it('Add dish opens the selected reservation menu and closes the cart', () => {
    component.orders.set([{ ...draft, orderItems: [] }, { ...draft, orderId: 'order-2', reservationId: 'reservation-2' }]);
    fixture.detectChanges();
    spyOn(component.closeCartEvent, 'emit');

    (fixture.nativeElement as HTMLElement)
      .querySelector<HTMLButtonElement>('app-not-submitted-order .add-dish-btn')
      ?.click();

    expect(routerMock.navigate).toHaveBeenCalledOnceWith(['/menu'], {
      queryParams: {
        preOrder: 'true',
        reservationId: 'reservation-1',
        reservationLabel: 'Street, 2030-01-01T12:00:00Z',
      },
    });
    expect(component.closeCartEvent.emit).toHaveBeenCalledTimes(1);
  });

  it('does not navigate from an unavailable draft order', () => {
    component.addDishToOrder('missing-order');
    expect(routerMock.navigate).not.toHaveBeenCalled();
  });

  it('opens ordinary confirmation with the selected dish and total; Cancel does not submit', () => {
    component.orders.set([draft]);
    component.openSubmission(draft.orderId);
    fixture.detectChanges();
    const dialog = (fixture.nativeElement as HTMLElement).querySelector('app-submission-confirmation [role="dialog"]');
    expect(dialog?.textContent).toContain('Salad × 2');
    expect(dialog?.textContent).toContain('$16.00');
    component.closeSubmission();
    fixture.detectChanges();
    expect(preOrdersServiceMock.submitPreOrderByReservationId).not.toHaveBeenCalled();
    expect(component.pendingOrderId()).toBeNull();
  });

  it('confirms ordinary order once even when Confirm is invoked twice', () => {
    const pending = new Subject<unknown>();
    preOrdersServiceMock.submitPreOrderByReservationId.and.returnValue(pending as ReturnType<PreOrdersService['submitPreOrderByReservationId']>);
    component.orders.set([draft]);
    component.openSubmission(draft.orderId);
    component.submitOrder();
    component.submitOrder();
    expect(preOrdersServiceMock.submitPreOrderByReservationId).toHaveBeenCalledOnceWith('reservation-1');
    pending.next({});
    pending.complete();
    expect(component.pendingOrderId()).toBeNull();
  });

  it('rejects a stale ordinary order and allows recovery after a server error', () => {
    component.orders.set([{ ...draft, status: 1 }]);
    component.openSubmission(draft.orderId);
    expect(component.pendingOrderId()).toBeNull();
    component.orders.set([draft]);
    component.openSubmission(draft.orderId);
    preOrdersServiceMock.submitPreOrderByReservationId.and.returnValue(throwError(() => new Error('stale')));
    component.submitOrder();
    expect(component.submissionError()).toContain('Could not submit');
    expect(component.submitting()).toBeFalse();
    expect(component.pendingOrderId()).toBeNull();
  });

  it('preserves ordinary unsubmit and cancel service actions', () => {
    component.orders.set([{ ...draft, status: 1 }]);
    preOrdersServiceMock.unsubmitPreOrderByReservationId.and.returnValue(of({}));
    preOrdersServiceMock.cancelPreOrderByReservationId.and.returnValue(of({}));
    component.editOrder(draft.orderId);
    component.orders.set([{ ...draft, status: 1 }]);
    component.cancelOrder(draft.orderId);
    expect(preOrdersServiceMock.unsubmitPreOrderByReservationId).toHaveBeenCalledOnceWith('reservation-1');
    expect(preOrdersServiceMock.cancelPreOrderByReservationId).toHaveBeenCalledOnceWith('reservation-1');
  });
});
