-- Run against the HiRAM application database. No schema changes are required.
-- Existing department IDs, names, active states, and borrower assignments remain intact.
SET XACT_ABORT ON;
BEGIN TRANSACTION;

DECLARE @Departments TABLE (
    department_code varchar(20) NOT NULL,
    department_name varchar(150) NOT NULL
);

INSERT INTO @Departments (department_code, department_name)
VALUES
    ('CCS', 'College of Computer Studies'),
    ('COE', 'College of Engineering'),
    ('CBA', 'College of Business and Accountancy'),
    ('CED', 'College of Education'),
    ('CAS', 'College of Arts and Sciences'),
    ('CCJE', 'College of Criminal Justice Education'),
    ('CON', 'College of Nursing'),
    ('CAHS', 'College of Allied Health Sciences'),
    ('CHTCA', 'College of Hospitality, Tourism and Customs Administration'),
    ('SOL', 'School of Law');

-- Stop for manual mapping rather than create a duplicate or rename an existing record.
IF EXISTS (
    SELECT 1
    FROM @Departments AS proposed
    INNER JOIN dbo.Departments AS existing
        ON existing.department_code = proposed.department_code
        OR existing.department_name = proposed.department_name
    WHERE existing.department_code <> proposed.department_code
        OR existing.department_name <> proposed.department_name
)
BEGIN
    ROLLBACK TRANSACTION;
    THROW 50001, 'An existing department has a conflicting code or name. Resolve the mapping before continuing.', 1;
END;

INSERT INTO dbo.Departments
    (department_code, department_name, is_active, created_by, created_at)
SELECT proposed.department_code, proposed.department_name, 1,
       'Academic catalog setup', SYSDATETIME()
FROM @Departments AS proposed
WHERE NOT EXISTS (
    SELECT 1
    FROM dbo.Departments AS existing WITH (UPDLOCK, HOLDLOCK)
    WHERE existing.department_code = proposed.department_code
);

COMMIT TRANSACTION;

SELECT department_id, department_code, department_name, is_active
FROM dbo.Departments
ORDER BY department_name;
