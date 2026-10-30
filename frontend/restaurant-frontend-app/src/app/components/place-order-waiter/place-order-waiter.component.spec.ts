import { of } from 'rxjs';
import { AuthenticationService } from '@app/auth/services/authentication.service';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { PlaceOrderWaiterComponent } from './place-order-waiter.component';
import { API_BASE_URL } from '@app/shared/config/api-base-url.token';

describe('PlaceOrderWaiterComponent', () => {
  let component: PlaceOrderWaiterComponent;
  let fixture: ComponentFixture<PlaceOrderWaiterComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      providers: [
        provideHttpClient(), provideHttpClientTesting(),
        { provide: API_BASE_URL, useValue: 'https://test.api' },
        { provide: AuthenticationService, useValue: {
          getUser: () => ({ id: 'waiter-1', firstName: 'Test', lastName: 'Waiter', role: 'Waiter' }),
          getUserNameById$: () => of('Test Customer'),
        } },
      ],
      imports: [PlaceOrderWaiterComponent],
    }).compileComponents();

    fixture = TestBed.createComponent(PlaceOrderWaiterComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('initializes the authenticated waiter and customer names', () => {
    expect(component.waiterName).toBe('Test Waiter');
    expect(component.customerName).toBe('Test Customer');
    expect(component.dishNameForm.get('dishName')?.hasError('required')).toBeTrue();
  });
});
