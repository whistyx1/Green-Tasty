import { HttpClient, HttpContext, HttpErrorResponse } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { catchError, forkJoin, map, Observable, of, switchMap, tap, throwError } from 'rxjs';
import { SKIP_ERROR_TOAST } from '@app/auth/skip-error-toast.context';
import { GroupOrderContext } from '@app/models/group-orders/group-order-context.model';
import { GroupSessionsResponse } from '@app/models/group-orders/group-sessions-response.model';
import { GroupStatusResponse } from '@app/models/group-orders/group-status.model';
import { OwnerSessionResponse } from '@app/models/group-orders/owner-session.model';
import {
  GroupReservationHeader,
  OwnerOrderProjection,
} from '@app/models/group-orders/owner-order-projection.model';
import {
  SubmittedDish,
  SubmitGroupPreOrderRequest,
} from '@app/models/group-orders/submit-group-pre-order.model';
import { Order } from '@app/models/orders/order.model';
import { GuestInvite } from '@app/models/reservations/guest-invite.model';
import { GuestInviteService } from '@app/services/reservation/guest-invite.service';
import { API_BASE_URL } from '@app/shared/config/api-base-url.token';

@Injectable({ providedIn: 'root' })
export class GroupOrdersService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);
  private readonly invites = inject(GuestInviteService);
  private readonly ownerLookups = new Map<string, OwnerSessionResponse>();
  private readonly groups = new Map<string, GroupOrderContext>();
  private readonly ownerOrders = new Map<string, OwnerOrderProjection>();
  private readonly removedGuests = new Map<string, Set<string>>();

  getGroupInvite$(reservationId: string): Observable<GuestInvite> {
    const owner = this.ownerLookups.get(reservationId);
    if (owner?.groupReservationId) {
      const groupId = owner.groupReservationId;
      return this.http
        .get<GroupReservationHeader>(
          `${this.baseUrl}/reservations/${encodeURIComponent(reservationId)}`
        )
        .pipe(
          map((reservation) => ({
            GroupReservationId: groupId,
            LocationAddress: reservation.locationAddress,
            RestaurantName: reservation.restaurantName,
            StartTime: reservation.startTime,
            EndTime: reservation.endTime,
            MaxGuestCount: reservation.guestCount,
            // These invitation-only fields are not supplied/used by the Owner header.
            LocationId: '',
            TableNumber: 0,
            InviterName: '',
          }))
        );
    }
    // Original EPAM responses remain supported without parsing opaque IDs.
    return this.invites
      .getInviteToken$(reservationId)
      .pipe(switchMap((response) => this.invites.getInvite$(response.token)));
  }

  getSessions$(
    groupId: string,
    options: { silent?: boolean } = {}
  ): Observable<GroupSessionsResponse> {
    const snapshot = this.http
      .get<GroupSessionsResponse>(
        `${this.baseUrl}/guest/groups/${encodeURIComponent(groupId)}/sessions`,
        { context: new HttpContext().set(SKIP_ERROR_TOAST, options.silent ?? false) }
      )
      .pipe(map((response) => this.excludeRemoved(groupId, response)));
    const context = this.groups.get(groupId);
    if (!context) return snapshot;
    return forkJoin({
      snapshot,
      order: this.http
        .get<Order>(`${this.baseUrl}/orders/${encodeURIComponent(context.reservationId)}`, {
          context: new HttpContext().set(SKIP_ERROR_TOAST, true),
        })
        .pipe(
          catchError((error: unknown) =>
            error instanceof HttpErrorResponse && error.status === 404
              ? of(null)
              : throwError(() => error)
          )
        ),
      status: this.getStatus$(groupId, options),
    }).pipe(
      map(({ snapshot: response, order, status }) => {
        this.ownerOrders.delete(groupId);
        if (!order || (status.status === 'Submitted' && order.status === 0)) {
          return {
            ...response,
            groupStatus: status,
            finalSummaryPending: status.status === 'Submitted',
          };
        }
        const projection: OwnerOrderProjection = {
          items: order.orderItems.map((item) => ({ dish: item.dish, quantity: item.quantity })),
          editable: order.status === 0,
          total: order.orderItems.reduce((sum, item) => sum + item.dish.price * item.quantity, 0),
        };
        if (status.status === 'Submitted' || (status.status === 'Closed' && order.status !== 0)) {
          return { ...response, groupStatus: status, submittedOrder: projection };
        }
        this.ownerOrders.set(groupId, projection);
        return { ...response, groupStatus: status, ownerPreOrder: projection };
      })
    );
  }

  getOwnerSessionId$(reservationId: string): Observable<string> {
    return this.http
      .get<OwnerSessionResponse>(
        `${this.baseUrl}/reservations/${encodeURIComponent(reservationId)}/guest-session-id`
      )
      .pipe(
        tap((response) => {
          this.ownerLookups.set(reservationId, response);
          if (response.groupReservationId)
            this.groups.set(response.groupReservationId, {
              reservationId,
              ownerSessionId: response.guestSessionId,
            });
        }),
        map((response) => response.guestSessionId)
      );
  }

  getStatus$(groupId: string, options: { silent?: boolean } = {}): Observable<GroupStatusResponse> {
    return this.http.get<GroupStatusResponse>(
      `${this.baseUrl}/guest/groups/${encodeURIComponent(groupId)}/status`,
      {
        context: new HttpContext().set(SKIP_ERROR_TOAST, options.silent ?? false),
      }
    );
  }

  removeSession$(groupId: string, guestSessionId: string): Observable<GroupSessionsResponse> {
    return this.http
      .delete<GroupSessionsResponse>(
        `${this.baseUrl}/guest/groups/${encodeURIComponent(groupId)}/sessions/${encodeURIComponent(guestSessionId)}`
      )
      .pipe(
        map((snapshot) => {
          // Keep the exact DELETE membership snapshot; do not reread a stale GSI.
          const removed = this.removedGuests.get(groupId) ?? new Set<string>();
          removed.add(guestSessionId);
          this.removedGuests.set(groupId, removed);
          const response = this.excludeRemoved(groupId, snapshot);
          const projection = this.ownerOrders.get(groupId);
          return projection ? { ...response, ownerPreOrder: projection } : response;
        })
      );
  }

  private excludeRemoved(groupId: string, snapshot: GroupSessionsResponse): GroupSessionsResponse {
    const removed = this.removedGuests.get(groupId);
    if (!removed?.size) return snapshot;
    const participants = snapshot.participants.filter((person) => !removed.has(person.id));
    return {
      ...snapshot,
      participants,
      total: participants.reduce((sum, person) => sum + person.total, 0),
    };
  }

  submit$(groupId: string, reservationId: string): Observable<SubmittedDish[]> {
    const body: SubmitGroupPreOrderRequest = { reservationId };
    return this.http
      .post<
        SubmittedDish[]
      >(`${this.baseUrl}/group-reservations/${encodeURIComponent(groupId)}/submit`, body)
      .pipe(
        switchMap((submitted) => {
          const context = this.groups.get(groupId);
          if (!context) return of(submitted);
          return this.http
            .get<Order>(`${this.baseUrl}/orders/${encodeURIComponent(context.reservationId)}`, {
              context: new HttpContext().set(SKIP_ERROR_TOAST, true),
            })
            .pipe(
              map((order) =>
                submitted.map((item) => {
                  const dish = order.orderItems.find(
                    (entry) => entry.dish.id === item.dishId
                  )?.dish;
                  return dish ? { ...item, dish } : item;
                })
              ),
              // POST already committed. A failed read must never offer mutation retry.
              catchError(() => of(submitted.map((item) => ({ ...item, summaryReadFailed: true }))))
            );
        })
      );
  }
}
