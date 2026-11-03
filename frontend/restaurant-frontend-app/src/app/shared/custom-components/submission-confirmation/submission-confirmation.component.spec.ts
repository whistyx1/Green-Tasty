import { TestBed } from '@angular/core/testing';
import { SubmissionConfirmationComponent, SubmissionKind } from './submission-confirmation.component';

describe('SubmissionConfirmationComponent', () => {
  for (const [kind, warning] of [
    ['ordinary', 'until 30 minutes before'],
    ['guest', 'cannot be modified through the app'],
    ['group', 'finalizes the group order'],
  ] as const satisfies readonly (readonly [SubmissionKind, string])[]) {
    it(`shows ${kind} dishes, quantities, total, and warning`, () => {
      const fixture = TestBed.createComponent(SubmissionConfirmationComponent);
      fixture.componentRef.setInput('kind', kind);
      fixture.componentRef.setInput('items', [
        { name: 'Salad', quantity: 2, unitPrice: 8 },
        { name: 'Soup', quantity: 1, unitPrice: 5 },
      ]);
      fixture.detectChanges();
      const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
      expect(text).toContain('Salad × 2');
      expect(text).toContain('Soup × 1');
      expect(text).toContain('$21.00');
      expect(text).toContain(warning);
      expect((fixture.nativeElement as HTMLElement).querySelector('[role="dialog"]')).not.toBeNull();
    });
  }

  it('Cancel emits only close and Confirm emits only confirmation', () => {
    const fixture = TestBed.createComponent(SubmissionConfirmationComponent);
    fixture.componentRef.setInput('kind', 'ordinary');
    fixture.componentRef.setInput('items', [{ name: 'Salad', quantity: 1, unitPrice: 8 }]);
    fixture.detectChanges();
    const close = spyOn(fixture.componentInstance.closed, 'emit');
    const confirm = spyOn(fixture.componentInstance.confirmed, 'emit');
    const buttons = [...(fixture.nativeElement as HTMLElement).querySelectorAll('button')];
    buttons.find(button => button.textContent?.trim() === 'Cancel')?.click();
    expect(close).toHaveBeenCalledTimes(1);
    expect(confirm).not.toHaveBeenCalled();
    buttons.find(button => button.textContent?.trim() === 'Confirm')?.click();
    expect(confirm).toHaveBeenCalledTimes(1);
  });

  it('blocks confirmation and dismissal while busy', () => {
    const fixture = TestBed.createComponent(SubmissionConfirmationComponent);
    fixture.componentRef.setInput('kind', 'group');
    fixture.componentRef.setInput('items', [{ name: 'Salad', quantity: 1, unitPrice: 8 }]);
    fixture.componentRef.setInput('busy', true);
    fixture.detectChanges();
    const close = spyOn(fixture.componentInstance.closed, 'emit');
    const confirm = spyOn(fixture.componentInstance.confirmed, 'emit');
    fixture.componentInstance.close();
    fixture.componentInstance.confirm();
    expect(close).not.toHaveBeenCalled();
    expect(confirm).not.toHaveBeenCalled();
  });

  it('returns keyboard focus to the trigger after dismissal', async () => {
    const trigger = document.createElement('button');
    document.body.appendChild(trigger);
    trigger.focus();
    const fixture = TestBed.createComponent(SubmissionConfirmationComponent);
    fixture.componentRef.setInput('kind', 'ordinary');
    fixture.componentRef.setInput('items', [{ name: 'Salad', quantity: 1, unitPrice: 8 }]);
    fixture.detectChanges();
    try {
      await fixture.whenStable();
      expect((fixture.nativeElement as HTMLElement).contains(document.activeElement)).toBeTrue();
    } finally {
      fixture.destroy();
      expect(document.activeElement).toBe(trigger);
      trigger.remove();
    }
  });

  it('explains pending group participants without including their dishes', () => {
    const fixture = TestBed.createComponent(SubmissionConfirmationComponent);
    fixture.componentRef.setInput('kind', 'group');
    fixture.componentRef.setInput('pendingGuests', 2);
    fixture.componentRef.setInput('items', [{ name: 'Salad', quantity: 1, unitPrice: 8 }]);
    fixture.detectChanges();
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('2 participants have not submitted yet');
    expect(text).toContain('Pending participant carts are not included yet');
    expect(text).toContain('$8.00');
  });
});
