# Notification implementation report

Implemented November 6, 2026. The previously prepared UI now retrieves personal notifications, supports opening/reading messages, and connects registration/reservation events to transactional in-app and email records.

## Behavior

| Event | Recipient |
| --- | --- |
| Registration submitted | Active Admin accounts |
| Registration approved | Approved borrower |
| Reservation submitted | Active Custodian accounts |
| Reservation approved/rejected | Requesting borrower |
| Reservation cancelled | Active Custodian accounts |

Staff groups also require an active role. The existing registration receipt remains separate. No return-date reminder or registration-rejection workflow was added. Pending users still cannot sign in.

The bell loads five recent messages and an unread count on server rendering. View all opens the notifications table, with database search, read/unread filters, ten-row paging, and preserved list context. Opening submits a protected POST, records the first read timestamp, and redirects to the full message. Details GET is read-only. Every retrieval and read update is scoped to the signed-in recipient. Related links use existing authorized destinations.

Business services stage event messages; repositories commit them with the business change. A background worker sends queued mail through existing SMTP settings after commit. It claims token-protected two-minute leases, checks every 15 seconds, processes at most 20 messages per cycle, and allows five attempts with increasing delays. Failed mail does not remove the in-app message or undo business changes. No JavaScript was added.

## Files added

- `ASI.Basecode.Resources/Constants/NotificationConstants.cs`: shared event and delivery states.
- `ASI.Basecode.Data/Interfaces/INotificationRepository.cs` and `Repositories/NotificationRepository.cs`: recipients, paging, read updates, staged persistence, and guarded email leases.
- `ASI.Basecode.Services/Interfaces/INotificationService.cs` and `Services/NotificationService.cs`: event rules, messages, normalization, and view-model mapping.
- `ASI.Basecode.Services/Interfaces/INotificationEmailDispatcher.cs` and `Services/NotificationEmailDispatcher.cs`: delivery, retry policy, and sanitized persisted errors.
- `ASI.Basecode.WebApp/Controllers/NotificationController.cs`: authenticated list/details/open routes.
- `ASI.Basecode.WebApp/ViewComponents/NotificationMenuViewComponent.cs`: asynchronous bell retrieval.
- `ASI.Basecode.WebApp/Workers/NotificationEmailWorker.cs`: hosted delivery processing.
- `tests/NotificationChecks/NotificationChecks.csproj` and `Program.cs`: isolated SQL integration checks with fake email.

## Files changed or moved

- Moved `WebApp/Models/NotificationViewModels.cs` to `Services/ServiceModels/NotificationViewModels.cs`; added event/related-record metadata for authorized links.
- `Data/Interfaces/IReservationRepository.cs` and `Repositories/ReservationRepository.cs`: serialized approve/reject transaction including staged notifications.
- `Services/Services/BorrowerProfileService.cs`: registration event before save.
- `Services/Services/EquipmentReservationService.cs`: submission/cancellation events inside existing transactions.
- `Services/Services/ReservationService.cs`: approval/rejection events with repository-owned review transaction.
- `Services/Interfaces/IUserAccountService.cs`, `Services/Services/UserAccountService.cs`, and `WebApp/Controllers/UserAccountController.cs`: approval queues mail rather than sending it directly; result now describes EmailQueued.
- `WebApp/Startup.DI.cs`: scoped repository/services and hosted worker registration.
- `WebApp/Views/_ViewImports.cshtml`, `Shared/_AppTopbar.cshtml`, `Shared/_NotificationMenu.cshtml`, `Notification/Index.cshtml`, and `Notification/Details.cshtml`: live model integration, protected open forms, feedback, and list navigation.
- `WebApp/wwwroot/css/notifications.css`: styles for message-opening buttons that retain the existing UI.
- `tests/RegistrationChecks/Program.cs` and `tests/ReservationChecks/Program.cs`: constructor wiring and queued-email expectations.
- `output/ui-previews/notifications/{index,details,dropdown}.html`: refreshed embedded production CSS; these remain fixture previews, not live authenticated routes.

No generated Data/Models or HiramDbContext files were edited. No SQL was executed against the application database.

## Documentation

Updated `docs/development/README.md`, `architecture-and-data.md`, `authentication-and-password-reset.md`, and `ui-and-shared-behavior.md`; added `notifications.md`. Contracts now describe transactional staging, queued approval mail, recipient-scoped access, server-rendered navigation, and worker delivery limits. Registration and reservation workflows are affected. These documents are currently ignored by the repository's existing Git rules; local updates will not be included in a normal commit unless that is addressed separately.

## Verification

- Notification checks: **32 passed**. Includes all six events, active staff recipients, generated-key linkage, rollback, ownership, stable read timestamps, search/paging, retries, concurrent claims, and expired/stale leases.
- Registration/photo checks: **39 passed**. Includes approval rollback, concurrent approval, post-commit dispatch, and provider-failure retry.
- Reservation checks: **40 passed**. Includes ownership, stale edits/cancellation, availability, and conflicting concurrent submissions.
- Test databases use guarded temporary names and an overridden context configuration; mail is fake. They do not use the application database or real SMTP.
- App and test project builds succeeded. Existing scaffold connection-string warning remains; notification-test build also reported unavailable NuGet source warnings while using cached packages.
- Local preview inspected at 1440, 768, and 390 pixels: zero inspector failures; one warning for the existing full-height workspace blank area. Desktop/mobile screenshots reviewed. This does not verify authenticated browser forms or actual SMTP delivery.
- Git whitespace check passed, with line-ending normalization notices only.

Regression testing exposed pooled SQL connections retaining serializable isolation. Email claims now explicitly use a read-committed transaction; both registration and notification checks passed afterward.

## Deployment and limits

Restart the web application to load the worker. The existing notification SQL/scaffold must be present in the target database, and existing Smtp configuration must be valid. This change does not backfill earlier events. The worker pauses when the host stops.

Sent means SMTP acceptance, not guaranteed inbox delivery. An accepted email followed by database acknowledgement failure can result in a duplicate after lease recovery. There is no failed-mail management screen yet. Actual SMTP/inbox delivery and authenticated end-to-end browser behavior remain unverified.
