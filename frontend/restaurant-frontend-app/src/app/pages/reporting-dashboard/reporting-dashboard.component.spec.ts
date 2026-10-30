import { provideRouter, Router } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { AuthenticationService } from '@app/auth/services/authentication.service';
import { of } from 'rxjs';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ReportingDashboardComponent } from './reporting-dashboard.component';
import { API_BASE_URL } from '@app/shared/config/api-base-url.token';

describe('ReportingDashboardComponent', () => {
  let component: ReportingDashboardComponent;
  let fixture: ComponentFixture<ReportingDashboardComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      providers: [
        provideRouter([]), provideHttpClient(), provideHttpClientTesting(),
        { provide: API_BASE_URL, useValue: 'https://test.api' },
        { provide: AuthenticationService, useValue: {
          user$: of(null), isAuthorised$: of(false), getUser: () => null, getToken: () => null,
        } },
      ],
      imports: [ReportingDashboardComponent],
    }).compileComponents();

    spyOn(TestBed.inject(Router), 'navigate').and.resolveTo(true);
    fixture = TestBed.createComponent(ReportingDashboardComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('renders report controls with no authenticated report data', () => {
    expect(component.reportData).toEqual([]);
    expect(fixture.nativeElement.textContent).toContain('Generate Report');
  });
});
