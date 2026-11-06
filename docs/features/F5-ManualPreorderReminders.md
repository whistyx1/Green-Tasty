# F5 — Manual Pre-order Reminders

The in-app reminder path was deployed to the AWS development environment on 2026-11-06. Email delivery remains disabled while SES production access is under review. The source requirements are in [features.txt](../../features.txt).

## Behavior

- The reservation owner can remind an active, pending Online guest from the group-order page between 60 and 30 minutes before the reservation begins.
- The guest may optionally save, update, or remove a reminder email. Saving requires explicit consent. The address stays on the guest session in DynamoDB; the owner snapshot does not expose it.
- With email delivery enabled, a consented usable address selects SES email. Otherwise, the reminder is stored as an in-app notification that only the corresponding guest-session token can read. In the current deployment, all reminders use the in-app channel.
- A 15-minute cooldown is claimed with a DynamoDB transaction that checks the guest, group, and reservation state. The in-app notification is written in the same transaction. Concurrent requests cannot claim the same cooldown window.
- If SES does not confirm acceptance, the API reports an error and retains the claim. This avoids an ambiguous retry sending a duplicate email. SES acceptance does not prove delivery to the mailbox.

## API and configuration

The owner route is `POST /guest/groups/{groupReservationId}/sessions/{guestSessionId}/reminder` with Cognito authentication. Invited guests use their session bearer token for `PUT` and `DELETE /guest/sessions/{guestSessionId}/email` and `GET /guest/sessions/{guestSessionId}/notifications`. Email updates return the new guest-session revision so subsequent F4 preference changes use the current revision.

Terraform sets `ReminderEmailEnabled` from `enable_reminder_email`, which defaults to `false`. While it is false, the backend rejects email collection, never calls SES, and the production frontend hides the email form. When enabled later, Terraform grants the API Lambda `ses:SendEmail` for the existing verified sender identity; the frontend must also be rebuilt with `REMINDER_EMAIL_ENABLED=true`. No DynamoDB table or migration is added.

## Local verification

On 2026-11-06, the reminder-focused backend tests passed 21/21 and the full backend suite passed 387/387. The relevant guest-page tests passed 10/10 and the full frontend suite passed 448/448. Backend Release publish, frontend lint (0 errors, 38 warnings), production build, bundle check, `terraform fmt -check`, and `terraform validate` passed. The isolated release build used the live `/develop` API URL with `REMINDER_EMAIL_ENABLED=false`. A subsequent owner-route interceptor fix passed 12/12 focused frontend tests, lint, and a production build.

Local tests cover owner and guest authorization, the reminder window and cooldown, the transaction conditions, guest email consent and removal, notification access, and the F4 revision interaction. The deployed API Lambda `dev` alias points to version 13, with `ReminderEmailEnabled=false`; public health and unauthenticated route checks passed. CloudFront serves the verified frontend build. The first live owner reminder returned 401 because the frontend interceptor omitted Cognito authentication for the new route. The frontend-only fix added that route to owner authentication and was deployed without changing the backend.

An authenticated owner then sent an in-app reminder to a pending Online guest, who opened it in their guest session. The [guest notification screenshot](screenshots/f5-in-app-reminder.png) shows the received message; it is cropped to exclude reservation identifiers. The owner also confirmed that switching a guest to In Restaurant removes the Send reminder action. These manual checks verify delivery and UI eligibility for those scenarios, but not the 15-minute cooldown or concurrency behavior under production traffic. Live email delivery remains unverified while SES production access is pending. A concurrent consent change or reservation edit after the last pre-send check can still race with the external SES call when email is enabled; the backend narrows this window but cannot make SES delivery atomic with DynamoDB.
