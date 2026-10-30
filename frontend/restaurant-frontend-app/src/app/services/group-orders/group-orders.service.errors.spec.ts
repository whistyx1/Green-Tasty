import { HttpErrorResponse, provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { API_BASE_URL } from '@app/shared/config/api-base-url.token';
import { GroupOrdersService } from './group-orders.service';

describe('Group orders service errors and encoded identifiers', () => {
  let service: GroupOrdersService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: API_BASE_URL, useValue: 'https://api.test' },
      ],
    });
    service = TestBed.inject(GroupOrdersService);
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());

  it('propagates invite-token failure without requesting an invite', () => {
    let status = 0;
    service.getGroupInvite$('reservation').subscribe({
      error: (error: HttpErrorResponse) => {
        status = error.status;
      },
    });
    http
      .expectOne('https://api.test/reservations/reservation/invite-token')
      .flush({ error: 'Missing' }, { status: 404, statusText: 'Not Found' });
    expect(status).toBe(404);
    http.expectNone((request) => request.url.includes('/guest/invites/'));
  });

  it('encodes group identifiers as one path segment', () => {
    service.getSessions$('group/with space').subscribe();
    http
      .expectOne('https://api.test/guest/groups/group%2Fwith%20space/sessions')
      .flush({ participants: [], total: 0 });
  });

  it('propagates submission errors without retrying the mutation', () => {
    let status = 0;
    service.submit$('group', 'reservation').subscribe({
      error: (error: HttpErrorResponse) => {
        status = error.status;
      },
    });
    http
      .expectOne('https://api.test/group-reservations/group/submit')
      .flush({ error: 'No order' }, { status: 400, statusText: 'Bad Request' });
    expect(status).toBe(400);
  });
});
