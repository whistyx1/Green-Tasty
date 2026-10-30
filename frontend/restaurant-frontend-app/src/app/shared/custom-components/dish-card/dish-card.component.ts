import {Component, ChangeDetectionStrategy, input, output, computed, signal} from '@angular/core';
import { CommonModule } from '@angular/common';
import { DishListResponse } from '@app/models/dishes/dish-list-response.model';
import { DishState } from '@app/models/enums/dish-state.model';
import { DishStateLabels } from '@app/shared/mappers/dish-enum.mapper';
import {CustomButtonComponent} from '@app/shared/custom-components/custom-button/custom-button.component';

@Component({
  selector: 'app-dish-card',
  templateUrl: './dish-card.component.html',
  styleUrls: ['./dish-card.component.scss'],
  standalone: true,
  changeDetection: ChangeDetectionStrategy.Eager,
  imports: [CommonModule, CustomButtonComponent],
})
export class DishCardComponent {
  readonly dish = input.required<DishListResponse>();
  readonly maxWidth = input('');
  readonly preOrderEnabled = input(false);

  readonly dishClicked = output<void>();
  readonly preOrderClicked = output<void>();

  private readonly failedImageKey = signal<string | null>(null);
  readonly imageUrl = computed(() => {
    const dish = this.dish();
    const imageUrl = dish.imageUrl?.trim();
    return imageUrl && this.failedImageKey() !== `${dish.id}:${imageUrl}` ? imageUrl : null;
  });

  onImageError(): void {
    const dish = this.dish();
    this.failedImageKey.set(`${dish.id}:${dish.imageUrl?.trim()}`);
  }

  readonly dishStateLabel = computed(() => {
    const state = this.dish().state;

    return DishStateLabels[state as DishState] ?? state;
  });

  readonly isUnavailable = computed(() => {
    const state = this.dish().state.toLowerCase();

    return (
      state === DishState.OnStop.toLowerCase() ||
      state === DishState.OutOfStock.toLowerCase()
    );
  });

  onCardClicked(): void {
    if (!this.isUnavailable() && !this.preOrderEnabled()) {
      this.dishClicked.emit();
    }
  }
}
