import { Injectable, computed, effect, signal, inject } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { GuestInviteService } from '@app/services/reservation/guest-invite.service';
import { GuestInvite } from '@app/models/reservations/guest-invite.model';
import { GuestMenuDish, GuestReminderNotification, GuestSessionResponse, OrderingPreference } from '@app/services/guest-sessions/guest-sessions.service';
import { GuestSessionsService } from '@app/services/guest-sessions/guest-sessions.service';

export type GuestPreorderStatus = 'active' | 'submitted' | 'inactive';

export interface GuestInviteVm {
    inviterName: string;
    restaurantName: string;
    locationAddress: string;
    tableNumberText: string;
    dateText: string;
    timeText: string;
    guestsText: string;
}

export interface GuestCartItem {
    dishId: string;
    title: string;
    price: number;
    imageUrl?: string;
    qty: number;
}

@Injectable({ providedIn: 'root' })
export class GuestPreorderStore {
    private readonly contextId = signal<string | null>(null);
    private verifiedSessionId: string | null = null;
    private checkingSession = false;
    private readonly guestInviteService = inject(GuestInviteService);

    readonly invite = signal<GuestInvite | null>(null);
    readonly inviteLoading = signal(false);
    readonly inviteError = signal<'expired' | 'retryable' | null>(null);

    readonly guestName = signal<string>('');
    readonly status = signal<GuestPreorderStatus>('active');
    readonly cartItems = signal<GuestCartItem[]>([]);
    readonly orderingPreference = signal<OrderingPreference>('Online');
    readonly revision = signal(0);
    readonly preferenceBusy = signal(false);
    readonly preferenceError = signal<string | null>(null);
    readonly reminderEmail = signal<string | null>(null);
    readonly reminderConsent = signal(false);
    readonly emailInput = signal('');
    readonly emailConsentInput = signal(false);
    private readonly emailDraftDirty = signal(false);
    readonly emailBusy = signal(false);
    readonly emailError = signal<string | null>(null);
    readonly emailMessage = signal<string | null>(null);
    readonly notifications = signal<GuestReminderNotification[]>([]);
    readonly notificationsBusy = signal(false);
    readonly notificationsError = signal<string | null>(null);

    private readonly guestSessionsService = inject(GuestSessionsService);

    readonly joinLoading = signal(false);
    readonly joinError = signal<string | null>(null);

    readonly guestSessionId = signal<string | null>(null);
    readonly guestSessionToken = signal<string | null>(null);
    readonly menuLoading = signal(false);
    readonly menuError = signal<string | null>(null);
    readonly cartBusy = signal(false);
    readonly cartError = signal<string | null>(null);

    readonly menuByCategory = signal<Record<string, GuestMenuDish[]>>({});
    readonly menuCategories = computed(() => Object.keys(this.menuByCategory()));
    readonly hasMenu = computed(() =>
        Object.values(this.menuByCategory()).some(dishes => dishes.length > 0)
    );

    readonly total = computed(() =>
        this.cartItems().reduce((sum, i) => sum + i.price * i.qty, 0)
    );

    constructor() {
        effect(() => {
            const ctx = this.contextId();
            if (!ctx) return;

            // IMPORTANT: after submit do not persist anything
            if (this.status() !== 'active') return;

            localStorage.setItem(this.key(ctx, 'guestName'), this.guestName());
            localStorage.setItem(this.key(ctx, 'cart'), JSON.stringify(this.cartItems()));
            localStorage.setItem(this.key(ctx, 'guestSessionId'), this.guestSessionId() ?? '');
            localStorage.setItem(this.key(ctx, 'guestSessionToken'), this.guestSessionToken() ?? '');
        });
    }

