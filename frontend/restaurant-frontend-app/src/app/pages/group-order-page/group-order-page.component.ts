import { CurrencyPipe, DatePipe } from '@angular/common';
import { CdkTrapFocus } from '@angular/cdk/a11y';
import { HttpErrorResponse } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  inject,
  OnInit,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute } from '@angular/router';
import { distinctUntilChanged, finalize, of, interval, map, Subscription, switchMap } from 'rxjs';
import { ParticipantCardComponent } from '@app/components/group-orders/participant-card/participant-card.component';
import { SubmittedDish } from '@app/models/group-orders/submit-group-pre-order.model';
import { GroupStatusResponse } from '@app/models/group-orders/group-status.model';
import { Participant } from '@app/models/group-orders/participant.model';
import { GroupSessionsResponse } from '@app/models/group-orders/group-sessions-response.model';
import { OwnerOrderProjection } from '@app/models/group-orders/owner-order-projection.model';
import { GuestInvite } from '@app/models/reservations/guest-invite.model';
import { GroupOrdersService } from '@app/services/group-orders/group-orders.service';
import { ToastService } from '@app/services/toast/toast.service';
import { ModalComponent } from '@app/shared/custom-components/modal/modal.component';
import { consolidatePreOrder } from '@app/shared/helpers/consolidate-pre-order';
import { getServerErrorMessage } from '@app/shared/helpers/http-error-helpers';

