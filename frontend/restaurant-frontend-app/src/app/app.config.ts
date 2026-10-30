import { ApplicationConfig, provideZoneChangeDetection } from '@angular/core';
import { provideRouter } from '@angular/router';

import { routes } from './app.routes';
import { provideHttpClient, withInterceptors, withXhr } from '@angular/common/http';
import { tokenInterceptor } from './auth/token.interceptor';
import { provideDaterangepickerLocale } from 'ngx-daterangepicker-bootstrap';

import { environment } from '../environments/environment';
import { API_BASE_URL } from './shared/config/api-base-url.token';
import { homeworkProviders, homeworkInterceptors } from './shared/config/homework-providers';

export const appConfig: ApplicationConfig = {
  providers: [
    ...homeworkProviders,
    provideZoneChangeDetection({ eventCoalescing: true }),
    provideRouter(routes),
    provideHttpClient(withXhr(), withInterceptors([tokenInterceptor, ...homeworkInterceptors])),
    provideDaterangepickerLocale({
      separator: ' - ',
      applyLabel: 'Apply',
      cancelLabel: 'Cancel',
      format: 'MMM D, YYYY',
    }),
    { provide: API_BASE_URL, useValue: environment.backendUrl },
  ],
};