    initContext(id: string) {
        if (!id) return;
        if (this.contextId() === id) return;

        this.contextId.set(id);
        this.orderingPreference.set('Online');
        this.revision.set(0);
        this.preferenceError.set(null);
        this.clearReminderState();
        this.verifiedSessionId = null;
        void this.loadInviteByToken(id);

        if (localStorage.getItem(this.key(id, 'status')) === 'inactive') {
            this.clearStorage(id);
            localStorage.setItem(this.key(id, 'status'), 'inactive');
            this.status.set('inactive');
            this.guestSessionId.set(null);
            this.guestSessionToken.set(null);
            this.cartItems.set([]);
            this.menuByCategory.set({});
            return;
        }
        this.status.set('active');

        const name = localStorage.getItem(this.key(id, 'guestName'));
        const cartRaw = localStorage.getItem(this.key(id, 'cart'));

        const sessionId = localStorage.getItem(this.key(id, 'guestSessionId'));
        const sessionToken = localStorage.getItem(this.key(id, 'guestSessionToken'));

        if (sessionId) this.guestSessionId.set(sessionId);
        if (sessionToken) this.guestSessionToken.set(sessionToken);
        // A saved session was previously active on this browser. Check it even
        // if the first menu request after a reload now reports revocation.
        if (sessionId && sessionToken) this.verifiedSessionId = sessionId;

        // If guest was already joined on this device/browser, load menu automatically
        if (this.guestSessionId() && this.guestSessionToken()) {
            void this.loadMenu();
        }

        if (name) this.guestName.set(name);

        if (cartRaw) {
            try {
                const parsed = JSON.parse(cartRaw);
                if (Array.isArray(parsed)) this.cartItems.set(parsed);
            } catch {
                // ignore parse errors
            }
        }
    }

    async loadInviteByToken(token: string) {
        this.inviteLoading.set(true);
        this.inviteError.set(null);

        try {
            const inv = await firstValueFrom(this.guestInviteService.getInvite$(token));
            this.invite.set(inv);
        } catch (error) {
            this.invite.set(null);
            const status = error instanceof HttpErrorResponse ? error.status : null;
            this.inviteError.set(status === 404 || status === 410 ? 'expired' : 'retryable');
        } finally {
            this.inviteLoading.set(false);
        }
    }

    async join(name: string) {
        const inv = this.invite();
        if (!inv) {
            this.joinError.set('Invite is not loaded yet');
            return;
        }

        this.setGuestName(name);

        const groupReservationId = inv.GroupReservationId;
        if (!groupReservationId) {
            this.joinError.set('Invite is missing GroupReservationId');
            return;
        }

        const payload = {
            groupReservationId,
            guestName: this.guestName(),
        };

        console.log('[join payload]', payload);

        this.joinLoading.set(true);
        this.joinError.set(null);

        try {
            const res = await firstValueFrom(this.guestSessionsService.createSession(payload));
            this.guestSessionId.set(res.SessionId);
            this.guestSessionToken.set(res.SessionToken);
            await this.loadMenu();
        } catch (e) {
            console.log('[join error]', e);
            this.joinError.set('Failed to join session');
        } finally {
            this.joinLoading.set(false);
        }
    }

    readonly inviteVm = computed<GuestInviteVm | null>(() => {
        const inv = this.invite();
        if (!inv) return null;

        const inviterName = inv.InviterName ?? 'Someone';
        const restaurantName = inv.RestaurantName ?? '—';
        const locationAddress = inv.LocationAddress ?? '—';

        const tableNumberText =
            inv.TableNumber != null && String(inv.TableNumber).trim() !== ''
                ? `Table ${inv.TableNumber}`
                : '—';

        const startText = inv.StartTime ? String(inv.StartTime) : '';
        const endText = inv.EndTime ? String(inv.EndTime) : '';

        const dateText = startText
            ? new Date(startText).toLocaleDateString('en-US', { month: 'short', day: '2-digit', year: 'numeric' })
            : '—';

        const timeText = (startText && endText)
            ? `${new Date(startText).toLocaleTimeString('en-US', { hour: '2-digit', minute: '2-digit', hour12: false })} - ${new Date(endText).toLocaleTimeString('en-US', { hour: '2-digit', minute: '2-digit', hour12: false })}`
            : '—';

        const guestsText = inv.MaxGuestCount != null ? `${inv.MaxGuestCount} people` : '—';

        return { inviterName, restaurantName, locationAddress, tableNumberText, dateText, timeText, guestsText };
    });

    readonly inviteText = computed(() => {
        const vm = this.inviteVm();
        if (!vm) return null;

        const parts = [vm.locationAddress, vm.tableNumberText, vm.timeText].filter(Boolean);
        return `You are making a pre-order for the reservation ${parts.join(', ')}`;
    });

    async loadMenu() {
        const id = this.guestSessionId();
        const token = this.guestSessionToken();
        if (!id || !token) return;

        this.menuLoading.set(true);
        this.menuError.set(null);

        try {
            const res = await firstValueFrom(this.guestSessionsService.getMenu(id, token));
            console.log('[menu response]', res);
            console.log('[menu dishes keys]', Object.keys(res.dishes ?? {}));

            if (this.guestSessionId() !== id || this.status() !== 'active') return;
            this.menuByCategory.set(res.dishes ?? {});
            this.verifiedSessionId = id;
            await this.checkSession(true);
        } catch (e) {
            console.log('[menu error]', e);
            if (!this.invalidateIfRevoked(e, id)) {
                this.menuError.set('Failed to load menu');
                this.menuByCategory.set({});
            }
        } finally {
            this.menuLoading.set(false);
        }
    }

