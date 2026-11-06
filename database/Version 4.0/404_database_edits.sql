-- Incremental update: select the existing HiRAM database in SSMS before running.
-- Stores a private storage object path, not image bytes or a signed URL.
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.Users', N'U') IS NULL
        THROW 50001, 'dbo.Users was not found. Select the HiRAM database first.', 1;

    IF COL_LENGTH(N'dbo.Users', N'profile_photo_storage_path') IS NULL
    BEGIN
        ALTER TABLE dbo.Users
            ADD profile_photo_storage_path nvarchar(500) NULL;
    END;

    COMMIT TRANSACTION;
    PRINT 'Profile photo column is ready. Regenerate models using scaffolding.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;
