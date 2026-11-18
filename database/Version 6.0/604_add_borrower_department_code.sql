-- Transitional migration: retain DepartmentId and its foreign key.
-- Pause borrower registration/profile/account edits while running this script.
-- Rerun before cutover if the old application has written new assignments.
SET XACT_ABORT ON;
BEGIN TRY
    BEGIN TRANSACTION;

    IF COL_LENGTH('dbo.BorrowerProfiles', 'department_code') IS NULL
        ALTER TABLE dbo.BorrowerProfiles ADD department_code varchar(20) NULL;

    -- Compile after the column exists. Preserve previously populated codes.
    EXEC sys.sp_executesql N'
        IF EXISTS (
            SELECT 1
            FROM dbo.BorrowerProfiles AS profile
            LEFT JOIN dbo.Departments AS department
                ON department.department_id = profile.department_id
            WHERE department.department_code IS NULL
                OR department.department_code NOT IN
                    (''CCS'', ''COE'', ''CBA'', ''CED'', ''CAS'',
                     ''CCJE'', ''CON'', ''CAHS'', ''CHTCA'', ''SOL'')
                OR (profile.department_code IS NOT NULL
                    AND profile.department_code <> department.department_code)
        )
            THROW 50001, ''A borrower has an unmapped or conflicting department. No changes were committed.'', 1;

        UPDATE profile
        SET profile.department_code = department.department_code
        FROM dbo.BorrowerProfiles AS profile
        INNER JOIN dbo.Departments AS department
            ON department.department_id = profile.department_id
        WHERE profile.department_code IS NULL;

        IF EXISTS (SELECT 1 FROM dbo.BorrowerProfiles WHERE department_code IS NULL)
            THROW 50002, ''Some borrowers have no department code. No changes were committed.'', 1;
    ';

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;

EXEC sys.sp_executesql N'
    SELECT user_id, department_id, department_code, borrower_type, program
    FROM dbo.BorrowerProfiles
    ORDER BY user_id;
';
