import { TestBed } from '@angular/core/testing';
import { Participant } from '@app/models/group-orders/participant.model';
import { ParticipantCardComponent } from './participant-card.component';

const GUEST: Participant = {
  id: 'guest-image-test',
  type: 'Guest',
  name: 'Test guest',
  isSubmitted: false,
  total: 8,
  items: [{ dish: { id: 'salad', name: 'Salad', price: 8 }, quantity: 1 }],
};

describe('Participant card image fallback', () => {
  it('shows a dish initial without a broken image when its URL is missing', () => {
    const fixture = TestBed.createComponent(ParticipantCardComponent);
    fixture.componentRef.setInput('participant', GUEST);
    fixture.detectChanges();
    const element: HTMLElement = fixture.nativeElement;

    expect(element.querySelector('img')).toBeNull();
    expect(element.querySelector('.initial')?.textContent).toBe('S');
    expect(element.textContent).toContain('Salad');
  });

  it('replaces a failed image with its initial and keeps the dish details', () => {
    const fixture = TestBed.createComponent(ParticipantCardComponent);
    const withImage = {
      ...GUEST,
      items: [{ ...GUEST.items[0], dish: { ...GUEST.items[0].dish, imageUrl: 'missing.jpg' } }],
    };
    fixture.componentRef.setInput('participant', withImage);
    fixture.detectChanges();
    const element: HTMLElement = fixture.nativeElement;

    element.querySelector('img')?.dispatchEvent(new Event('error'));
    fixture.detectChanges();

    expect(element.querySelector('img')).toBeNull();
    expect(element.querySelector('.initial')?.textContent).toBe('S');
    expect(element.textContent).toContain('$8.00');
  });

  it('tries a replacement URL even if the old URL for the same dish failed', () => {
    const fixture = TestBed.createComponent(ParticipantCardComponent);
    const withImage = {
      ...GUEST,
      items: [{ ...GUEST.items[0], dish: { ...GUEST.items[0].dish, imageUrl: 'old.jpg' } }],
    };
    fixture.componentRef.setInput('participant', withImage);
    fixture.detectChanges();
    const element: HTMLElement = fixture.nativeElement;
    element.querySelector('img')?.dispatchEvent(new Event('error'));
    fixture.detectChanges();

    fixture.componentRef.setInput('participant', {
      ...withImage,
      items: [{ ...withImage.items[0], dish: { ...withImage.items[0].dish, imageUrl: 'new.jpg' } }],
    });
    fixture.detectChanges();

    expect(element.querySelector('img')?.getAttribute('src')).toBe('new.jpg');
    expect(element.querySelector('.initial')).toBeNull();
  });
});
