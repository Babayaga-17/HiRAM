-- Incremental change. Run against the existing HiRAM database before using maintenance.
IF COL_LENGTH('dbo.MaintenanceRecords', 'expected_completion_date_time') IS NULL
BEGIN
    ALTER TABLE dbo.MaintenanceRecords ADD expected_completion_date_time datetime2(7) NULL;
END;
GO
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_MaintenanceRecords_expected_completion')
BEGIN
    ALTER TABLE dbo.MaintenanceRecords ADD CONSTRAINT CK_MaintenanceRecords_expected_completion
        CHECK (expected_completion_date_time IS NULL OR expected_completion_date_time > start_date_time);
END;
