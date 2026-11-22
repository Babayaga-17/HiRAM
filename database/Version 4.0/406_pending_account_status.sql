-- Incremental update. Select the existing HiRAM database before running.
-- Preserve the four existing statuses and allow registrations awaiting approval.
SET XACT_ABORT ON;
BEGIN TRY
    BEGIN TRANSACTION;
    IF OBJECT_ID(N'dbo.Accounts', N'U') IS NULL
        THROW 50001, 'Select the HiRAM database containing dbo.Accounts.', 1;
    IF EXISTS (SELECT 1 FROM sys.check_constraints
               WHERE parent_object_id = OBJECT_ID(N'dbo.Accounts') AND name = N'CK_Accounts_status')
        ALTER TABLE dbo.Accounts DROP CONSTRAINT CK_Accounts_status;
    ALTER TABLE dbo.Accounts WITH CHECK ADD CONSTRAINT CK_Accounts_status
        CHECK (account_status IN ('Active', 'Inactive', 'Suspended', 'Disabled', 'PendingApproval'));
    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
