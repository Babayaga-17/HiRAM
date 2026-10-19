   SELECT COUNT(*) AS total_borrowers FROM BorrowerProfiles;
   SELECT @@TRANCOUNT AS open_transactions;
   -- Seed ~45 borrowers for testing search. Safe to re-run (skips rows that already exist).
USE HiramDb;
GO
SET XACT_ABORT ON;
BEGIN TRAN;

INSERT INTO Departments (department_code, department_name, is_active, created_by, created_at)
SELECT v.c, v.n, 1, 'seed', SYSDATETIME()
FROM (VALUES
  ('CCS','College of Computer Studies'),
  ('COE','College of Engineering'),
  ('CBA','College of Business and Accountancy'),
  ('CAS','College of Arts and Sciences'),
  ('CTE','College of Teacher Education')
) v(c,n)
WHERE NOT EXISTS (SELECT 1 FROM Departments d WHERE d.department_code = v.c);

INSERT INTO Users (user_code, first_name, last_name, contact_number, created_by, created_at)
SELECT v.code, v.fn, v.ln, v.ph, 'seed', SYSDATETIME()
FROM (VALUES
  ('2024-0004','Jhondel','Presbitero','09414570548'),
  ('2024-0005','Joshua','Mendoza','09638118517'),
  ('2024-0006','Mikaela','Fernandez','09358277203'),
  ('2024-0007','Francis','Cruz','09919994920'),
  ('2024-0008','Gabriel','Tan','09729682115'),
  ('2024-0009','Jose','Delos Santos','09450028352'),
  ('2024-0010','Sofia','Delos Santos','09378490828'),
  ('2024-0011','Mark','Fernandez','09684494331'),
  ('2024-0012','Trisha','Villanueva','09549911297'),
  ('2024-0013','Patricia','Torres','09995710061'),
  ('2024-0014','Katrina','Valdez','09240739294'),
  ('2024-0015','Carlo','Villanueva','09165395729'),
  ('2024-0016','Joshua','Villanueva','09894485254'),
  ('2024-0017','Danica','Cruz','09479872700'),
  ('2024-0018','Mark','Reyes','09591946611'),
  ('2024-0019','Kenneth','Gonzales','09811326932'),
  ('2024-0020','Kristine','Rivera','09726365975'),
  ('2024-0021','Louise','Navarro','09975150085'),
  ('2024-0022','Nicole','Mendoza','09654867725'),
  ('2024-0023','Adrian','Gonzales','09551646166'),
  ('2024-0024','Gabriel','Gonzales','09988134464'),
  ('2024-0025','Bianca','Aquino','09638641453'),
  ('2024-0026','Mikaela','Lim','09240405983'),
  ('2024-0027','Ana','Salazar','09671042709'),
  ('2024-0028','Paolo','Rivera','09263033078'),
  ('2024-0029','Ryan','Torres','09662110918'),
  ('2024-0030','Paolo','Castillo','09648195686'),
  ('2024-0031','Kenneth','Mendoza','09120084195'),
  ('2024-0032','Danica','Rivera','09572580523'),
  ('2024-0033','Daniel','Rivera','09933767140'),
  ('2024-0034','Mikaela','Mendoza','09296610599'),
  ('2024-0035','Angela','Pascual','09753430573'),
  ('2024-0036','Adrian','Ramos','09104222468'),
  ('2024-0037','Rafael','Ramos','09933265493'),
  ('2024-0038','Erika','Castillo','09958102737'),
  ('2024-0039','Rafael','Pascual','09260849193'),
  ('2024-0040','Adrian','Castillo','09285055879'),
  ('2024-0041','Rafael','Villanueva','09251997788'),
  ('2024-0042','Adrian','Tan','09608409165'),
  ('2024-0043','Rafael','Rivera','09764754893'),
  ('2024-0044','Danica','Navarro','09878670347'),
  ('2024-0045','Maria','Mendoza','09229210455'),
  ('2024-0046','Christian','Gonzales','09697511159'),
  ('2024-0047','Miguel','Domingo','09166309234')
) v(code,fn,ln,ph)
WHERE NOT EXISTS (SELECT 1 FROM Users u WHERE u.user_code = v.code);

INSERT INTO BorrowerProfiles (user_id, department_id, program, year_level, eligibility_status,
                              suspension_reason, suspended_at, suspended_until, created_by, created_at)