    setGuestName(name: string) {
        this.guestName.set(name.trim());
    }

    async addDish(item: Omit<GuestCartItem, 'qty'>): Promise<boolean> {
        return this.changeRemoteQuantity(item.dishId, 1, 'add', () => {
            const items = this.cartItems();
            const existing = items.find(cartItem => cartItem.dishId === item.dishId);
            this.cartItems.set(existing
                ? items.map(cartItem => cartItem.dishId === item.dishId
                    ? { ...cartItem, qty: cartItem.qty + 1 }
                    : cartItem)
                : [...items, { ...item, qty: 1 }]);
        });
    }

    async inc(dishId: string): Promise<boolean> {
        return this.changeRemoteQuantity(dishId, 1, 'add', () => {
            this.cartItems.set(this.cartItems().map(item =>
                item.dishId === dishId ? { ...item, qty: item.qty + 1 } : item
            ));
        });
    }

    async dec(dishId: string): Promise<boolean> {
        return this.changeRemoteQuantity(dishId, 1, 'remove', () => {
            this.cartItems.set(this.cartItems()
                .map(item => item.dishId === dishId ? { ...item, qty: item.qty - 1 } : item)
                .filter(item => item.qty > 0));
        });
    }

    async remove(dishId: string): Promise<boolean> {
        const item = this.cartItems().find(cartItem => cartItem.dishId === dishId);
        if (!item) return false;

        return this.changeRemoteQuantity(dishId, item.qty, 'remove', () => {
            this.cartItems.set(this.cartItems().filter(cartItem => cartItem.dishId !== dishId));
        });
    }

    async submitPreorder() {
        const sessionId = this.guestSessionId();
        const token = this.guestSessionToken();
        if (!sessionId || !token) {
            this.cartError.set('Your guest session has expired. Please rejoin the reservation.');
            return;
        }
        if (this.cartItems().length === 0 || this.cartBusy() || this.emailBusy() || this.orderingPreference() !== 'Online') return;

        this.cartBusy.set(true);
        this.cartError.set(null);
        try {
            await firstValueFrom(this.guestSessionsService.submit(sessionId, token));
            this.submitSuccess();
        } catch (error) {
            if (!this.invalidateIfRevoked(error, sessionId))
                this.cartError.set('Could not submit your pre-order. Please try again.');
        } finally {
            this.cartBusy.set(false);
        }
    }

    // call this only after API submit success
    submitSuccess() {
        this.status.set('submitted');

        const ctx = this.contextId();
        if (ctx) this.clearStorage(ctx);
    }

    async createNewPreorder() {
        const ctx = this.contextId();
        const invite = this.invite();
        const guestName = this.guestName().trim();
        const previousSessionId = this.guestSessionId();
        const previousToken = this.guestSessionToken();
        if (!ctx || !invite?.GroupReservationId || !guestName || this.joinLoading() ||
            this.status() !== 'submitted' || !previousSessionId || !previousToken) return;
        this.joinLoading.set(true);

        try {
            // A submitted guest can be removed while this success screen is open.
            // Check that session before the shared invite creates another one.
            await firstValueFrom(this.guestSessionsService.getSession(previousSessionId, previousToken));
            if (this.status() !== 'submitted' || this.guestSessionId() !== previousSessionId) return;

            localStorage.removeItem(this.key(ctx, 'cart'));
            localStorage.removeItem(this.key(ctx, 'guestSessionId'));
            localStorage.removeItem(this.key(ctx, 'guestSessionToken'));
            this.guestSessionId.set(null);
            this.guestSessionToken.set(null);
            this.clearReminderState();
            this.verifiedSessionId = null;
            this.menuByCategory.set({});
            this.menuError.set(null);
            this.menuLoading.set(true);
            this.cartItems.set([]);
            this.status.set('active');

            const session = await firstValueFrom(this.guestSessionsService.createSession({
                groupReservationId: invite.GroupReservationId,
                guestName,
            }));
            this.guestSessionId.set(session.SessionId);
            this.guestSessionToken.set(session.SessionToken);
            await this.loadMenu();
        } catch (error) {
            if (!this.invalidateIfRevoked(error, previousSessionId))
                this.menuError.set('Could not start a new pre-order. Please try again.');
        } finally {
            this.menuLoading.set(false);
            this.joinLoading.set(false);
        }
    }

