import { EnvironmentProviders, isDevMode, provideAppInitializer } from '@angular/core';
import { HttpInterceptorFn } from '@angular/common/http';
import { applyDemoLoginFromUrl } from '../mocks/auth/demo-auth';
import { groupOrdersMockInterceptor } from '../mocks/group-orders/group-orders-mock.interceptor';

// Replaced into the application only by the Angular development configuration.
export const homeworkProviders: EnvironmentProviders[] = isDevMode()
  ? [provideAppInitializer(() => { applyDemoLoginFromUrl(); })]
  : [];

export const homeworkInterceptors: HttpInterceptorFn[] = isDevMode()
  ? [groupOrdersMockInterceptor]
  : [];
