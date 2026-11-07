# Registration approval change report

Date: November 6, 2026

## Result

Admins use Users & Roles, select Pending approval in the existing status filter, open an account, review personal details, borrower information and the optional profile photo, then choose Approve registration. A successful approval sets Accounts.account_status to Active and BorrowerProfiles.eligibility_status to Eligible together. Both records receive UpdatedBy (the signed-in admin's user ID) and UpdatedAt.

## Changes

| Files | Purpose |
| --- | --- |
| ASI.Basecode.Data/Interfaces/IAccountRepository.cs; Repositories/AccountRepository.cs | Added UpdateRegistration. A repository-owned serializable transaction locks the account for update, loads the associated role and borrower, runs the service callback, saves once and commits. Exceptions roll back. |
| ASI.Basecode.Services/Interfaces/IUserAccountService.cs; Services/UserAccountService.cs | Added ApproveRegistration. Service validates the account version, PendingApproval state, active Borrower role and an Ineligible borrower with no suspension fields. Sets the two statuses and attribution. Guards generic activation/deactivation and edits against bypassing review. |
| ASI.Basecode.Resources/Constants/UserAccountConstants.cs | Added FilterOptions containing PendingApproval while preserving the existing ordinary assignable statuses. |
| ASI.Basecode.Services/ServiceModels/UserAccountFormViewModel.cs | Permits retaining PendingApproval on edit; the service checks the persisted state and prevents forged transitions. |
| ASI.Basecode.WebApp/Controllers/UserAccountController.cs | Details loads registration data through the existing AccountService. New anti-forgery-protected Approve POST uses the existing Admin-only management policy, reports validation/save outcomes and redirects back to account details. |
| ASI.Basecode.WebApp/Views/UserAccount/Index.cshtml | Added Pending approval to the existing status filter and readable status label. Existing filtering remains client-side. |
| ASI.Basecode.WebApp/Views/UserAccount/Details.cshtml | Displays pending borrower review and optional profile photo; pending accounts receive Approve registration instead of Activate account. |
| ASI.Basecode.WebApp/Views/UserAccount/_UserAccountForm.cshtml | Pending role and status are read-only; existing personal-detail editing remains available. |
| ASI.Basecode.WebApp/wwwroot/css/user-account.css | Scoped review layout and pending status styling, including small-screen stacking. |
| tests/RegistrationChecks/Program.cs | Added approval integration checks and updated the repository test double for the existing profile lookup interface. |
| database/064_pending_account_status.sql | New incremental, transactional script allowing PendingApproval plus the four existing statuses. Not executed against the live database. |
| docs/development/architecture-and-data.md | Documents repository-owned approval transaction and available approval attribution. |
| docs/development/authentication-and-password-reset.md | Records registration, login and approval contracts and the database prerequisite. |
| docs/development/ui-and-shared-behavior.md | Records the existing-list review flow and pending-account edit restrictions. |

The documentation files are currently ignored by Git. They were updated locally; ignore rules were not changed. Existing unrelated working-tree changes were preserved. Generated Data/Models files and HiramDbContext were not edited.

## Verification

- Application build passed using a separate output directory to avoid the running app's file locks.
- RegistrationChecks build passed.
- All 29 registration/photo/approval checks passed against an isolated temporary SQL Server database, removed afterward.
- Checks include pending login denial, generic activation and edit bypass rejection, stale approval rejection, successful atomic approval, attribution on both records, successful authentication after approval, duplicate approval rejection, missing account handling, rollback on simulated profile-save failure, suspension protection and simultaneous approvals producing exactly one success.
- Existing storage/image and registration checks also passed; storage and email use substitutes.
- Focused whitespace checks passed.

## Limits and follow-up

The actual Admin browser flow, HTTP role enforcement and anti-forgery behavior were source-reviewed but not exercised through a live browser or HTTP test. SQL tests verify service/repository behavior and credential authentication, not issuance of a browser sign-in cookie. Live profile-photo delivery was not tested. A photo-signing failure currently follows the existing Details error path and can prevent that review page loading.

No rejection flow or approval notification email was added. The existing disabled audit-log subsystem remains disabled; approval attribution is UpdatedBy/UpdatedAt, not a historical audit log. Existing AutoMapper vulnerability and scaffolded connection-string warnings remain.

## Try it

Restart the app. Sign in as Admin, open Users & Roles, filter Status to Pending approval, open a registrant, review the details, and approve. Confirm the account is Active, the borrower is Eligible, and the borrower can sign in. Approval does not bypass equipment availability or other borrowing rules.
