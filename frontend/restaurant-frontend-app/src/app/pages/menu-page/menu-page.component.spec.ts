import { provideRouter, Router } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { AuthenticationService } from '@app/auth/services/authentication.service';
import { of } from 'rxjs';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MenuPageComponent } from './menu-page.component';
import { API_BASE_URL } from '@app/shared/config/api-base-url.token';

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
});
