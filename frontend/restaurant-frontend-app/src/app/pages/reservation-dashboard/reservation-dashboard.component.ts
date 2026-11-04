import { HttpErrorResponse } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  OnInit,
  computed,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Router, RouterLink } from '@angular/router';
import {
  catchError,
  finalize,
  forkJoin,
  from,
  map,
  mergeMap,
  of,
  switchMap,
  Subscription,
  throwError,
  toArray,
} from 'rxjs';

import { AuthenticationService } from '@app/auth/services/authentication.service';
import { CardReservationComponent } from '@app/components/reservations/card-reservation/card-reservation.component';
import { FeedbackWindowComponent } from '@app/components/reservations/feedback-window/feedback-window.component';
import { ReservationConfirmedComponent } from '@app/components/reservations/reservation-confirmed/reservation-confirmed.component';
import { MakeReservationComponent } from '@app/components/tables/make-reservation/make-reservation.component';
import { FeedbackUpdatedEvent } from '@app/models/feedbacks/feedback.model';
import { Order } from '@app/models/orders/order.model';
import {
  Reservation,
  ReservationCustomer,
  ReservationStatus,
  ReservationStatusCustomer,
} from '@app/models/reservations/reservation.model';
import { Table } from '@app/models/tables/table.model';
import { TimeRange } from '@app/models/tables/time-range.model';
import { LocationsService } from '@app/services/locations/locations.service';
import { PreOrdersService } from '@app/services/pre-orders/pre-orders.service';
import { ReservationService } from '@app/services/reservation/reservation.service';
import { TableService } from '@app/services/table/table.service';
import { Roles } from '@app/shared/constants/roles';
import { HeaderComponent } from '@app/shared/custom-components/header/header.component';
import { SubmissionConfirmationComponent, SubmissionItem } from '@app/shared/custom-components/submission-confirmation/submission-confirmation.component';
import {
  formatTime,
  revertFormatDate,
} from '@app/shared/helpers/date-helpers';

interface ReservationStatusView {
  class: string;
  label: string;
}

type SubmissionFilter = 'all' | 'submitted' | 'not-submitted';
type SubmissionState = Exclude<SubmissionFilter, 'all'> | 'unavailable';

