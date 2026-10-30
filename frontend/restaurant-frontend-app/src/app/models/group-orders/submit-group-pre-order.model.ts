import { DishShort } from './participant.model';

/** POST /group-reservations/{groupReservationId}/submit — request body */
export interface SubmitGroupPreOrderRequest {
  reservationId: string;
}

/** POST /group-reservations/{groupReservationId}/submit — response item */
export interface SubmittedDish {
  dishId: string;
  quantity: number;
  /** Optional local enrichment from the final persisted order, not required by POST. */
  dish?: DishShort;
  /** POST succeeded but the one final-order read failed. */
  summaryReadFailed?: boolean;
}
