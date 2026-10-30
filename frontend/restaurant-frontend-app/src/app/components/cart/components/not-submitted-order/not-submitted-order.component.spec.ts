import {
  ComponentFixture,
  TestBed,
} from '@angular/core/testing';

import { Order } from '@app/models/orders/order.model';

import { NotSubmittedOrderComponent } from './not-submitted-order.component';

describe('NotSubmittedOrderComponent', () => {
  let component: NotSubmittedOrderComponent;
  let fixture: ComponentFixture<NotSubmittedOrderComponent>;

  const order = {
    orderId: 'order-1',
    status: 0,
    reservationId: 'res-1',
    reservationAddress: 'Test address',
    reservationTime: '2026-09-07T12:15:00Z',
    orderItems: [
      {
        quantity: 2,
        dish: {
          id: 'dish-1',
          name: 'Test dish',
          price: 10,
        },
      },
    ],
  } as Order;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [NotSubmittedOrderComponent],
    }).compileComponents();

    fixture = TestBed.createComponent(
      NotSubmittedOrderComponent,
    );

    component = fixture.componentInstance;

    fixture.componentRef.setInput('order', order);
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should emit submitted with the order ID', () => {
    spyOn(component.submitted, 'emit');

    component.submit();

    expect(
      component.submitted.emit,
    ).toHaveBeenCalledWith('order-1');
  });

  it('should calculate the total price', () => {
    expect(component.totalPrice()).toBe(20);
  });
  it('shows a placeholder instead of a missing image request', () => {
    expect(fixture.nativeElement.querySelector('.dish-image-placeholder').getAttribute('aria-label'))
      .toBe('Test dish: image unavailable');
    expect(fixture.nativeElement.querySelector('img.dish-image')).toBeNull();
  });

  it('replaces a failed image and recovers when its URL changes', () => {
    const withImage = {
      ...order,
      orderItems: [{ ...order.orderItems[0], dish: {
        ...order.orderItems[0].dish, name: 'Salad', imageUrl: '/salad.jpg',
      } }],
    };
    fixture.componentRef.setInput('order', withImage);
    fixture.detectChanges();
    const image: HTMLImageElement = fixture.nativeElement.querySelector('img.dish-image');
    expect(image.getAttribute('src')).toBe('/salad.jpg');
    image.dispatchEvent(new Event('error'));
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('img.dish-image')).toBeNull();
    expect(fixture.nativeElement.querySelector('.dish-image-placeholder').getAttribute('aria-label'))
      .toBe('Salad: image unavailable');
    fixture.componentRef.setInput('order', { ...withImage, orderItems: [{
      ...withImage.orderItems[0], dish: { ...withImage.orderItems[0].dish, imageUrl: '/replacement.jpg' },
    }] });
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('img.dish-image').getAttribute('src'))
      .toBe('/replacement.jpg');
  });

});
