-- Run after importing the latest schema into your chosen demo database.
-- Confirm the SSMS database selection before execution.
-- Adds missing departments; preserves existing IDs, names and active states.
-- Programs remain in Resources/Constants/ProgramOptions.cs.
USE HiramDb

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'dbo.Departments', N'U') IS NULL
    THROW 50001, 'Import the HiRAM schema before running this seed.', 1;

INSERT INTO dbo.Departments
    (department_code, department_name, is_active, created_by, created_at)
SELECT
    seed.DepartmentCode,
    seed.DepartmentName,
    1,
    'DemoSeed',
    SYSDATETIME()
FROM (VALUES
    ('CCS', 'College of Computer Studies'),
    ('COE', 'College of Engineering'),
    ('CBA', 'College of Business and Accountancy'),
    ('CED', 'College of Education'),
    ('CAS', 'College of Arts and Sciences'),
    ('CCJE', 'College of Criminal Justice Education'),
    ('CON', 'College of Nursing'),
    ('CAHS', 'College of Allied Health Sciences'),
    ('CHTCA', 'College of Hospitality, Tourism and Customs Administration'),
    ('SOL', 'School of Law')
) AS seed(DepartmentCode, DepartmentName)
WHERE NOT EXISTS (
    SELECT 1
    FROM dbo.Departments AS existing
    WHERE existing.department_code = seed.DepartmentCode
);

SELECT DB_NAME() AS SeedDatabase;

SELECT department_id, department_code, department_name, is_active
FROM dbo.Departments
ORDER BY department_code;
