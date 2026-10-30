/** GET /reservations/{reservationId}/guest-session-id */
export interface OwnerSessionResponse {
  guestSessionId: string;
  /** Additive Owner lookup context; absent in the original EPAM demo response. */
  groupReservationId?: string;
}
