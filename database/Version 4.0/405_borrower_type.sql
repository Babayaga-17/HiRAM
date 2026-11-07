-- Incremental update: select the existing HiRAM database in SSMS before running.
-- Existing borrowers remain NULL until their Student/Faculty type is confirmed.
-- After running, regenerate Data models and HiramDbContext through scaffolding.
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.BorrowerProfiles', N'U') IS NULL
        THROW 50001, 'dbo.BorrowerProfiles was not found. Select the HiRAM database first.', 1;

    IF COL_LENGTH(N'dbo.BorrowerProfiles', N'borrower_type') IS NULL
    BEGIN
        ALTER TABLE dbo.BorrowerProfiles
            ADD borrower_type nvarchar(20) NULL;
    END;

    IF NOT EXISTS (
        SELECT 1
        FROM sys.check_constraints
        WHERE parent_object_id = OBJECT_ID(N'dbo.BorrowerProfiles')
          AND name = N'CK_BorrowerProfiles_borrower_type'
    )
    BEGIN
        -- Compile after adding the column, including on the first execution.
        EXEC sys.sp_executesql N'
            ALTER TABLE dbo.BorrowerProfiles WITH CHECK
            ADD CONSTRAINT CK_BorrowerProfiles_borrower_type
                CHECK (borrower_type IS NULL OR borrower_type IN (N''Student'', N''Faculty''));';
    END;

    ALTER TABLE dbo.BorrowerProfiles WITH CHECK
        CHECK CONSTRAINT CK_BorrowerProfiles_borrower_type;

    COMMIT TRANSACTION;
    PRINT 'Borrower type column is ready. Regenerate models using scaffolding.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;
