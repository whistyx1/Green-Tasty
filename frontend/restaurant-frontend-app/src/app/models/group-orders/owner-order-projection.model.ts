import { ParticipantItem } from './participant.model';

/** Local read-model projection, not an extra field required from the sessions API. */
export interface OwnerOrderProjection {
  items: ParticipantItem[];
  total: number;
  editable?: boolean;
}

/** Header fields supplied by GET /reservations/{reservationId}. */
export interface GroupReservationHeader {
  id: string;
  startTime: string;
  endTime: string;
  guestCount: number;
  locationAddress: string;
  restaurantName: string;
}
