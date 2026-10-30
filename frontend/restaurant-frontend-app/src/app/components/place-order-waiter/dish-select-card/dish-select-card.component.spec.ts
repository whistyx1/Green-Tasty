import { DishListResponse } from '@app/models/dishes/dish-list-response.model';
const dish: DishListResponse = {
  id: 'dish-1', name: 'Test salad', description: 'Fresh vegetables',
  imageUrl: 'assets/img/dish.png', price: 8, weight: '250 g',
  state: 'Available', dishType: 'Appetizer', calories: '180',
  proteins: '2', fats: '3', carbohydrates: '12', vitamins: 'A',
};

import { ComponentFixture, TestBed } from '@angular/core/testing';

import { DishSelectCardComponent } from './dish-select-card.component';

describe('DishSelectCardComponent', () => {
  let component: DishSelectCardComponent;
  let fixture: ComponentFixture<DishSelectCardComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [DishSelectCardComponent],
    }).compileComponents();

    fixture = TestBed.createComponent(DishSelectCardComponent);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('dish', dish);
    fixture.detectChanges();
  });

  it('renders the supplied dish name and image', () => {
    expect(component).toBeTruthy();
    expect(fixture.nativeElement.textContent).toContain('Test salad');
    expect(fixture.nativeElement.querySelector('img').getAttribute('src')).toBe(dish.imageUrl);
  });
});
