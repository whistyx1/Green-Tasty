import { EnvironmentProviders } from '@angular/core';
import { HttpInterceptorFn } from '@angular/common/http';

// Production/default entry point. Development replaces this entire module.
// Keep mock imports out of this file so they cannot enter the production graph.
export const homeworkProviders: EnvironmentProviders[] = [];
export const homeworkInterceptors: HttpInterceptorFn[] = [];
