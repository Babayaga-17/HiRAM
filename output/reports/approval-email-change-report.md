# Approval email change report

Implemented November 6, 2026. Approval now sends an email through the existing IEmailManager only after the account and borrower transaction commits. The message confirms approval, sign-in availability, and equipment request eligibility without including credentials or an invented login URL.

Changed: UserAccountService.cs (email/logger injection and async post-commit notification); IUserAccountService.cs (Found/EmailSent async result); UserAccountController.cs (await approval and distinguish notification failure); Views/UserAccount/Details.cshtml (warning display); tests/RegistrationChecks/Program.cs (notification and failure checks); docs/development/authentication-and-password-reset.md and ui-and-shared-behavior.md (contract updates).

No production files, database columns or generated files were added. This report is the only added file for the email change. Existing SMTP configuration and dependency registrations are reused.

Verification: application and check-project builds passed. All 36 registration/photo/approval checks passed using an isolated SQL Server database and substituted email/storage. Checks include committed state visible from a separate connection before sending, correct notification recipient, no sends for rejected/missing/repeated/failed saves, and preserved Active/Eligible state when email fails. Real SMTP acceptance and inbox delivery were not exercised. No automatic retries or durable outbox were added. A process failure after commit but before sending can miss a notification.

The two updated development guides are currently ignored by Git and remain local updates.
