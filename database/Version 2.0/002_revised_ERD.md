# Proposed HiRAM ERD

This diagram matches `proposed_hiram_schema.sql`. The database uses a borrower
profile for each reservation, and an approved reservation is a distinct state.
Each equipment row represents one physical asset.

```mermaid
erDiagram
    USERS {
        int user_id PK
        varchar user_code UK
    }
    ROLES {
        tinyint role_id PK
    }
    ACCOUNTS {
        int account_id PK
        int user_id FK,UK
        tinyint role_id FK
    }
    DEPARTMENTS {
        int department_id PK
    }
    BORROWER_PROFILES {
        int user_id PK,FK
        int department_id FK
        int suspended_by_user_id FK
        varchar eligibility_status
    }
    EQUIPMENT_CATEGORIES {
        int category_id PK
    }
    EQUIPMENTS {
        int equipment_id PK
        int category_id FK
        varchar equipment_code UK
        varchar condition_status
        bit is_active
    }
    MAINTENANCE_RECORDS {
        int maintenance_id PK
        int equipment_id FK
        int reported_by_user_id FK
        int completed_by_user_id FK
    }
    PASSWORD_RESET_TOKENS {
        int token_id PK
        int account_id FK
    }
    AUDIT_LOGS {
        bigint audit_log_id PK
        int user_id FK
    }
    RESERVATIONS {
        int reservation_id PK
        int borrower_user_id FK
        int approved_by_user_id FK
        int rejected_by_user_id FK
        int received_by_user_id FK
        varchar status
        datetime2 start_date_time
        datetime2 expected_return_date_time
    }
    RESERVATION_ITEMS {
        int reservation_item_id PK
        int reservation_id FK
        int equipment_id FK
        bit is_returned
    }

    USERS ||--o| ACCOUNTS : has
    ROLES ||--o{ ACCOUNTS : grants
    USERS ||--o| BORROWER_PROFILES : has
    DEPARTMENTS ||--o{ BORROWER_PROFILES : includes
    USERS o|--o{ BORROWER_PROFILES : suspends
    USERS o|--o{ AUDIT_LOGS : acts_in
    ACCOUNTS ||--o{ PASSWORD_RESET_TOKENS : receives
    EQUIPMENT_CATEGORIES ||--o{ EQUIPMENTS : classifies
    EQUIPMENTS ||--o{ MAINTENANCE_RECORDS : receives
    USERS o|--o{ MAINTENANCE_RECORDS : reports
    USERS o|--o{ MAINTENANCE_RECORDS : completes
    BORROWER_PROFILES ||--o{ RESERVATIONS : requests
    USERS o|--o{ RESERVATIONS : approves
    USERS o|--o{ RESERVATIONS : rejects
    USERS o|--o{ RESERVATIONS : receives
    RESERVATIONS ||--o{ RESERVATION_ITEMS : contains
    EQUIPMENTS ||--o{ RESERVATION_ITEMS : appears_in
```

## Rules handled in application services

- `(reservation_id, equipment_id)` is unique in the database, so an asset occurs
  only once within a reservation.
- Before approval, check borrower eligibility, at least one item, equipment
  condition, overlapping approved bookings, and scheduled maintenance.
- Before scheduling maintenance, check for overlapping bookings and maintenance.
- Before completing a reservation, check that every item has a return record.
- Do not change the equipment list after the reservation leaves `Pending`.
- Make approval and maintenance scheduling atomic. Use a database transaction with
  suitable isolation or locking, and retry conflicts. A separate availability
  read followed by a later save can approve two requests for the same item.
- `Equipments.availability_status` is intentionally omitted. Current and future
  availability come from reservations, returns, maintenance, and `is_active`.

The SQL file contains no triggers or stored procedures. The service layer owns
the workflow checks listed above. Basic foreign keys, uniqueness, and row-level
checks remain in the database so invalid values and broken links are rejected.

This is a proposed clean-install design. It is not a migration for the current
database, and the existing generated EF models need corresponding changes.
