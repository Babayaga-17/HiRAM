-- Sample data for testing Approval and Release (3.4x).
-- Adds 12 equipment items and 15 reservations in every stage:
--   4 pending, 3 approved (ready for release), 2 rejected, 3 borrowed (1 overdue), 2 returned, 1 cancelled.
-- Uses borrowers that already have a borrower profile (eligible ones, picked automatically) and the TEST-ADMIN / TEST-CUSTODIAN users.
-- Safe to re-run: rows that already exist are skipped.
USE HiramDb;
GO
SET XACT_ABORT ON;
BEGIN TRAN;

-- >>> If your database uses different words, change them here (see the CHECK constraint query in the notes) <<<
DECLARE @Pending   varchar(20) = 'Pending';
DECLARE @Approved  varchar(20) = 'Approved';
DECLARE @Rejected  varchar(20) = 'Rejected';
DECLARE @Borrowed  varchar(20) = 'Borrowed';
DECLARE @Returned  varchar(20) = 'Returned';
DECLARE @Cancelled varchar(20) = 'Cancelled';
DECLARE @CondGood  varchar(30) = 'Good';

DECLARE @admin     int = ISNULL((SELECT user_id FROM Users WHERE user_code = 'TEST-ADMIN'),     (SELECT MIN(user_id) FROM Users));
DECLARE @custodian int = ISNULL((SELECT user_id FROM Users WHERE user_code = 'TEST-CUSTODIAN'), @admin);
DECLARE @t8 datetime2 = DATEADD(hour, 8, CAST(CAST(SYSDATETIME() AS date) AS datetime2));   -- today at 8:00

-- 1) Equipment ---------------------------------------------------------------
INSERT INTO Equipments (equipment_code, asset_tag, equipment_name, brand, model, serial_number,
                        condition_status, storage_location, is_active, created_by, created_at)
SELECT v.code, v.tag, v.nm, v.brand, v.model, v.serial, @CondGood, v.loc, 1, 'seed', SYSDATETIME()
FROM (VALUES
  ('EQ-101','TAG-101','Projector',           'Epson',    'EB-X06',     'SN-EP-1001','AV Room'),
  ('EQ-102','TAG-102','Laptop',              'Lenovo',   'ThinkPad E14','SN-LN-1002','IT Office'),
  ('EQ-103','TAG-103','Extension Cord',      'Omni',     '5-gang',     'SN-OM-1003','AV Room'),
  ('EQ-104','TAG-104','DSLR Camera',         'Canon',    'EOS 200D',   'SN-CN-1004','Media Room'),
  ('EQ-105','TAG-105','Camera Tripod',       'Velbon',   'EX-440',     'SN-VB-1005','Media Room'),
  ('EQ-106','TAG-106','Portable Speaker',    'JBL',      'PartyBox 110','SN-JB-1006','AV Room'),
  ('EQ-107','TAG-107','Wireless Microphone', 'Shure',    'BLX24',      'SN-SH-1007','AV Room'),
  ('EQ-108','TAG-108','Tablet',              'Samsung',  'Galaxy Tab A8','SN-SM-1008','IT Office'),
  ('EQ-109','TAG-109','HDMI Cable',          'Ugreen',   '2m',         'SN-UG-1009','AV Room'),
  ('EQ-110','TAG-110','Arduino Kit',         'Arduino',  'Starter Kit','SN-AR-1010','Robotics Lab'),
  ('EQ-111','TAG-111','Wi-Fi Router',        'TP-Link',  'Archer C6',  'SN-TP-1011','IT Office'),
  ('EQ-112','TAG-112','Laptop',              'Acer',     'Aspire 5',   'SN-AC-1012','IT Office')
) v(code, tag, nm, brand, model, serial, loc)
WHERE NOT EXISTS (SELECT 1 FROM Equipments e WHERE e.equipment_code = v.code);

-- Pick existing, eligible borrowers (only people who really have a borrower profile)
DECLARE @B TABLE (n int, user_id int);
INSERT INTO @B (n, user_id)
SELECT ROW_NUMBER() OVER (ORDER BY b.user_id), b.user_id
FROM BorrowerProfiles b
WHERE b.eligibility_status = 'Eligible';
DECLARE @bc int = (SELECT COUNT(*) FROM @B);
IF @bc = 0 THROW 50000, 'No eligible borrowers found. Run seed_borrowers.sql first.', 1;

-- 2) Reservations ------------------------------------------------------------
-- st: P = pending, A = approved, R = rejected, B = borrowed, T = returned, C = cancelled
-- bn = borrower number (1 = first eligible borrower, 2 = second, ...)
-- sd / rd = start / return day offset from today (return time is 17:00)
DECLARE @R TABLE (num varchar(30), bn int, purpose varchar(500), st char(1),
                  sd int, rd int, i1 varchar(30), i2 varchar(30), note varchar(500));
