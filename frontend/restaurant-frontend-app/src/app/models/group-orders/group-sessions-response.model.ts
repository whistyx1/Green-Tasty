import { Participant } from './participant.model';
import { GroupStatusResponse } from './group-status.model';
import { OwnerOrderProjection } from './owner-order-projection.model';

/** GET /guest/groups/{groupReservationId}/sessions */
export interface GroupSessionsResponse {
  participants: Participant[];
  /** Сума ВСІХ учасників, включно з тими, хто ще не підтвердив кошик. */
  total: number;
  /** Ordinary Owner preorder before group submission; raw participants stay unchanged. */
  ownerPreOrder?: OwnerOrderProjection;
  /** Status used by the real read-model adapter for the same snapshot. */
  groupStatus?: GroupStatusResponse;
  /** Final persisted order after submission; must not be merged into guests again. */
  submittedOrder?: OwnerOrderProjection;
  /** Group is final but the order read has not caught up yet. */
  finalSummaryPending?: boolean;
}
