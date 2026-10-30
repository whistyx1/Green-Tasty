import { DishListResponse } from '@app/models/dishes/dish-list-response.model';
const dish: DishListResponse = {
  id: 'dish-1', name: 'Test salad', description: 'Fresh vegetables',
  imageUrl: 'assets/img/dish.png', price: 8, weight: '250 g',
  state: 'Available', dishType: 'Appetizer', calories: '180',
  proteins: '2', fats: '3', carbohydrates: '12', vitamins: 'A',
};

import { ComponentFixture, TestBed } from '@angular/core/testing';

import { DishModalComponent } from './dish-modal.component';

describe('DishModalComponent', () => {
  let component: DishModalComponent;
  let fixture: ComponentFixture<DishModalComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [DishModalComponent],
    }).compileComponents();

    fixture = TestBed.createComponent(DishModalComponent);
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
