import { provideRouter, Router } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { AuthenticationService } from '@app/auth/services/authentication.service';
import { of } from 'rxjs';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ReservationDashboardComponent } from './reservation-dashboard.component';
import { API_BASE_URL } from '@app/shared/config/api-base-url.token';

describe('ReservationDashboardComponent', () => {
  let component: ReservationDashboardComponent;
  let fixture: ComponentFixture<ReservationDashboardComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      providers: [
        provideRouter([]), provideHttpClient(), provideHttpClientTesting(),
        { provide: API_BASE_URL, useValue: 'https://test.api' },
        { provide: AuthenticationService, useValue: {
          user$: of(null), isAuthorised$: of(false), getUser: () => null, getToken: () => null,
        } },
      ],
      imports: [ReservationDashboardComponent],
    }).compileComponents();

    spyOn(TestBed.inject(Router), 'navigate').and.resolveTo(true);
    fixture = TestBed.createComponent(ReservationDashboardComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('redirects an unauthenticated visitor without loading reservations', () => {
    expect(component.reservationsLoaded()).toBeTrue();
    expect(TestBed.inject(Router).navigate).toHaveBeenCalledOnceWith(['/login']);
  });
});