@Component({
  selector: 'app-reservation-dashboard',
  standalone: true,
  imports: [
    HeaderComponent,
    RouterLink,
    CardReservationComponent,
    MakeReservationComponent,
    ReservationConfirmedComponent,
    FeedbackWindowComponent,
    SubmissionConfirmationComponent,
  ],
  templateUrl: './reservation-dashboard.component.html',
  styleUrl: './reservation-dashboard.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ReservationDashboardComponent implements OnInit {
  private readonly authenticationService =
    inject(AuthenticationService);

  private readonly reservationService =
    inject(ReservationService);

  private readonly tableService = inject(TableService);
  private readonly locationsService = inject(LocationsService);
  private readonly ordersService = inject(PreOrdersService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  private reservationsLoadSubscription?: Subscription;

  readonly Roles = Roles;

  readonly firstName = signal('');
  readonly lastName = signal('');
  readonly role = signal<string>(Roles.CUSTOMER);
  readonly customerId = signal('');

  readonly reservations = signal<ReservationCustomer[]>([]);
  readonly reservationsLoaded = signal(false);
  readonly reservationDateFilter = signal('');
  readonly submissionStatusFilter = signal<SubmissionFilter>('all');
  readonly guestCountFilter = signal<number | null>(null);
  readonly hideCancelled = signal(false);
  readonly orderStatusUnavailable = signal(false);

  readonly guestCountOptions = computed(() =>
    [...new Set(this.reservations().map(item => item.guestCount))]
      .filter(count => Number.isInteger(count) && count > 0)
      .sort((a, b) => a - b)
  );
  readonly hasActiveFilters = computed(() =>
    !!this.reservationDateFilter() || this.submissionStatusFilter() !== 'all' ||
    this.guestCountFilter() !== null || this.hideCancelled()
  );
  readonly filteredReservations = computed(() => {
    const date = this.reservationDateFilter();
    const status = this.submissionStatusFilter();
    const guests = this.guestCountFilter();
    const hideCancelled = this.hideCancelled();
    return this.reservations().filter(reservation =>
      (!date || this.localReservationDate(reservation.startTime) === date) &&
      (status === 'all' || this.submissionState(reservation) === status) &&
      (guests === null || reservation.guestCount === guests) &&
      (!hideCancelled || reservation.status !== ReservationStatusCustomer.Cancelled)
    );
  });
  readonly submissionLoadingId = signal<string | null>(null);
  readonly submissionOrder = signal<Order | null>(null);
  readonly submittingOrder = signal(false);
  readonly submissionError = signal('');
  readonly submissionItems = computed<SubmissionItem[]>(() =>
    this.submissionOrder()?.orderItems.map(item => ({
      name: item.dish.name,
      quantity: item.quantity,
      unitPrice: item.dish.price,
    })) ?? []
  );

  readonly selectedReservation =
    signal<ReservationCustomer | null>(null);

  readonly selectedTable = signal<Table | null>(null);
  readonly selectedTimeRange = signal<TimeRange | null>(null);

  readonly showEditReservationModal = signal(false);
  readonly showEditReservationConfirmed = signal(false);
  readonly showFeedbackModal = signal(false);

  readonly statusEnum: Record<number, ReservationStatusView> = {
    [ReservationStatusCustomer.Reserved]: {
      class: 'reserved',
      label: 'Reserved',
    },
    [ReservationStatusCustomer.Cancelled]: {
      class: 'canceled',
      label: 'Cancelled',
    },
    [ReservationStatusCustomer.InProgress]: {
      class: 'in-progress',
      label: 'In Progress',
    },
    [ReservationStatusCustomer.Finished]: {
      class: 'finished',
      label: 'Finished',
    },
  };

  readonly selectedReservationAsReservation =
    computed<Reservation | null>(() => {
      const reservation = this.selectedReservation();

      if (!reservation) {
        return null;
      }

      return {
        id: reservation.id,
        tableId: reservation.tableId,
        startTime: reservation.startTime,
        endTime: reservation.endTime,
        guestCount: reservation.guestCount,
        status:
          reservation.status as unknown as ReservationStatus,
        waiterId: reservation.waiterId ?? '',
        customerId:
          reservation.customerId ?? this.customerId(),
        locationAddress: reservation.locationAddress,
        restaurantName: reservation.restaurantName,
        tableCapacity:
          this.selectedTable()?.capacity ??
          reservation.guestCount,
        hasSubmittedPreOrder:
        reservation.hasSubmittedPreOrder,
        preOrder: reservation.preOrder,
        preOrders: reservation.preOrders,
      };
    });

  submissionState(reservation: ReservationCustomer): SubmissionState {
    if (this.orderStatusUnavailable()) return 'unavailable';
    return reservation.preOrder?.status === 1 || reservation.preOrder?.status === 3
      ? 'submitted' : 'not-submitted';
  }

  setSubmissionStatusFilter(value: string): void {
    this.submissionStatusFilter.set(
      value === 'submitted' || value === 'not-submitted' ? value : 'all'
    );
  }

  setGuestCountFilter(value: string): void {
    const count = Number(value);
    this.guestCountFilter.set(Number.isInteger(count) && count > 0 ? count : null);
  }

  clearFilters(): void {
    this.reservationDateFilter.set('');
    this.submissionStatusFilter.set('all');
    this.guestCountFilter.set(null);
    this.hideCancelled.set(false);
  }

  private localReservationDate(startTime: Date): string {
    const date = new Date(startTime);
    if (!Number.isFinite(date.getTime())) return '';
    return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`;
  }

  ngOnInit(): void {
    const token = this.authenticationService.getToken();
    const user = this.authenticationService.getUser();

    if (!token || !user) {
      this.reservationsLoaded.set(true);
      void this.router.navigate(['/login']);
      return;
    }

    if (user.role === Roles.WAITER) {
      void this.router.navigate(['/waiter-reservations']);
      return;
    }

    if (user.role !== Roles.CUSTOMER) {
      void this.router.navigate(['/main']);
      return;
    }

    this.firstName.set(user.firstName ?? '');
    this.lastName.set(user.lastName ?? '');
    this.role.set(user.role);
    this.customerId.set(user.id);

    this.loadReservations();
  }

  openSubmission(reservationId: string): void {
    const reservation = this.reservations().find(item => item.id === reservationId);
    if (!reservation || !this.canSubmitReservation(reservation) ||
        !reservation.preOrder?.orderItems?.length || this.submissionLoadingId() ||
        this.submissionOrder() || this.submittingOrder()) return;

    this.submissionError.set('');
    this.submissionLoadingId.set(reservationId);
    this.ordersService.getByReservationId(reservationId)
      .pipe(
        finalize(() => this.submissionLoadingId.set(null)),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: order => {
          const currentReservation = this.reservations().find(item => item.id === reservationId);
          if (!currentReservation || !this.canSubmitReservation(currentReservation) ||
              !this.isDraftForReservation(order, reservationId)) {
            this.rejectStaleSubmission();
            return;
          }
          this.submissionOrder.set(order);
        },
        error: () => this.rejectStaleSubmission(),
      });
  }

  closeSubmission(): void {
    if (!this.submittingOrder()) this.submissionOrder.set(null);
  }

  confirmSubmission(): void {
    const selected = this.submissionOrder();
    if (!selected || this.submittingOrder()) return;
    const reservation = this.reservations().find(item => item.id === selected.reservationId);
    if (!reservation || !this.canSubmitReservation(reservation)) {
      this.rejectStaleSubmission();
      return;
    }

    this.submittingOrder.set(true);
    forkJoin({
      reservation: this.reservationService.getReservationById(selected.reservationId),
      order: this.ordersService.getByReservationId(selected.reservationId),
    }).pipe(
      switchMap(({ reservation: freshReservation, order }) => {
        if (!freshReservation || !this.canSubmitReservation(freshReservation) ||
            !this.isDraftForReservation(order, selected.reservationId) ||
            !this.sameSubmissionItems(selected, order)) {
          return throwError(() => new Error('Pre-order changed before submission.'));
        }
        return this.ordersService.submitPreOrderByReservationId(selected.reservationId);
      }),
      finalize(() => this.submittingOrder.set(false)),
      takeUntilDestroyed(this.destroyRef),
    ).subscribe({
      next: () => {
        this.submissionOrder.set(null);
        this.submissionError.set('');
        this.reservations.update(items => items.map(item =>
          item.id === selected.reservationId
            ? { ...item, preOrder: item.preOrder ? { ...item.preOrder, status: 3 } : undefined,
                hasSubmittedPreOrder: true }
            : item
        ));
        this.loadReservations();
      },
      error: () => this.rejectStaleSubmission(),
    });
  }

  private canSubmitReservation(reservation: ReservationCustomer): boolean {
    const cutoff = new Date(reservation.startTime).getTime() - 30 * 60_000;
    return reservation.guestCount === 1 &&
      reservation.status === ReservationStatusCustomer.Reserved &&
      reservation.customerId === this.customerId() &&
      Number.isFinite(cutoff) && Date.now() <= cutoff;
  }

  private isDraftForReservation(order: Order | null, reservationId: string): order is Order {
    return !!order && order.reservationId === reservationId && order.status === 0 &&
      Array.isArray(order.orderItems) && order.orderItems.length > 0 &&
      order.orderItems.every(item => Number.isInteger(item.quantity) && item.quantity > 0 &&
        !!item.dish?.id && typeof item.dish.name === 'string' &&
        Number.isFinite(item.dish.price));
  }

  private sameSubmissionItems(first: Order, second: Order): boolean {
    return first.orderId === second.orderId &&
      JSON.stringify(first.orderItems.map(item => [item.dish.id, item.dish.name, item.dish.price, item.quantity])) ===
      JSON.stringify(second.orderItems.map(item => [item.dish.id, item.dish.name, item.dish.price, item.quantity]));
  }

  private rejectStaleSubmission(): void {
    this.submissionOrder.set(null);
    this.submissionError.set('This pre-order is no longer ready to submit. Review your reservation and try again.');
    this.loadReservations();
  }

  cancelReservation(id: string): void {
    this.reservationService
      .cancelReservation$(id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (response) => {
          if (response.error) {
            console.error(
              'Failed to cancel reservation:',
              response.error,
            );
            return;
          }

          this.reservations.update((reservations) =>
            reservations.map((reservation) =>
              reservation.id === id
                ? {
                  ...reservation,
                  status:
                  ReservationStatusCustomer.Cancelled,
                }
                : reservation,
            ),
          );

          const selected = this.selectedReservation();

          if (selected?.id === id) {
            this.selectedReservation.set({
              ...selected,
              status: ReservationStatusCustomer.Cancelled,
            });
          }
        },
        error: (error) => {
          console.error(
            'Failed to cancel reservation:',
            error,
          );
        },
      });
  }

  openEditModal(reservation: ReservationCustomer): void {
    this.selectedReservation.set(reservation);

    const startTime = new Date(reservation.startTime);
    const date = revertFormatDate(startTime.toISOString());
    const time = formatTime(startTime.toISOString());

    this.locationsService
      .getLocations()
      .pipe(
        map((locations) =>
          locations.find(
            (location) =>
              location.address.trim().toLowerCase() ===
              reservation.locationAddress
                .trim()
                .toLowerCase(),
          ),
        ),
        switchMap((location) => {
          if (!location) {
            console.error(
              'Location not found:',
              reservation.locationAddress,
            );

            return of(null);
          }

          return forkJoin({
            timeRanges: this.tableService.getTableTimeSlots(
              reservation.tableId,
              date,
              time,
            ),
            tables:
              this.tableService.getAvailableTablesWithoutStartTime$(
                {
                  location: location.id,
                  date: null,
                  time: null,
                  guests: reservation.guestCount,
                },
              ),
          }).pipe(
            map(({ timeRanges, tables }) => ({
              locationId: location.id,
              timeRanges,
              tables,
            })),
          );
        }),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: (result) => {
          if (!result) {
            return;
          }

          const matchingTable = result.tables.find(
            (table) => table.id === reservation.tableId,
          );

          this.selectedTable.set({
            id: reservation.tableId,
            tableNumber:
              matchingTable?.tableNumber ??
              this.extractTableNumber(
                reservation.tableName,
              ),
            capacity:
              matchingTable?.capacity ??
              reservation.guestCount,
            locationId:
              matchingTable?.locationId ??
              result.locationId,
            timeRanges: result.timeRanges ?? [],
          });

          this.selectedTimeRange.set({
            start: new Date(
              reservation.startTime,
            ).toISOString(),
            end: new Date(reservation.endTime).toISOString(),
          });

          this.showEditReservationModal.set(true);
        },
        error: (error) => {
          console.error(
            'Failed to load reservation editing data:',
            error,
          );
        },
      });
  }

  closeEditReservationModal(): void {
    this.showEditReservationModal.set(false);
  }

  onSubmitEditReservation(
    timeRange: TimeRange,
    guests: number,
  ): void {
    const currentReservation = this.selectedReservation();

    if (!currentReservation) {
      return;
    }

    this.reservationService
      .editReservation$(
        currentReservation.id,
        timeRange,
        guests,
      )
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (updatedReservation) => {
          const updatedCustomerReservation: ReservationCustomer =
            {
              ...currentReservation,
              ...updatedReservation,
              status:
                updatedReservation.status as unknown as ReservationStatusCustomer,
              restaurantName:
                updatedReservation.restaurantName ??
                currentReservation.restaurantName ??
                '',
              feedback: currentReservation.feedback,
              preOrder: currentReservation.preOrder,
              preOrders: currentReservation.preOrders,
              createdByName:
              currentReservation.createdByName,
              createdByRole:
              currentReservation.createdByRole,
            };

          this.reservations.update((reservations) =>
            reservations.map((reservation) =>
              reservation.id ===
              updatedCustomerReservation.id
                ? updatedCustomerReservation
                : reservation,
            ),
          );

          this.selectedReservation.set(
            updatedCustomerReservation,
          );

          this.selectedTimeRange.set({
            start: new Date(
              updatedCustomerReservation.startTime,
            ).toISOString(),
            end: new Date(
              updatedCustomerReservation.endTime,
            ).toISOString(),
          });

          this.showEditReservationModal.set(false);
          this.showEditReservationConfirmed.set(true);
        },
        error: (error) => {
          console.error(
            'Failed to update reservation:',
            error,
          );
        },
      });
  }

  closeReservationConfirmed(): void {
    this.showEditReservationConfirmed.set(false);
  }

  editConfirmedReservation(): void {
    const reservation = this.selectedReservation();

    if (!reservation) {
      return;
    }

    this.showEditReservationConfirmed.set(false);
    this.openEditModal(reservation);
  }

  cancelConfirmedReservation(): void {
    const reservation = this.selectedReservation();

    if (!reservation) {
      return;
    }

    this.cancelReservation(reservation.id);
    this.showEditReservationConfirmed.set(false);
  }

  openFeedback(reservation: ReservationCustomer): void {
    this.selectedReservation.set(reservation);
    this.showFeedbackModal.set(true);
  }

  closeFeedback(): void {
    this.showFeedbackModal.set(false);
  }

  onFeedbackUpdated(event: FeedbackUpdatedEvent): void {
    this.reservationService
      .getReservationFeedback(event.reservationId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (feedback) => {
          this.reservations.update((reservations) =>
            reservations.map((reservation) =>
              reservation.id === event.reservationId
                ? {
                  ...reservation,
                  feedback: feedback ?? undefined,
                }
                : reservation,
            ),
          );

          const selected = this.selectedReservation();

          if (selected?.id === event.reservationId) {
            this.selectedReservation.set({
              ...selected,
              feedback: feedback ?? undefined,
            });
          }
        },
        error: (error) => {
          console.error(
            'Failed to refresh reservation feedback:',
            error,
          );
        },
      });
  }

  private loadReservations(): void {
    this.reservationsLoadSubscription?.unsubscribe();
    this.reservationsLoaded.set(false);
    this.orderStatusUnavailable.set(false);

    this.reservationsLoadSubscription = forkJoin({
      reservations:
        this.reservationService.getReservationsForUser$(),
      orders: this.ordersService.getPreOrders().pipe(
        catchError((error) => {
          this.orderStatusUnavailable.set(true);
          console.error('Failed to load pre-orders:', error);
          return of([]);
        }),
      ),
    })
      .pipe(
        switchMap(({ reservations, orders }) => {
          if (reservations.length === 0) {
            return of<ReservationCustomer[]>([]);
          }

          return from(reservations.map((reservation, index) => ({ reservation, index }))).pipe(
            mergeMap(({ reservation, index }) =>
              this.reservationService.getReservationFeedback(reservation.id).pipe(
                catchError((error: HttpErrorResponse) => {
                  if (error.status !== 404) {
                    console.error(
                      `Failed to load feedback for reservation ${reservation.id}:`,
                      error,
                    );
                  }

                  return of(null);
                }),
                map((feedback): { index: number; reservation: ReservationCustomer } => {
                  const preOrder = orders.find(
                    (order) => order.reservationId === reservation.id && order.status !== 2,
                  );
                  const hasFeedback =
                    feedback?.service != null || feedback?.cuisineExperience != null;

                  return {
                    index,
                    reservation: {
                      ...reservation,
                      status: reservation.status as unknown as ReservationStatusCustomer,
                      restaurantName: reservation.restaurantName ?? '',
                      feedback: hasFeedback ? feedback ?? undefined : undefined,
                      preOrder,
                      hasSubmittedPreOrder:
                        preOrder != null && (preOrder.status === 1 || preOrder.status === 3),
                    },
                  };
                }),
              ),
              3,
            ),
            toArray(),
            map(items => items.sort((left, right) => left.index - right.index)
              .map(item => item.reservation)),
          );
        }),
        finalize(() => {
          this.reservationsLoaded.set(true);
        }),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: (reservations) => {
          this.reservations.set(reservations);
        },
        error: (error) => {
          console.error(
            'Failed to load reservations:',
            error,
          );
          this.reservations.set([]);
        },
      });
  }

  private extractTableNumber(
    tableName: string | undefined,
  ): number {
    const match = tableName?.match(/\d+/);
    return match ? Number(match[0]) : 0;
  }
}