    private async changeRemoteQuantity(
        dishId: string,
        quantity: number,
        operation: 'add' | 'remove',
        updateLocalCart: () => void,
    ): Promise<boolean> {
        const sessionId = this.guestSessionId();
        const token = this.guestSessionToken();
        if (!sessionId || !token) {
            this.cartError.set('Your guest session has expired. Please rejoin the reservation.');
            return false;
        }
        if (this.cartBusy() || this.preferenceBusy() || this.emailBusy() || this.orderingPreference() !== 'Online') return false;

        this.cartBusy.set(true);
        this.cartError.set(null);
        try {
            const request = operation === 'add'
                ? this.guestSessionsService.addDish(sessionId, token, dishId, quantity)
                : this.guestSessionsService.removeDish(sessionId, token, dishId, quantity);
            await firstValueFrom(request);
            updateLocalCart();
            // Each successful cart write advances the session revision on the server.
            // Keep the next preference change aligned with that write.
            this.revision.update(revision => revision + 1);
            return true;
        } catch (error) {
            if (!this.invalidateIfRevoked(error, sessionId))
                this.cartError.set('Could not update your cart. Please try again.');
            return false;
        } finally {
            this.cartBusy.set(false);
        }
    }

    async checkSession(force = false): Promise<void> {
        const id = this.guestSessionId();
        const token = this.guestSessionToken();
        if (!id || !token || this.status() === 'inactive' || this.verifiedSessionId !== id || this.checkingSession || this.emailBusy() ||
            (!force && (this.cartBusy() || this.preferenceBusy())))
            return;
        this.checkingSession = true;
        try {
            const session = await firstValueFrom(this.guestSessionsService.getSession(id, token));
            if (this.guestSessionId() === id && !this.cartBusy() && !this.preferenceBusy() && !this.emailBusy())
                this.applySession(session);
        } catch (error) {
            this.invalidateIfRevoked(error, id);
        } finally {
            this.checkingSession = false;
        }
    }

    async setPreference(preference: OrderingPreference): Promise<void> {
        const id = this.guestSessionId();
        const token = this.guestSessionToken();
        if (!id || !token || this.status() !== 'active' || this.cartBusy() || this.preferenceBusy() || this.emailBusy() ||
            preference === this.orderingPreference()) return;
        this.preferenceBusy.set(true);
        this.preferenceError.set(null);
        try {
            const session = await firstValueFrom(this.guestSessionsService.setPreference(id, token, preference, this.revision()));
            if (this.guestSessionId() === id) this.applySession(session);
        } catch (error) {
            if (!this.invalidateIfRevoked(error, id)) {
                this.preferenceError.set('Preference changed or could not be saved. Reloaded current state; please try again.');
                const current = await firstValueFrom(this.guestSessionsService.getSession(id, token)).catch(() => null);
                if (current && this.guestSessionId() === id) this.applySession(current);
            }
        } finally {
            this.preferenceBusy.set(false);
        }
    }

    private applySession(session: GuestSessionResponse): void {
        if ('email' in session || 'emailConsent' in session) {
            this.reminderEmail.set(session.email ?? null);
            this.reminderConsent.set(session.emailConsent ?? false);
            if (!this.emailDraftDirty()) {
                this.emailInput.set(session.email ?? '');
                this.emailConsentInput.set(session.emailConsent ?? false);
            }
        }
        this.orderingPreference.set(session.orderingPreference ?? 'Online');
        this.revision.set(session.revision ?? 0);
        this.cartItems.set((session.items ?? []).map(item => ({
            dishId: item.dish.id, title: item.dish.name, price: item.dish.price,
            imageUrl: item.dish.imageUrl ?? undefined, qty: item.quantity,
        })));
        if (session.isSubmitted) this.status.set('submitted');
    }

    editReminderEmail(value: string): void {
        this.emailInput.set(value);
        this.emailDraftDirty.set(true);
    }

    editReminderConsent(value: boolean): void {
        this.emailConsentInput.set(value);
        this.emailDraftDirty.set(true);
    }