INSERT INTO @R VALUES
  ('RES-1001',1,'Class presentation',          'P',  1,  1,'EQ-101','EQ-103',NULL),
  ('RES-1002',2,'Thesis defense rehearsal',    'P',  2,  3,'EQ-102',NULL,    NULL),
  ('RES-1003',3,'Group video project',         'P',  1,  2,'EQ-104','EQ-105',NULL),
  ('RES-1004',4,'Event sound system',          'P',  3,  3,'EQ-106','EQ-107',NULL),
  ('RES-1005',5,'Seminar attendance sheet app','A',  0,  1,'EQ-108',NULL,    NULL),
  ('RES-1006',6,'Robotics workshop',           'A',  0,  2,'EQ-110',NULL,    NULL),
  ('RES-1007',7,'Class demo',                  'A',  1,  1,'EQ-111',NULL,    NULL),
  ('RES-1008',8,'Personal project',            'R',  2,  2,'EQ-102',NULL,    'Equipment reserved for another class'),
  ('RES-1009',9,'Outdoor event',               'R',  4,  4,'EQ-106',NULL,    'Incomplete request details'),
  ('RES-1010',10,'Research interview',          'B', -1,  1,'EQ-109',NULL,    NULL),
  ('RES-1011',11,'Lab activity',                'B', -3, -1,'EQ-112',NULL,    NULL),   -- overdue
  ('RES-1012',12,'Club meeting',                'B', -2,  0,'EQ-103',NULL,    NULL),   -- due today
  ('RES-1013',10,'Class presentation',          'T',-10, -9,'EQ-101',NULL,    NULL),
  ('RES-1014',11,'Photography class',           'T', -8, -7,'EQ-104','EQ-105',NULL),
  ('RES-1015',12,'Schedule changed',            'C',  5,  5,'EQ-108',NULL,    NULL);

INSERT INTO Reservations (reservation_number, borrower_user_id, purpose, start_date_time, expected_return_date_time,
                          status, requested_at, approved_by_user_id, approved_at, rejected_by_user_id, rejected_at,
                          rejection_reason, borrowed_at, returned_at, received_by_user_id, cancelled_at,
                          cancellation_reason, created_by, created_at)
SELECT r.num, u.user_id, r.purpose,
       DATEADD(day, r.sd, @t8),
       DATEADD(hour, 9, DATEADD(day, r.rd, @t8)),
       CASE r.st WHEN 'P' THEN @Pending WHEN 'A' THEN @Approved WHEN 'R' THEN @Rejected
                 WHEN 'B' THEN @Borrowed WHEN 'T' THEN @Returned ELSE @Cancelled END,
       DATEADD(day, r.sd - 2, SYSDATETIME()),
       CASE WHEN r.st IN ('A','B','T') THEN @admin END,
       CASE WHEN r.st IN ('A','B','T') THEN DATEADD(day, r.sd - 1, SYSDATETIME()) END,
       CASE WHEN r.st = 'R' THEN @admin END,
       CASE WHEN r.st = 'R' THEN DATEADD(day, -1, SYSDATETIME()) END,
       CASE WHEN r.st = 'R' THEN r.note END,
       CASE WHEN r.st IN ('B','T') THEN DATEADD(day, r.sd, @t8) END,
       CASE WHEN r.st = 'T' THEN DATEADD(hour, 9, DATEADD(day, r.rd, @t8)) END,
       CASE WHEN r.st = 'T' THEN @custodian END,
       CASE WHEN r.st = 'C' THEN DATEADD(day, -1, SYSDATETIME()) END,
       CASE WHEN r.st = 'C' THEN 'Schedule changed' END,
       'seed', SYSDATETIME()
FROM @R r
JOIN @B u ON u.n = ((r.bn - 1) % @bc) + 1
WHERE NOT EXISTS (SELECT 1 FROM Reservations x WHERE x.reservation_number = r.num);

-- 3) Reservation items -------------------------------------------------------
INSERT INTO ReservationItems (reservation_id, equipment_id, condition_at_borrowing, is_returned,
                              returned_at, condition_at_return, created_by, created_at)
SELECT res.reservation_id, e.equipment_id,
       CASE WHEN r.st IN ('B','T') THEN @CondGood END,
       CASE WHEN r.st = 'T' THEN 1 ELSE 0 END,
       CASE WHEN r.st = 'T' THEN res.returned_at END,
       CASE WHEN r.st = 'T' THEN @CondGood END,
       'seed', SYSDATETIME()
FROM @R r
JOIN Reservations res ON res.reservation_number = r.num
CROSS APPLY (VALUES (r.i1), (r.i2)) x(code)
JOIN Equipments e ON e.equipment_code = x.code
WHERE x.code IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM ReservationItems ri
                  WHERE ri.reservation_id = res.reservation_id AND ri.equipment_id = e.equipment_id);

COMMIT;

-- Check the result
SELECT status, COUNT(*) AS reservations FROM Reservations GROUP BY status;
SELECT COUNT(*) AS equipment_items FROM Equipments;