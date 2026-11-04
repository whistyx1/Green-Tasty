import { DatePipe } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  inject,
  Injector,
  input,
  OnInit,
  output,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Router } from '@angular/router';
import { interval } from 'rxjs';

import { WaiterPreOrderState } from '@app/models/orders/order.model';
import { CreateOrderRequest } from '@app/models/cart/create-order-request.model';
import {
  ReservationCustomer,
  ReservationStatusCustomer,
} from '@app/models/reservations/reservation.model';
import { PlaceOrderWaiterComponent } from '@app/components/place-order-waiter/place-order-waiter.component';
import { Roles } from '@app/shared/constants/roles';
import { CustomSelectComponent } from '@app/shared/custom-components/custom-select/custom-select.component';
import { preOrderMenuQueryParams } from '@app/shared/helpers/pre-order-navigation';
import { GuestInviteService } from '@app/services/reservation/guest-invite.service';
import { ToastService } from '@app/services/toast/toast.service';

interface ReservationStatusView {
  class: string;
  label: string;
}

@Component({
  selector: 'app-card-reservation',
  standalone: true,
  imports: [
    DatePipe,
    CustomSelectComponent,
    PlaceOrderWaiterComponent,
  ],
  templateUrl: './card-reservation.component.html',
  styleUrl: './card-reservation.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CardReservationComponent implements OnInit {
  private readonly router = inject(Router);
  private readonly injector = inject(Injector);
  private readonly destroyRef = inject(DestroyRef);
  readonly Roles = Roles;
  readonly ReservationStatusCustomer = ReservationStatusCustomer;

  readonly r = input.required<ReservationCustomer>();
  readonly role = input<string>(Roles.CUSTOMER);
  readonly ownerUserId = input('');
  readonly submitLoading = input(false);
  readonly submissionState = input<'submitted' | 'not-submitted' | 'unavailable' | null>(null);
  private readonly now = signal(Date.now());
  readonly copyingInvite = signal(false);
  readonly showInviteAction = computed(() =>
    this.role() === Roles.CUSTOMER &&
    this.r().guestCount > 1 &&
    !!this.ownerUserId() &&
    this.r().customerId === this.ownerUserId()
  );
  readonly inviteDisabledReason = computed(() => {
    if (this.r().status !== ReservationStatusCustomer.Reserved)
      return 'Invitation unavailable: reservation is no longer active.';
    if (new Date(this.r().endTime).getTime() <= this.now())
      return 'Invitation unavailable: reservation has ended.';
    if (!this.r().guestInviteLinkAvailable)
      return 'Invitation unavailable: group pre-order is submitted or closed.';
    return '';
  });

  ngOnInit(): void {
    if (this.role() === Roles.CUSTOMER) {
      interval(60_000).pipe(takeUntilDestroyed(this.destroyRef))
        .subscribe(() => this.now.set(Date.now()));
    }
  }

  readonly showSingleSubmit = computed(() => {
    const reservation = this.r();
    const cutoff = new Date(reservation.startTime).getTime() - 30 * 60_000;
    return this.role() === Roles.CUSTOMER && reservation.guestCount === 1 &&
      reservation.status === ReservationStatusCustomer.Reserved &&
      !!this.ownerUserId() && reservation.customerId === this.ownerUserId() &&
      Number.isFinite(cutoff) && this.now() <= cutoff &&
      (reservation.preOrder == null || reservation.preOrder.status === 0);
  });
  readonly singleSubmitDisabledReason = computed(() => {
    const items = this.r().preOrder?.orderItems;
    return items?.length ? '' : 'Add dishes to this reservation before submitting.';
  });
  readonly submitSinglePreOrder = output<string>();

  onSubmitSinglePreOrder(): void {
    this.now.set(Date.now());
    if (!this.showSingleSubmit() || this.singleSubmitDisabledReason() || this.submitLoading()) return;
    this.submitSinglePreOrder.emit(this.r().id);
  }

  copyInvitationLink(): void {
    this.now.set(Date.now());
    if (!this.showInviteAction() || this.inviteDisabledReason() || this.copyingInvite()) return;
    const guestInviteService = this.injector.get(GuestInviteService);
    const toastService = this.injector.get(ToastService);
    this.copyingInvite.set(true);
    guestInviteService.getInviteToken$(this.r().id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: async ({ token }) => {
          try {
            await navigator.clipboard.writeText(`${window.location.origin}/join/${token}`);
            toastService.success('Share it with your friends.', 'Link copied!');
          } catch {
            toastService.error('Could not copy the link. Please try again.');
          } finally {
            this.copyingInvite.set(false);
          }
        },
        error: () => {
          this.copyingInvite.set(false);
          toastService.error('Invitation link is unavailable. Please refresh your reservations.');
        },
      });
  }
  readonly statusEnum =
    input<Record<number, ReservationStatusView>>({});

  readonly availableTableOptions = input<string[]>([]);

  readonly availableTableOptionsMap =
    input<Map<string, string>>(new Map<string, string>());

  readonly selectedTableLabel = input('');

  readonly createdByName = input<string | undefined>();

  readonly createdByRole =
    input<ReservationCustomer['createdByRole']>();
  readonly tableChanged = output<{
    reservationId: string;
    newTableId: string;
  }>();

  readonly cancelReservation = output<string>();

  readonly editReservation =
    output<ReservationCustomer>();

  readonly postponeReservation =
    output<ReservationCustomer>();

  readonly leaveFeedbackReservation =
    output<ReservationCustomer>();

  readonly updateFeedbackReservation =
    output<ReservationCustomer>();

  readonly sendAnonymousFeedback =
    output<ReservationCustomer>();

  readonly statusChanged = output<{
    id: string;
    newStatus: ReservationStatusCustomer;
  }>();

  readonly preOrder = output<CreateOrderRequest>();
  readonly retryPreOrder = output<string>();
  readonly showWaiterOrderModal = signal(false);

  onGroupPreOrder(): void {
    void this.router.navigate(['/reservations', this.r().id, 'group-order']);
  }

  readonly statusView = computed<ReservationStatusView>(() => {
    const reservation = this.r();

    return (
      this.statusEnum()[reservation.status] ?? {
        class: '',
        label: 'Unknown',
      }
    );
  });

  readonly displayedTableLabel = computed(() => {
    return this.selectedTableLabel() || 'Table (Unknown)';
  });

  readonly creatorName = computed(() => {
    return (
      this.createdByName() ||
      this.r().createdByName ||
      'Unknown'
    );
  });

  readonly creatorRole = computed(() => {
    return (
      this.createdByRole() ||
      this.r().createdByRole ||
      Roles.CUSTOMER
    );
  });

  readonly preOrderItemCount = computed(() => {
    return this.r().preOrder?.orderItems?.length ?? 0;
  });

  readonly canOpenWaiterPreOrder = computed(() => {
    return !this.r().preOrderLoadError && (
      this.preOrderItemCount() > 0 ||
      this.creatorRole() === Roles.WAITER
    );
  });

  readonly isAnonymous = computed(() => {
    return !this.r().customerId;
  });

  readonly hasFullFeedback = computed(() => {
    const feedback = this.r().feedback;

    return (
      feedback?.cuisineExperience != null &&
      feedback?.service != null
    );
  });

  onPreOrder(): void {
    void this.router.navigate(['/menu'], {
      queryParams: preOrderMenuQueryParams(this.r()),
    });
  }
  onPreOrderWaiter(): void {
    if (this.preOrderItemCount() > 0) {
      this.navigateToWaiterPreOrder();
      return;
    }

    if (this.creatorRole() === Roles.WAITER) {
      this.showWaiterOrderModal.set(true);
    }
  }

  closeWaiterOrderModal(): void {
    this.showWaiterOrderModal.set(false);
  }

  onWaiterOrderCreated(order: CreateOrderRequest): void {
    this.showWaiterOrderModal.set(false);
    this.preOrder.emit(order);
  }

  onTableChange(label: string): void {
    const reservation = this.r();
    const newTableId =
      this.availableTableOptionsMap().get(label);

    if (!newTableId || newTableId === reservation.tableId) {
      return;
    }

    this.tableChanged.emit({
      reservationId: reservation.id,
      newTableId,
    });
  }

  onStatusChange(
    newStatus: ReservationStatusCustomer,
  ): void {
    this.statusChanged.emit({
      id: this.r().id,
      newStatus,
    });
  }

  private navigateToWaiterPreOrder(): void {
    const reservation = this.r();

    const navigationData: WaiterPreOrderState = {
      reservationId: reservation.id,
      createdByName: this.creatorName(),
      createdByRole: this.creatorRole(),
      tableName: this.selectedTableLabel() || reservation.tableName || 'Table unavailable',
      startTime: new Date(reservation.startTime).toISOString(),
      endTime: new Date(reservation.endTime).toISOString(),
    };

    sessionStorage.setItem(
      'waiterPreOrderData',
      JSON.stringify(navigationData),
    );

    void this.router.navigate(['/waiter-pre-order'], {
      state: navigationData,
    });
  }
}