    async saveReminderEmail(): Promise<void> {
        const id = this.guestSessionId();
        const token = this.guestSessionToken();
        const email = this.emailInput().trim();
        if (!id || !token || this.status() === 'inactive' || this.emailBusy() || this.cartBusy() || this.preferenceBusy()) return;
        this.emailError.set(null);
        this.emailMessage.set(null);
        if (!this.emailConsentInput() || !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) {
            this.emailError.set('Enter a valid email and agree to receive pre-order reminders.');
            return;
        }
        this.emailBusy.set(true);
        try {
            const response = await firstValueFrom(this.guestSessionsService.saveReminderEmail(id, token, email));
            if (this.guestSessionId() !== id) return;
            this.reminderEmail.set(response.email);
            this.reminderConsent.set(response.consent);
            this.revision.set(response.revision);
            this.emailInput.set(response.email ?? '');
            this.emailConsentInput.set(response.consent);
            this.emailDraftDirty.set(false);
            this.emailMessage.set('Reminder email saved.');
        } catch (error) {
            if (!this.invalidateIfRevoked(error, id)) this.emailError.set('Could not save reminder email. Please try again.');
        } finally {
            this.emailBusy.set(false);
        }
    }

    async removeReminderEmail(): Promise<void> {
        const id = this.guestSessionId();
        const token = this.guestSessionToken();
        if (!id || !token || this.status() === 'inactive' || this.emailBusy() || this.cartBusy() || this.preferenceBusy() || !this.reminderEmail()) return;
        this.emailBusy.set(true);
        this.emailError.set(null);
        this.emailMessage.set(null);
        try {
            const response = await firstValueFrom(this.guestSessionsService.removeReminderEmail(id, token));
            if (this.guestSessionId() !== id) return;
            this.reminderEmail.set(null);
            this.reminderConsent.set(false);
            this.revision.set(response.revision);
            this.emailInput.set('');
            this.emailConsentInput.set(false);
            this.emailDraftDirty.set(false);
            this.emailMessage.set('Reminder email removed. Future reminders will appear here.');
        } catch (error) {
            if (!this.invalidateIfRevoked(error, id)) this.emailError.set('Could not remove reminder email. Please try again.');
        } finally {
            this.emailBusy.set(false);
        }
    }

    async loadReminderNotifications(): Promise<void> {
        const id = this.guestSessionId();
        const token = this.guestSessionToken();
        if (!id || !token || this.status() === 'inactive' || this.notificationsBusy()) return;
        this.notificationsBusy.set(true);
        this.notificationsError.set(null);
        try {
            const messages = await firstValueFrom(this.guestSessionsService.getReminderNotifications(id, token));
            if (this.guestSessionId() === id) this.notifications.set(messages);
        } catch (error) {
            if (!this.invalidateIfRevoked(error, id))
                this.notificationsError.set('Could not load reminders. Please try again.');
        } finally {
            this.notificationsBusy.set(false);
        }
    }

    private clearReminderState(): void {
        this.reminderEmail.set(null);
        this.reminderConsent.set(false);
        this.emailInput.set('');
        this.emailConsentInput.set(false);
        this.emailDraftDirty.set(false);
        this.emailError.set(null);
        this.emailMessage.set(null);
        this.notifications.set([]);
        this.notificationsError.set(null);
    }

    private invalidateIfRevoked(error: unknown, sessionId: string): boolean {
        if (!(error instanceof HttpErrorResponse) ||
            (error.status !== 401 && error.status !== 404) ||
            this.verifiedSessionId !== sessionId ||
            this.guestSessionId() !== sessionId)
            return false;
        const ctx = this.contextId();
        if (ctx) {
            this.clearStorage(ctx);
            localStorage.setItem(this.key(ctx, 'status'), 'inactive');
        }
        this.verifiedSessionId = null;
        this.status.set('inactive');
        this.guestSessionId.set(null);
        this.guestSessionToken.set(null);
        this.clearReminderState();
        this.cartItems.set([]);
        this.menuByCategory.set({});
        this.cartError.set(null);
        return true;
    }

    private clearStorage(ctx: string) {
        localStorage.removeItem(this.key(ctx, 'guestName'));
        localStorage.removeItem(this.key(ctx, 'cart'));
        localStorage.removeItem(this.key(ctx, 'status'));
        localStorage.removeItem(this.key(ctx, 'guestSessionId'));
        localStorage.removeItem(this.key(ctx, 'guestSessionToken'));
        // cleanup legacy
    }

    private key(
        ctx: string,
        part: 'guestName' | 'cart' | 'status' | 'guestSessionId' | 'guestSessionToken'
    ) {
        return `guestPreorder:${ctx}:${part}`;
    }

}
