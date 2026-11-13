use HiramDb;
-- Incremental update: select the existing HiRAM database before running.
-- No existing user/reservation records are modified. All timestamps are UTC.
-- After applying, scaffold the entities and context; do not edit generated files manually.
SET XACT_ABORT ON;
BEGIN TRY
    BEGIN TRANSACTION;
    IF OBJECT_ID(N'dbo.Users', N'U') IS NULL OR OBJECT_ID(N'dbo.Reservations', N'U') IS NULL
        THROW 50001, 'Select the HiRAM database containing Users and Reservations.', 1;

    IF OBJECT_ID(N'dbo.Notifications', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.Notifications (
            notification_id bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_Notifications PRIMARY KEY,
            recipient_user_id int NOT NULL,
            -- Reuse the same event GUID for every recipient/retry of one committed event.
            event_key uniqueidentifier NOT NULL,
            event_type nvarchar(40) NOT NULL,
            related_user_id int NULL,
            reservation_id int NULL,
            title nvarchar(200) NOT NULL,
            body nvarchar(2000) NOT NULL,
            created_at datetime2(7) NOT NULL CONSTRAINT DF_Notifications_created_at DEFAULT SYSUTCDATETIME(),
            read_at datetime2(7) NULL,
            CONSTRAINT FK_Notifications_Recipient FOREIGN KEY(recipient_user_id) REFERENCES dbo.Users(user_id),
            CONSTRAINT FK_Notifications_RelatedUser FOREIGN KEY(related_user_id) REFERENCES dbo.Users(user_id),
            CONSTRAINT FK_Notifications_Reservation FOREIGN KEY(reservation_id) REFERENCES dbo.Reservations(reservation_id),
            CONSTRAINT UQ_Notifications_EventRecipient UNIQUE(event_key, recipient_user_id),
            CONSTRAINT CK_Notifications_event_type CHECK(event_type IN
                (N'RegistrationSubmitted', N'RegistrationApproved', N'ReservationSubmitted',
                 N'ReservationApproved', N'ReservationRejected', N'ReservationCancelled')),
            CONSTRAINT CK_Notifications_reference CHECK
                ((event_type IN (N'RegistrationSubmitted', N'RegistrationApproved') AND related_user_id IS NOT NULL AND reservation_id IS NULL)
                 OR (event_type IN (N'ReservationSubmitted', N'ReservationApproved', N'ReservationRejected', N'ReservationCancelled') AND reservation_id IS NOT NULL AND related_user_id IS NULL)),
            CONSTRAINT CK_Notifications_content CHECK(LEN(LTRIM(RTRIM(title))) > 0 AND LEN(LTRIM(RTRIM(body))) > 0),
            CONSTRAINT CK_Notifications_read_at CHECK(read_at IS NULL OR read_at >= created_at)
        );
        CREATE INDEX IX_Notifications_RecipientCreated ON dbo.Notifications(recipient_user_id, created_at DESC, notification_id DESC);
        CREATE INDEX IX_Notifications_Unread ON dbo.Notifications(recipient_user_id, notification_id DESC) WHERE read_at IS NULL;
    END;

    IF OBJECT_ID(N'dbo.NotificationEmails', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.NotificationEmails (
            notification_email_id bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_NotificationEmails PRIMARY KEY,
            notification_id bigint NOT NULL CONSTRAINT UQ_NotificationEmails_Notification UNIQUE,
            -- Snapshot the destination/content; never store passwords, keys or signed URLs.
            recipient_email nvarchar(255) NOT NULL,
            subject nvarchar(200) NOT NULL,
            body nvarchar(4000) NOT NULL,
            delivery_status nvarchar(20) NOT NULL CONSTRAINT DF_NotificationEmails_status DEFAULT N'Pending',
            attempt_count int NOT NULL CONSTRAINT DF_NotificationEmails_attempts DEFAULT 0,
            next_attempt_at datetime2(7) NOT NULL CONSTRAINT DF_NotificationEmails_next DEFAULT SYSUTCDATETIME(),
            lease_token uniqueidentifier NULL,
            locked_until datetime2(7) NULL,
            sent_at datetime2(7) NULL,
            last_error nvarchar(500) NULL,
            created_at datetime2(7) NOT NULL CONSTRAINT DF_NotificationEmails_created DEFAULT SYSUTCDATETIME(),
            CONSTRAINT FK_NotificationEmails_Notification FOREIGN KEY(notification_id) REFERENCES dbo.Notifications(notification_id),
            CONSTRAINT CK_NotificationEmails_status CHECK(delivery_status IN (N'Pending', N'Processing', N'Sent', N'Failed')),
            CONSTRAINT CK_NotificationEmails_attempts CHECK(attempt_count >= 0),
            CONSTRAINT CK_NotificationEmails_content CHECK(LEN(LTRIM(RTRIM(recipient_email))) > 0 AND LEN(LTRIM(RTRIM(subject))) > 0 AND LEN(LTRIM(RTRIM(body))) > 0),
            CONSTRAINT CK_NotificationEmails_lease CHECK
                ((delivery_status = N'Processing' AND lease_token IS NOT NULL AND locked_until IS NOT NULL)
                 OR (delivery_status <> N'Processing' AND lease_token IS NULL AND locked_until IS NULL)),
            CONSTRAINT CK_NotificationEmails_sent CHECK
                ((delivery_status = N'Sent' AND sent_at IS NOT NULL)
                 OR (delivery_status <> N'Sent' AND sent_at IS NULL))
        );
        CREATE INDEX IX_NotificationEmails_Dispatch ON dbo.NotificationEmails(delivery_status, next_attempt_at, notification_email_id);
        CREATE INDEX IX_NotificationEmails_Lease ON dbo.NotificationEmails(delivery_status, locked_until);
    END;

    COMMIT TRANSACTION;
    PRINT 'Notification tables are ready. Scaffold models/context before implementing application notifications.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
