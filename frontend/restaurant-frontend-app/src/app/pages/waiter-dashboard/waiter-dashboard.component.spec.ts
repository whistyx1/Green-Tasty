import { provideRouter, Router } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { AuthenticationService } from '@app/auth/services/authentication.service';
import { of } from 'rxjs';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { WaiterDashboardComponent } from './waiter-dashboard.component';
import { API_BASE_URL } from '@app/shared/config/api-base-url.token';

describe('WaiterDashboardComponent', () => {
  let component: WaiterDashboardComponent;
  let fixture: ComponentFixture<WaiterDashboardComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      providers: [
        provideRouter([]), provideHttpClient(), provideHttpClientTesting(),
        { provide: API_BASE_URL, useValue: 'https://test.api' },
        { provide: AuthenticationService, useValue: {
          user$: of(null), isAuthorised$: of(false), getUser: () => null, getToken: () => null,
        } },
      ],
      imports: [WaiterDashboardComponent],
    }).compileComponents();

    spyOn(TestBed.inject(Router), 'navigate').and.resolveTo(true);
    fixture = TestBed.createComponent(WaiterDashboardComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('finishes loading without reservations when the session is absent', () => {
    expect(component.reservationsLoaded()).toBeTrue();
    expect(component.reservations()).toEqual([]);
  });
});
