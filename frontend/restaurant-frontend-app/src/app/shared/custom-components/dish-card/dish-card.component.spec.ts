import { ComponentFixture, TestBed } from '@angular/core/testing';
import { DishCardComponent } from './dish-card.component';
import { DishListResponse } from '@app/models/dishes/dish-list-response.model';
import { DishState } from '@app/models/enums/dish-state.model';

describe('DishCardComponent', () => {
  let fixture: ComponentFixture<DishCardComponent>;

  const dish: DishListResponse = {
    id: 'dish-1',
    name: 'Khachapuri',
    imageUrl: 'assets/img/dish.png',
    price: 15,
    state: DishState.Available,
    weight: '350',
    dishType: 'MainCourse',
    description: 'Georgian cheese bread',
    calories: '500',
    proteins: '20',
    carbohydrates: '45',
    fats: '25',
    vitamins: 'A, B',
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [DishCardComponent],
    }).compileComponents();

    fixture = TestBed.createComponent(DishCardComponent);

    fixture.componentRef.setInput('dish', dish);
    fixture.detectChanges();
  });

  it('renders a valid image with the dish name', () => {
    const image: HTMLImageElement = fixture.nativeElement.querySelector('img');
    expect(image.getAttribute('src')).toBe(dish.imageUrl);
    expect(image.alt).toBe(dish.name);
    expect(fixture.nativeElement.querySelector('.dish-image--placeholder')).toBeNull();
  });

  for (const imageUrl of [null, '', '   ']) {
    it(`renders an accessible placeholder for ${JSON.stringify(imageUrl)} image URL`, () => {
      fixture.componentRef.setInput('dish', { ...dish, imageUrl });
      fixture.detectChanges();
      expect(fixture.nativeElement.querySelector('img')).toBeNull();
      const placeholder: HTMLElement = fixture.nativeElement.querySelector('.dish-image--placeholder');
      expect(placeholder.getAttribute('role')).toBe('img');
      expect(placeholder.getAttribute('aria-label')).toBe('Khachapuri: image unavailable');
      expect(placeholder.textContent).toContain('Image unavailable');
    });
  }

  it('replaces a failed image with a placeholder and renders a changed image URL', () => {
    fixture.nativeElement.querySelector('img').dispatchEvent(new Event('error'));
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('img')).toBeNull();
    expect(fixture.nativeElement.querySelector('.dish-image--placeholder')).not.toBeNull();

    fixture.componentRef.setInput('dish', { ...dish, imageUrl: 'assets/img/replacement.png' });
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('img').getAttribute('src')).toBe('assets/img/replacement.png');
    expect(fixture.nativeElement.querySelector('.dish-image--placeholder')).toBeNull();
  });

  it('keeps the price inside a narrow card when the dish has a long name', () => {
    fixture.componentRef.setInput('dish', {
      ...dish,
      name: 'AQA-Sample-06156315da4646dda2560e0965cb742e-Salad',
      imageUrl: null,
    });
    fixture.detectChanges();

    const card: HTMLElement = fixture.nativeElement.querySelector('.dish-card');
    card.style.width = '316px';
    const name: HTMLElement = card.querySelector('.dish-name')!;
    const price: HTMLElement = card.querySelector('.dish-price')!;
    const cardBounds = card.getBoundingClientRect();
    const nameBounds = name.getBoundingClientRect();
    const priceBounds = price.getBoundingClientRect();

    expect(priceBounds.right).toBeLessThanOrEqual(cardBounds.right);
    expect(nameBounds.right).toBeLessThanOrEqual(priceBounds.left);
  });
});
