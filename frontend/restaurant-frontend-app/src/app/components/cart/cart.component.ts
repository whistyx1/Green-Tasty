import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  OnInit,
  computed,
  inject,
  output,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Router } from '@angular/router';

import { Order } from '@app/models/orders/order.model';
import { PreOrdersService } from '@app/services/pre-orders/pre-orders.service';
import { SubmissionConfirmationComponent, SubmissionItem } from '@app/shared/custom-components/submission-confirmation/submission-confirmation.component';

import { CancelOrderModalComponent } from './components/cancel-order-modal/cancel-order-modal.component';
import { NotSubmittedOrderComponent } from './components/not-submitted-order/not-submitted-order.component';
import { SubmitOrderModalComponent } from './components/submit-order-modal/submit-order-modal.component';
import { SubmittedOrderComponent } from './components/submitted-order/submitted-order.component';

@Component({
  selector: 'app-cart',
  standalone: true,
  imports: [
    SubmittedOrderComponent,
    NotSubmittedOrderComponent,
    CancelOrderModalComponent,
    SubmitOrderModalComponent,
    SubmissionConfirmationComponent,
  ],
  templateUrl: './cart.component.html',
  styleUrl: './cart.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CartComponent implements OnInit {
  private readonly preOrdersService =
    inject(PreOrdersService);

  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  readonly closeCartEvent = output<void>();

  readonly orders = signal<Order[]>([]);

  readonly cancelOrderId = signal<string | null>(null);
  readonly submitOrderId = signal<string | null>(null);
  readonly pendingOrderId = signal<string | null>(null);
  readonly submitting = signal(false);
  readonly submissionError = signal('');
  readonly pendingOrder = computed(() => this.orders().find(
    order => order.orderId === this.pendingOrderId() && order.status === 0
  ));
  readonly pendingItems = computed<SubmissionItem[]>(() =>
    this.pendingOrder()?.orderItems.map(item => ({
      name: item.dish.name,
      quantity: item.quantity,
      unitPrice: item.dish.price,
    })) ?? []
  );

  readonly submittedOrders = computed(() =>
    this.orders().filter(
      (order) => order.status === 1 || order.status === 3,
    ),
  );

  readonly unsubmittedOrders = computed(() =>
    this.orders().filter((order) => order.status === 0),
  );

  readonly submittedOrder = computed(() => {
    const orderId = this.submitOrderId();

    if (!orderId) {
      return undefined;
    }

    return this.orders().find(
      (order) => order.orderId === orderId,
    );
  });

  ngOnInit(): void {
    this.loadOrders();
  }

  closeCart(): void {
    this.closeCartEvent.emit();
  }

  openCancelModal(orderId: string): void {
    this.cancelOrderId.set(orderId);
    this.submitOrderId.set(null);
  }

  closeCancelModal(): void {
    this.cancelOrderId.set(null);
  }

  closeSubmitModal(): void {
    this.submitOrderId.set(null);
  }

  editOrderModal(): void {
    const orderId = this.submitOrderId();

    if (!orderId) {
      console.error('No order selected for editing');
      return;
    }

    this.editOrder(orderId);
    this.closeSubmitModal();
  }

  goToReservations(): void {
    this.closeCart();
    void this.router.navigate(['/reservations']);
  }

  cancelOrder(orderId: string): void {
    const order = this.findOrder(orderId);

    if (!order) {
      return;
    }

    this.preOrdersService
      .cancelPreOrderByReservationId(order.reservationId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.closeCancelModal();
          this.loadOrders();
        },
        error: (error) => {
          console.error(
            `Error cancelling order ${orderId}:`,
            error,
          );
        },
      });
  }

  editOrder(orderId: string): void {
    const order = this.findOrder(orderId);

    if (!order) {
      return;
    }

    this.preOrdersService
      .unsubmitPreOrderByReservationId(order.reservationId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.loadOrders();
        },
        error: (error) => {
          console.error(
            `Error un-submitting order ${orderId}:`,
            error,
          );
        },
      });
  }

  openSubmission(orderId: string): void {
    const order = this.orders().find(item => item.orderId === orderId && item.status === 0);
    if (!order || !order.orderItems.length || this.submitting()) return;
    this.submissionError.set('');
    this.pendingOrderId.set(orderId);
  }

  closeSubmission(): void {
    if (!this.submitting()) this.pendingOrderId.set(null);
  }

  submitOrder(): void {
    const order = this.pendingOrder();
    if (!order || !order.orderItems.length || this.submitting()) return;
    const orderId = order.orderId;
    this.submitting.set(true);
    this.submissionError.set('');
    this.preOrdersService
      .submitPreOrderByReservationId(order.reservationId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.pendingOrderId.set(null);
          this.submitOrderId.set(orderId);
          this.submitting.set(false);
          this.loadOrders();
        },
        error: () => {
          this.pendingOrderId.set(null);
          this.submitting.set(false);
          this.submissionError.set('Could not submit this pre-order. Please review it and try again.');
          this.loadOrders();
        },
      });
  }

  addDishToOrder(orderId: string): void {
    const order = this.orders().find(
      (item) => item.orderId === orderId && item.status === 0,
    );
    if (!order?.reservationId) return;

    this.closeCart();
    void this.router.navigate(['/menu'], {
      queryParams: {
        preOrder: 'true',
        reservationId: order.reservationId,
        reservationLabel: [order.reservationAddress, order.reservationTime]
          .filter(Boolean)
          .join(', '),
      },
    });
  }

  updateDishQuantity(
    orderId: string,
    itemId: string,
    quantity: number,
  ): void {
    if (quantity <= 0) {
      return;
    }

    const originalOrder = this.findOrder(orderId);

    if (!originalOrder) {
      return;
    }

    const updatedOrder: Order = {
      ...originalOrder,
      orderItems: originalOrder.orderItems.map((item) =>
        item.dish.id === itemId
          ? {
            ...item,
            quantity,
          }
          : item,
      ),
    };

    this.preOrdersService
      .editPreOrderByReservationId(
        updatedOrder.reservationId,
        updatedOrder,
      )
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.loadOrders();
        },
        error: (error) => {
          console.error(
            `Error updating item ${itemId}:`,
            error,
          );
        },
      });
  }

  removeDishFromOrder(
    orderId: string,
    itemId: string,
  ): void {
    const originalOrder = this.findOrder(orderId);

    if (!originalOrder) {
      return;
    }

    const updatedOrder: Order = {
      ...originalOrder,
      orderItems: originalOrder.orderItems.filter(
        (item) => item.dish.id !== itemId,
      ),
    };

    this.preOrdersService
      .editPreOrderByReservationId(
        updatedOrder.reservationId,
        updatedOrder,
      )
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.loadOrders();
        },
        error: (error) => {
          console.error(
            `Error removing item ${itemId}:`,
            error,
          );
        },
      });
  }

  private findOrder(orderId: string): Order | undefined {
    const order = this.orders().find(
      (item) => item.orderId === orderId,
    );

    if (!order) {
      console.error(`Order with ID ${orderId} not found`);
    }

    return order;
  }

  private loadOrders(): void {
    this.preOrdersService
      .getPreOrders()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (orders) => {
          this.orders.set(orders);
        },
        error: (error) => {
          console.error('Error loading orders:', error);
          this.orders.set([]);
        },
      });
  }
}
