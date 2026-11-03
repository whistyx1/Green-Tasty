import { Params } from '@angular/router';
import { Reservation } from '@app/models/reservations/reservation.model';

type PreOrderReservation = Pick<
  Reservation,
  'id' | 'tableId' | 'locationAddress' | 'startTime' | 'endTime'
> & { tableName?: string };

export function preOrderMenuQueryParams(reservation: PreOrderReservation): Params {
  const startTime = new Date(reservation.startTime);
  const endTime = new Date(reservation.endTime);
  const tableLabel = reservation.tableName || `Table ${reservation.tableId}`;

  return {
    preOrder: 'true',
    reservationId: reservation.id,
    reservationLabel: [
      reservation.locationAddress,
      tableLabel,
      startTime.toDateString(),
      `${startTime.toLocaleTimeString()} - ${endTime.toLocaleTimeString()}`,
    ].join(', '),
  };
}
