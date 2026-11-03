import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, Router } from '@angular/router';
import { of } from 'rxjs';
import { ToastService } from '@app/services/toast/toast.service';
import { GuestPreorderStore } from './state/guest-preorder.store';
import { GuestPreOrderPageComponent } from './guest-pre-order-page.component';

describe('GuestPreOrderPageComponent F6 submission', () => {
  let store: Pick<GuestPreorderStore, 'status' | 'cartItems' | 'cartBusy' | 'inviteError' | 'submitPreorder' | 'initContext'>;
  let fixture: ComponentFixture<GuestPreOrderPageComponent>;

  beforeEach(() => {
    store = {
      status: signal<'active' | 'submitted' | 'inactive'>('active'),
      cartItems: signal([{ dishId: 'salad', title: 'Salad', price: 8, qty: 2 }]),
      cartBusy: signal(false),
      inviteError: signal<'expired' | 'retryable' | null>(null),
      submitPreorder: jasmine.createSpy('submitPreorder').and.resolveTo(),
      initContext: jasmine.createSpy('initContext'),
    };
    TestBed.configureTestingModule({
      imports: [GuestPreOrderPageComponent],
      providers: [
        { provide: GuestPreorderStore, useValue: store },
        { provide: ActivatedRoute, useValue: { paramMap: of(convertToParamMap({ token: 'test' })) } },
        { provide: Router, useValue: jasmine.createSpyObj('Router', ['navigateByUrl']) },
        { provide: ToastService, useValue: jasmine.createSpyObj('ToastService', ['success']) },
      ],
    }).overrideComponent(GuestPreOrderPageComponent, { set: { template: '', imports: [] } });
    fixture = TestBed.createComponent(GuestPreOrderPageComponent);
    fixture.detectChanges();
  });

  it('shows guest dishes and total; Cancel makes no request', () => {
    const page = fixture.componentInstance;
    page.openConfirmation();
    expect(page.confirmationItems()).toEqual([{ name: 'Salad', quantity: 2, unitPrice: 8 }]);
    page.closeConfirmation();
    expect(page.confirmationOpen()).toBeFalse();
    expect(store.submitPreorder).not.toHaveBeenCalled();
  });

  it('confirms the guest submission only once during a pending request', async () => {
    let resolve!: () => void;
    const pending = new Promise<void>(done => { resolve = done; });
    (store.submitPreorder as jasmine.Spy).and.returnValue(pending);
    const page = fixture.componentInstance;
    page.openConfirmation();
    const first = page.confirmSubmission();
    await page.confirmSubmission();
    expect(store.submitPreorder).toHaveBeenCalledTimes(1);
    resolve();
    await first;
    expect(page.confirmationOpen()).toBeFalse();
  });

  it('does not open for a submitted or inactive guest', () => {
    const page = fixture.componentInstance;
    store.status.set('submitted');
    page.openConfirmation();
    expect(page.confirmationOpen()).toBeFalse();
    store.status.set('inactive');
    page.openConfirmation();
    expect(page.confirmationOpen()).toBeFalse();
  });

  it('closes an open confirmation when the guest session becomes inactive', () => {
    const page = fixture.componentInstance;
    page.openConfirmation();
    expect(page.confirmationOpen()).toBeTrue();
    store.status.set('inactive');
    fixture.detectChanges();
    expect(page.confirmationOpen()).toBeFalse();
    expect(store.submitPreorder).not.toHaveBeenCalled();
  });
});
