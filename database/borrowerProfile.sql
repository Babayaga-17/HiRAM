USE HiramDb;
SET XACT_ABORT ON;
BEGIN TRAN;

IF NOT EXISTS (SELECT 1 FROM Departments WHERE department_code = 'CCS')
    INSERT INTO Departments (department_code, department_name, is_active, created_by, created_at)
    VALUES ('CCS', 'College of Computer Studies', 1, 'seed', SYSDATETIME());

INSERT INTO Users (user_code, first_name, last_name, contact_number, created_by, created_at)
SELECT v.code, v.fn, v.ln, v.ph, 'seed', SYSDATETIME()
FROM (VALUES ('2024-0001','Juan','Dela Cruz','09171234567'),
             ('2024-0002','Maria','Santos','09181234567'),
             ('2024-0003','Pedro','Reyes','09191234567'), 
             ('2024-0004', 'Presbitero','Jhondel', '09949559558')) v(code,fn,ln,ph)
WHERE NOT EXISTS (SELECT 1 FROM Users u WHERE u.user_code = v.code);

INSERT INTO BorrowerProfiles (user_id, department_id, program, year_level, eligibility_status,
                              suspension_reason, suspended_at, suspended_until, created_by, created_at)
SELECT u.user_id, d.department_id, v.prog, v.yr, v.st, v.reason,
       CASE WHEN v.st = 'Suspended' THEN SYSDATETIME() END,
       CASE WHEN v.st = 'Suspended' THEN DATEADD(day, 14, SYSDATETIME()) END,
       'seed', SYSDATETIME()
FROM (VALUES ('2024-0001','BSCS',3,'Eligible',NULL),
             ('2024-0002','BSIT',2,'Eligible',NULL),
             ('2024-0003','BSCS',4,'Suspended','Unreturned equipment'), 
             ('2024-0004', 'BSCS',2,'Eligible', NULL)) v(code,prog,yr,st,reason)
JOIN Users u ON u.user_code = v.code
CROSS JOIN Departments d
WHERE d.department_code = 'CCS'
  AND NOT EXISTS (SELECT 1 FROM BorrowerProfiles b WHERE b.user_id = u.user_id);

COMMIT;