SELECT u.user_id, d.department_id, v.prog, v.yr, v.st, v.reason,
       CASE WHEN v.st = 'Suspended' THEN SYSDATETIME() END,
       CASE WHEN v.st = 'Suspended' THEN DATEADD(day, 7 + (u.user_id % 20), SYSDATETIME()) END,
       'seed', SYSDATETIME()
FROM (VALUES
  ('2024-0004','CCS','BSCS',2,'Eligible',NULL),
  ('2024-0005','CAS','ABComm',1,'Suspended','Unreturned equipment'),
  ('2024-0006','CCS','BSIS',2,'Suspended','Pending clearance'),
  ('2024-0007','COE','BSCE',4,'Suspended','Unreturned equipment'),
  ('2024-0008','CTE','BEED',4,'Suspended','Damaged equipment'),
  ('2024-0009','COE','BSEE',4,'Suspended','Unreturned equipment'),
  ('2024-0010','COE','BSCE',2,'Eligible',NULL),
  ('2024-0011','CCS','BSIS',2,'Eligible',NULL),
  ('2024-0012','CAS','ABComm',4,'Eligible',NULL),
  ('2024-0013','COE','BSCE',3,'Eligible',NULL),
  ('2024-0014','CAS','BSBio',1,'Suspended','Pending clearance'),
  ('2024-0015','COE','BSEE',4,'Suspended','Unreturned equipment'),
  ('2024-0016','CBA','BSHM',4,'Eligible',NULL),
  ('2024-0017','CCS','BSIT',4,'Eligible',NULL),
  ('2024-0018','CBA','BSHM',4,'Eligible',NULL),
  ('2024-0019','CBA','BSBA',4,'Eligible',NULL),
  ('2024-0020','CCS','BSCS',3,'Suspended','Damaged equipment'),
  ('2024-0021','CAS','BSPsych',2,'Eligible',NULL),
  ('2024-0022','CAS','ABComm',3,'Eligible',NULL),
  ('2024-0023','CAS','BSPsych',2,'Suspended','Damaged equipment'),
  ('2024-0024','COE','BSCE',4,'Eligible',NULL),
  ('2024-0025','CBA','BSBA',2,'Eligible',NULL),
  ('2024-0026','CTE','BSEd English',2,'Eligible',NULL),
  ('2024-0027','CTE','BSEd English',4,'Eligible',NULL),
  ('2024-0028','CAS','BSPsych',2,'Suspended','Damaged equipment'),
  ('2024-0029','CCS','BSIT',1,'Suspended','Damaged equipment'),
  ('2024-0030','CTE','BEED',1,'Eligible',NULL),
  ('2024-0031','CBA','BSA',3,'Eligible',NULL),
  ('2024-0032','CAS','BSBio',1,'Suspended','Overdue returns'),
  ('2024-0033','COE','BSME',1,'Eligible',NULL),
  ('2024-0034','CTE','BEED',3,'Eligible',NULL),
  ('2024-0035','CBA','BSHM',3,'Eligible',NULL),
  ('2024-0036','CTE','BSEd Math',3,'Eligible',NULL),
  ('2024-0037','CAS','ABComm',2,'Eligible',NULL),
  ('2024-0038','CCS','BSCS',3,'Eligible',NULL),
  ('2024-0039','CTE','BSEd English',4,'Eligible',NULL),
  ('2024-0040','CCS','BSCS',1,'Eligible',NULL),
  ('2024-0041','COE','BSEE',1,'Eligible',NULL),
  ('2024-0042','CCS','BSIS',1,'Eligible',NULL),
  ('2024-0043','COE','BSEE',3,'Suspended','Pending clearance'),
  ('2024-0044','CCS','BSIS',2,'Suspended','Damaged equipment'),
  ('2024-0045','CTE','BSEd English',2,'Eligible',NULL),
  ('2024-0046','CBA','BSBA',2,'Suspended','Unreturned equipment'),
  ('2024-0047','COE','BSCE',1,'Eligible',NULL)
) v(code,dept,prog,yr,st,reason)
JOIN Users u ON u.user_code = v.code
JOIN Departments d ON d.department_code = v.dept
WHERE NOT EXISTS (SELECT 1 FROM BorrowerProfiles b WHERE b.user_id = u.user_id);

COMMIT;

SELECT COUNT(*) AS total_borrowers FROM BorrowerProfiles;