import { CurrencyPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, input, output, signal } from '@angular/core';
import { DishShort, Participant } from '@app/models/group-orders/participant.model';

@Component({
  selector: 'app-participant-card',
  imports: [CurrencyPipe],
  templateUrl: './participant-card.component.html',
  styleUrl: './participant-card.component.scss',
  changeDetection: ChangeDetectionStrategy.Eager,
})
export class ParticipantCardComponent {
  readonly participant = input.required<Participant>();
  readonly isMe = input(false);
  readonly canRemove = input(false);
  readonly ownerPreOrderSaved = input(false);
  readonly ownerOrderFinalized = input(false);
  readonly removeClick = output<Participant>();
  private readonly failedImages = signal<ReadonlySet<string>>(new Set());

  readonly title = computed(() => {
    const person = this.participant();
    if (this.isMe()) return 'My Pre-Order';
    return `${person.type === 'Owner' ? 'Owner' : 'Guest'}: ${person.name}`;
  });

  readonly cartStatus = computed(() =>
    this.ownerOrderFinalized()
      ? 'Included in sent order'
      : this.ownerPreOrderSaved()
        ? 'Saved pre-order'
        : this.participant().isSubmitted
          ? 'Submitted'
          : 'Still choosing'
  );

  readonly items = computed(() =>
    this.participant().items.map((item) => ({
      ...item,
      initial: item.dish.name.trim().charAt(0).toUpperCase(),
      showImage: !!item.dish.imageUrl && !this.failedImages().has(this.imageKey(item.dish)),
    }))
  );

  imageFailed(dish: DishShort): void {
    this.failedImages.update((images) => new Set([...images, this.imageKey(dish)]));
  }

  private imageKey(dish: DishShort): string {
    return `${dish.id}:${dish.imageUrl ?? ''}`;
  }
}
