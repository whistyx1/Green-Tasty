import { CurrencyPipe } from '@angular/common';
import { CdkTrapFocus } from '@angular/cdk/a11y';
import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { ModalComponent } from '../modal/modal.component';

export interface SubmissionItem {
  name: string;
  quantity: number;
  unitPrice: number;
}

export type SubmissionKind = 'ordinary' | 'guest' | 'group';

@Component({
  selector: 'app-submission-confirmation',
  imports: [CurrencyPipe, CdkTrapFocus, ModalComponent],
  templateUrl: './submission-confirmation.component.html',
  styleUrl: './submission-confirmation.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SubmissionConfirmationComponent {
  readonly kind = input.required<SubmissionKind>();
  readonly items = input.required<readonly SubmissionItem[]>();
  readonly busy = input(false);
  readonly pendingGuests = input(0);
  readonly closed = output<void>();
  readonly confirmed = output<void>();

  readonly total = computed(() => this.items().reduce(
    (sum, item) => sum + item.unitPrice * item.quantity, 0
  ));
  readonly title = computed(() => ({
    ordinary: 'Submit Pre-Order',
    guest: 'Submit Your Group Pre-Order',
    group: 'Finalize Group Pre-Order',
  })[this.kind()]);
  readonly warning = computed(() => ({
    ordinary: 'You can unsubmit and edit this pre-order until 30 minutes before your reservation starts. You can also cancel it.',
    guest: 'Once submitted, your guest pre-order cannot be modified through the app.',
    group: 'This finalizes the group order for the restaurant. Pending participant carts are not included yet; if someone submits before finalization, their dishes may be added. The group pre-order cannot be changed afterward.',
  })[this.kind()]);

  close(): void {
    if (!this.busy()) this.closed.emit();
  }

  confirm(): void {
    if (!this.busy() && this.items().length > 0) this.confirmed.emit();
  }
}