@Component({
  selector: 'app-group-order-page',
  imports: [CurrencyPipe, DatePipe, CdkTrapFocus, ParticipantCardComponent, ModalComponent],
  templateUrl: './group-order-page.component.html',
  styleUrl: './group-order-page.component.scss',
  changeDetection: ChangeDetectionStrategy.Eager,
})
export class GroupOrderPageComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly service = inject(GroupOrdersService);
  private readonly toast = inject(ToastService);
  private readonly destroyRef = inject(DestroyRef);
  private loadSubscription?: Subscription;
  private reservationId = '';
  private pollSubscription?: Subscription;
  private refreshSubscription?: Subscription;
  private mutationSubscription?: Subscription;

  readonly invite = signal<GuestInvite | null>(null);
  private readonly rawParticipants = signal<Participant[]>([]);
  readonly ownerPreOrder = signal<OwnerOrderProjection | null>(null);
  readonly submittedOrder = signal<OwnerOrderProjection | null>(null);
  readonly participants = computed(() => {
    const people = this.rawParticipants();
    const ownOrder = this.ownerPreOrder();
    if (!ownOrder) return people;
    const ownerId = this.ownerSessionId();
    const ownerExists = people.some((person) => person.id === ownerId);
    const rows = ownerExists
      ? people
      : [
          ...people,
          {
            id: ownerId,
            type: 'Owner' as const,
            name: 'You',
            isSubmitted: false,
            total: 0,
            items: [],
          },
        ];
    return rows.map((person) =>
      person.id !== ownerId
        ? person
        : {
            ...person,
            items: [...person.items, ...ownOrder.items],
            total: person.total + ownOrder.total,
          }
    );
  });
  readonly ownerSessionId = signal('');
  readonly groupStatus = signal<GroupStatusResponse | null>(null);
  readonly loading = signal(true);
  readonly loadError = signal('');
  readonly submitting = signal(false);
  readonly confirmationOpen = signal(false);
  readonly submitError = signal('');
  readonly refreshing = signal(false);
  readonly updatedAt = signal<Date | null>(null);
  readonly refreshError = signal(false);
  readonly removing = signal(false);
  readonly removeTarget = signal<Participant | null>(null);
  readonly removeError = signal('');
  readonly submittedSummaryIncomplete = signal(false);

  readonly consolidated = computed(() => {
    const finalOrder = this.submittedOrder();
    if (finalOrder) return finalOrder;
    const ownOrder = this.ownerPreOrder();
    const included = ownOrder
      ? [
          ...this.rawParticipants(),
          {
            id: 'ordinary-owner-order',
            name: 'Saved owner preorder',
            isSubmitted: true,
            items: ownOrder.items,
          },
        ]
      : this.rawParticipants();
    return consolidatePreOrder(included);
  });
  readonly status = computed(() => this.groupStatus()?.status ?? 'Open');
  readonly statusLabel = computed(() => {
    switch (this.status()) {
      case 'Submitted':
        return 'Sent to Restaurant';
      case 'Closed':
        return 'Closed';
      default:
        return 'Collecting pre-orders';
    }
  });
  readonly guestCount = computed(
    () => this.groupStatus()?.currentParticipants ?? this.participants().length
  );
  readonly capacity = computed(
    () => this.groupStatus()?.maxCapacity ?? this.invite()?.MaxGuestCount ?? 0
  );
  readonly pendingCount = computed(
    () =>
      this.rawParticipants().filter(
        (person) =>
          !person.isSubmitted &&
          !(
            this.ownerPreOrder() &&
            person.id === this.ownerSessionId() &&
            person.items.length === 0
          )
      ).length
  );
  readonly pendingMessage = computed(() => {
    const count = this.pendingCount();
    return count === 1
      ? "1 participant hasn't submitted their cart yet — their dishes won't be included in the order"
      : `${count} participants haven't submitted their cart yet — their dishes won't be included in the order`;
  });
  readonly empty = computed(
    () =>
      this.participants().every((person) => person.items.length === 0) &&
      !this.submittedOrder()?.items.length &&
      !this.submittedSummaryIncomplete()
  );
  readonly canSubmit = computed(
    () =>
      !this.loading() &&
      !this.loadError() &&
      !this.submitting() &&
      !this.removing() &&
      this.status() === 'Open' &&
      this.consolidated().items.length > 0 &&
      this.ownerPreOrder()?.editable !== false &&
      (!this.ownerPreOrder() ||
        this.rawParticipants().some((person) => person.isSubmitted && person.items.length > 0))
  );

  ngOnInit(): void {
    // Actual routes emit on parameter changes; snapshot-only contexts are useful
    // for embedded/tests and still receive the same initial loading behavior.
    if (this.route.paramMap) {
      this.route.paramMap
        .pipe(
          map((params) => params.get('reservationId') ?? ''),
          distinctUntilChanged(),
          takeUntilDestroyed(this.destroyRef)
        )
        .subscribe((reservationId) => this.changeReservation(reservationId));
    } else {
      this.changeReservation(this.route.snapshot.paramMap.get('reservationId') ?? '');
    }
  }

  private changeReservation(reservationId: string): void {
    this.mutationSubscription?.unsubscribe();
    this.submitting.set(false);
    this.removing.set(false);
    this.reservationId = reservationId;
    this.invite.set(null);
    this.rawParticipants.set([]);
    this.ownerPreOrder.set(null);
    this.submittedOrder.set(null);
    this.submittedSummaryIncomplete.set(false);
    this.groupStatus.set(null);
    this.ownerSessionId.set('');
    this.updatedAt.set(null);
    this.load();
  }

  load(): void {
    if (this.submitting() || this.removing()) return;
    this.stopPolling();
    this.cancelRefresh();
    this.loadSubscription?.unsubscribe();
    this.confirmationOpen.set(false);
    this.loadError.set('');
    this.submitError.set('');
    this.removeError.set('');
    this.removeTarget.set(null);
    this.submittedSummaryIncomplete.set(false);
    this.refreshError.set(false);
    this.loading.set(true);
    if (!this.reservationId) {
      this.loadError.set('Group reservation not found');
      this.loading.set(false);
      return;
    }
    this.loadSubscription = this.service
      .getOwnerSessionId$(this.reservationId)
      .pipe(
        switchMap((ownerId) =>
          this.service
            .getGroupInvite$(this.reservationId)
            .pipe(
              switchMap((invite) =>
                this.readSnapshot(invite.GroupReservationId).pipe(
                  map((response) => ({ ...response, invite, ownerId }))
                )
              )
            )
        ),
        takeUntilDestroyed(this.destroyRef),
        finalize(() => this.loading.set(false))
      )
      .subscribe({
        next: ({ invite, ownerId, sessions, status }) => {
          this.invite.set(invite);
          this.ownerSessionId.set(ownerId);
          this.applySnapshot(sessions);
          this.groupStatus.set(status);
          this.updatedAt.set(new Date());
          this.startPolling();
        },
        error: (error: unknown) => {
          this.loadError.set(
            error instanceof HttpErrorResponse && error.status === 404
              ? 'Group reservation not found'
              : "Couldn't load the group pre-order. Please try again."
          );
        },
      });
  }

  private readSnapshot(groupId: string, silent = false) {
    const request = silent
      ? this.service.getSessions$(groupId, { silent: true })
      : this.service.getSessions$(groupId);
    return request.pipe(
      switchMap((sessions) => {
        if (sessions.groupStatus) return of({ sessions, status: sessions.groupStatus });
        const status = silent
          ? this.service.getStatus$(groupId, { silent: true })
          : this.service.getStatus$(groupId);
        return status.pipe(map((value) => ({ sessions, status: value })));
      })
    );
  }

  private applySubmittedResponse(response: SubmittedDish[]): void {
    const known = new Map(
      this.participants()
        .flatMap((person) => person.items)
        .map((item) => [item.dish.id, item.dish])
    );
    const items = response.flatMap((item) => {
      const dish = item.dish ?? known.get(item.dishId);
      return dish ? [{ dish, quantity: item.quantity }] : [];
    });
    this.submittedSummaryIncomplete.set(
      items.length !== response.length || response.some((item) => item.summaryReadFailed)
    );
    this.submittedOrder.set({
      items,
      total: items.reduce((sum, item) => sum + item.dish.price * item.quantity, 0),
    });
  }

  private applySnapshot(snapshot: GroupSessionsResponse): void {
    this.rawParticipants.set(snapshot.participants);
    this.ownerPreOrder.set(snapshot.ownerPreOrder ?? null);
    this.submittedOrder.set(snapshot.submittedOrder ?? null);
    this.submittedSummaryIncomplete.set(snapshot.finalSummaryPending ?? false);
  }

  private startPolling(): void {
    this.stopPolling();
    if (this.status() !== 'Open') return;
    this.pollSubscription = interval(15_000)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.refresh());
  }

  private stopPolling(): void {
    this.pollSubscription?.unsubscribe();
    this.pollSubscription = undefined;
  }

  private cancelRefresh(): void {
    this.refreshSubscription?.unsubscribe();
    this.refreshSubscription = undefined;
  }

  refresh(): void {
    const invite = this.invite();
    if (
      !invite ||
      this.loading() ||
      this.refreshing() ||
      this.submitting() ||
      this.removing() ||
      this.status() !== 'Open'
    )
      return;
    this.refreshing.set(true);
    this.refreshSubscription = this.readSnapshot(invite.GroupReservationId, true)
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => this.refreshing.set(false))
      )
      .subscribe({
        next: ({ sessions, status }) => {
          this.applySnapshot(sessions);
          this.groupStatus.set(status);
          this.updatedAt.set(new Date());
          this.refreshError.set(false);
          if (status.status !== 'Open') {
            this.stopPolling();
            this.confirmationOpen.set(false);
            this.removeTarget.set(null);
          }
        },
        error: () => this.refreshError.set(true),
      });
  }

  openRemoval(participant: Participant): void {
    if (
      participant.type !== 'Guest' ||
      this.status() !== 'Open' ||
      this.submitting() ||
      this.removing() ||
      this.confirmationOpen()
    )
      return;
    this.removeError.set('');
    this.removeTarget.set(participant);
  }

  closeRemoval(): void {
    if (!this.removing()) this.removeTarget.set(null);
  }

  removeGuest(): void {
    const target = this.removeTarget();
    const invite = this.invite();
    if (
      !target ||
      !invite ||
      target.type !== 'Guest' ||
      this.status() !== 'Open' ||
      this.removing() ||
      this.submitting()
    )
      return;
    this.removing.set(true);
    this.stopPolling();
    this.cancelRefresh();
    this.mutationSubscription = this.service
      .removeSession$(invite.GroupReservationId, target.id)
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => this.removing.set(false))
      )
      .subscribe({
        next: (snapshot) => {
          this.applySnapshot(snapshot);
          const status = this.groupStatus();
          if (status)
            this.groupStatus.set({
              ...status,
              currentParticipants:
                snapshot.participants.filter((person) => person.type === 'Guest').length + 1,
            });
          this.updatedAt.set(new Date());
          this.refreshError.set(false);
          this.removeTarget.set(null);
          this.startPolling();
        },
        error: (error: unknown) => {
          this.removeError.set(
            error instanceof HttpErrorResponse
              ? getServerErrorMessage(error) || "Couldn't remove the guest. Please try again."
              : "Couldn't remove the guest. Please try again."
          );
          this.removeTarget.set(null);
          this.startPolling();
        },
      });
  }

  openConfirmation(): void {
    if (!this.canSubmit() || this.removeTarget()) return;
    this.submitError.set('');
    this.confirmationOpen.set(true);
  }

  closeConfirmation(): void {
    if (!this.submitting()) this.confirmationOpen.set(false);
  }

  submit(): void {
    const invite = this.invite();
    if (!invite || !this.confirmationOpen() || !this.canSubmit()) return;
    this.submitting.set(true);
    this.stopPolling();
    this.cancelRefresh();
    this.submitError.set('');
    this.mutationSubscription = this.service
      .submit$(invite.GroupReservationId, this.reservationId)
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => this.submitting.set(false))
      )
      .subscribe({
        next: (response) => {
          this.applySubmittedResponse(response);
          const status = this.groupStatus();
          if (status) this.groupStatus.set({ ...status, status: 'Submitted' });
          this.confirmationOpen.set(false);
          this.toast.success('Pre-order sent to the restaurant');
        },
        error: (error: unknown) => {
          // The existing HTTP interceptor owns the API-error toast.
          this.submitError.set(
            error instanceof HttpErrorResponse
              ? getServerErrorMessage(error) || "Couldn't send the pre-order. Please try again."
              : "Couldn't send the pre-order. Please try again."
          );
          this.confirmationOpen.set(false);
          this.startPolling();
        },
      });
  }
}
