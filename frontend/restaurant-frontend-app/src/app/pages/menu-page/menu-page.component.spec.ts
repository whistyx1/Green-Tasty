import { provideRouter, Router } from '@angular/router';
import { HttpErrorResponse, provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { AuthenticationService } from '@app/auth/services/authentication.service';
import { of, throwError } from 'rxjs';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MenuPageComponent } from './menu-page.component';
import { API_BASE_URL } from '@app/shared/config/api-base-url.token';
import { DishListResponse } from '@app/models/dishes/dish-list-response.model';
import { PreOrdersService } from '@app/services/pre-orders/pre-orders.service';
import { ToastService } from '@app/services/toast/toast.service';

describe('MenuPageComponent', () => {
  let component: MenuPageComponent;
  let fixture: ComponentFixture<MenuPageComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      providers: [
        provideRouter([]), provideHttpClient(), provideHttpClientTesting(),
        { provide: API_BASE_URL, useValue: 'https://test.api' },
        { provide: AuthenticationService, useValue: {
          user$: of(null), isAuthorised$: of(false), getUser: () => null, getToken: () => null,
        } },
      ],
      imports: [MenuPageComponent],
    }).compileComponents();

    spyOn(TestBed.inject(Router), 'navigate').and.resolveTo(true);
    fixture = TestBed.createComponent(MenuPageComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
    const request = TestBed.inject(HttpTestingController).expectOne(
      (candidate) => candidate.url === 'https://test.api/dishes'
    );
    expect(request.request.method).toBe('GET');
    request.flush([]);
    TestBed.inject(HttpTestingController).verify();
  });

  it('loads an empty menu through the routed page', () => {
    expect(component.dishes).toEqual([]);
    expect(component.isPreOrderEnabled).toBeFalse();
  });

  it('balances the outer margins when a row contains multiple dishes', () => {
    const dish: DishListResponse = {
      id: 'dish-0', name: 'Marathon Demo Salad', imageUrl: '', price: 8,
      state: 'Available', weight: '250 g', dishType: 'Appetizer',
      description: 'Demo dish', calories: '180', proteins: '0',
      carbohydrates: '0', fats: '0', vitamins: 'A',
    };
    component.filteredDishes = Array.from({ length: 8 }, (_, index) => ({
      ...dish, id: `dish-${index}`,
    }));
    fixture.detectChanges();

    const grid: HTMLElement = fixture.nativeElement.querySelector('.dish-cards');
    const cards = Array.from(grid.querySelectorAll<HTMLElement>('.dish-card'));
    const gridBounds = grid.getBoundingClientRect();
    const firstTop = cards[0].getBoundingClientRect().top;
    const firstRow = cards.filter((card) => Math.abs(card.getBoundingClientRect().top - firstTop) < 1);
    const leftInset = firstRow[0].getBoundingClientRect().left - gridBounds.left;
    const rightInset = gridBounds.right - firstRow[firstRow.length - 1].getBoundingClientRect().right;

    expect(gridBounds.width).toBeGreaterThan(0);
    expect(firstRow.length).toBeGreaterThan(1);
    expect(Math.abs(leftInset - rightInset)).toBeLessThanOrEqual(1);
  });

  it('does not replace an order validation response with a raw HTTP error toast', () => {
    const dish: DishListResponse = {
      id: 'dish-0', name: 'Marathon Demo Salad', imageUrl: '', price: 8,
      state: 'Available', weight: '250 g', dishType: 'Appetizer',
      description: 'Demo dish', calories: '180', proteins: '0',
      carbohydrates: '0', fats: '0', vitamins: 'A',
    };
    const service = TestBed.inject(PreOrdersService);
    const toast = TestBed.inject(ToastService);
    spyOn(service, 'addDishToPreOrderByReservationId').and.returnValue(
      throwError(() => new HttpErrorResponse({
        status: 400,
        error: { error: 'Pre-orders can only be modified up to 30 minutes before the reservation start time.' },
      }))
    );
    const localErrorToast = spyOn(toast, 'error');
    component.preOrderReservationId = 'reservation-1';

    component.addDishToCart(dish);

    expect(service.addDishToPreOrderByReservationId).toHaveBeenCalledOnceWith('reservation-1', dish, 1);
    expect(localErrorToast).not.toHaveBeenCalled();
  });
});
