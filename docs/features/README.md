# Feature Records

This directory documents additional features implemented for **Green & Tasty** after the original EPAM IT Marathon 2026 homework.

Green & Tasty is a full-stack restaurant reservation and group pre-order application built with Angular, .NET, and AWS.

## Documentation

- [Project README](../../README.md) — project overview, architecture, setup, testing, and deployment.
- [Feature Backlog](../../features.txt) — feature requirements, priorities, and acceptance criteria for F1–F7.
- [Original EPAM Homework](../epam-homework/) — original homework documentation and requirements.
- [Frontend Follow-up Tasks](../../frontend/restaurant-frontend-app/docs/FollowUpTasks.md) — additional frontend improvements and technical follow-up tasks.

## Implementation Status

| ID | Feature | Status |
|---|---|---|
| F1 | Persistent Group Invitation Link | Implemented & Deployed |
| F2 | Single Reservation Direct Submission | Implemented & Deployed |
| F3 | Reservation Dashboard Filters | Implemented & Deployed |
| F4 | Per-Guest Ordering Preference | Implemented & Deployed |
| F5 | Manual Pre-order Reminders | In-app path deployed; email pending SES access |
| F6 | Submission Confirmation Dialogs | Implemented & Deployed |
| F7 | Group Progress & Final Submission Gate | Implemented & Deployed |

**Progress: F1-F4, F6, and F7 are deployed. F5 in-app reminders are deployed and one authenticated delivery was verified; its email path awaits SES production access.**

---

## F1 — Persistent Group Invitation Link

Reservation owners can generate and share a persistent invitation link for their group reservation.

Guests can join through the invitation link without creating a separate account. The link remains reusable throughout the active group-ordering workflow, while backend authorization protects owner-specific operations.

**Documentation:** [Implementation and Verification](F1-PersistentGroupInvitationLink.md)

## F2 — Single Reservation Direct Submission

Customers making individual reservations can submit their pre-orders directly, without going through the group invitation workflow.

The backend validates submission requests and uses DynamoDB conditional writes to prevent duplicate submissions and maintain order integrity.

## F3 — Reservation Dashboard Filters

The reservation dashboard provides convenient filtering controls to help customers find and manage reservations.

The interface includes compact filters, clear-filter controls, and appropriate empty states.

## F4 — Per-Guest Ordering Preference

Each group participant, including the reservation owner, can choose between two ordering preferences:

- **Online** — select dishes and submit a pre-order before the restaurant visit.
- **In Restaurant** — order directly at the restaurant.

Switching preferences preserves saved dish selections. In Restaurant participants are excluded from online group totals and final pre-orders.

The reservation owner can submit their own pre-order separately from the final group submission.

## F5 — Manual Pre-order Reminders

Group owners can manually remind pending Online guests between 60 and 30 minutes before the reservation starts. The current release stores reminders in the guest session's notification inbox and enforces a 15-minute cooldown using a conditional DynamoDB transaction. Optional email collection and SES delivery remain disabled until SES production access is granted.

**Documentation:** [Implementation and Verification](F5-ManualPreorderReminders.md)

## F6 — Submission Confirmation Dialogs

Confirmation dialogs provide an additional review step before important submission actions.

They help users understand what they are submitting and reduce accidental submissions.

## F7 — Group Progress & Final Submission Gate

Reservation owners can monitor the ordering progress of all group participants.

Each participant has one of three ordering states:

- **Online Pending** — the participant has not submitted a valid online pre-order.
- **Online Submitted** — the participant has submitted their online pre-order.
- **In Restaurant** — the participant will order at the restaurant.

The dashboard displays submission progress and counts the reservation owner only once.

Final group submission is allowed only when all Online participants have submitted nonempty pre-orders. Participants choosing In Restaurant do not block finalization.

If everyone chooses In Restaurant, the owner can complete the online workflow without creating an empty order. The table reservation remains active.

The backend revalidates participant states and submission eligibility transactionally using DynamoDB.

---

## Verification

The implemented features have been validated through automated tests, production builds, and development-environment deployment checks.

F7 and the F4 UX improvements were additionally verified through live API smoke tests covering:

- Blocking finalization when an Online participant is pending.
- Successful finalization when all Online participants are ready.
- Completing an all-In-Restaurant workflow without creating an empty order.
- Preserving the active table reservation when online ordering is skipped.

## Deployment

The application is deployed to AWS using:

- **Frontend:** Angular, Amazon S3, Amazon CloudFront
- **Backend:** .NET, AWS Lambda, Amazon API Gateway
- **Database:** Amazon DynamoDB
- **Authentication:** Amazon Cognito
- **Infrastructure as Code:** Terraform

**Live application:** https://d14775dwphtih4.cloudfront.net/main

**Source repository:** https://github.com/whistyx1/Green-Tasty

---

*These feature records describe functionality added beyond the original EPAM homework. F5 email delivery is not yet deployed.*
