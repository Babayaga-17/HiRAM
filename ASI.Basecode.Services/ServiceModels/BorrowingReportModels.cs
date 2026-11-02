using System;
using System.Collections.Generic;

namespace ASI.Basecode.Services.ServiceModels
{
    public class BorrowingReportFilter
    {
        public string Search { get; set; }
        public int? BorrowerId { get; set; }
        public int? EquipmentId { get; set; }
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
        public string Status { get; set; }
        public bool LateOnly { get; set; }
    }

    public class BorrowingExportOptions
    {
        public string Orientation { get; set; } = "Landscape";
        public bool IncludeSummary { get; set; } = true;
        public bool ShowFilters { get; set; } = true;
        public string FileName { get; set; }
        public bool Landscape => !string.Equals(Orientation, "Portrait", StringComparison.OrdinalIgnoreCase);
    }

    public class ReportOption
    {
        public string Value { get; set; }
        public string Text { get; set; }
    }

    public class BorrowingReportRow
    {
        public int ReservationId { get; set; }
        public string ReservationNumber { get; set; }
        public string Purpose { get; set; }
        public string BorrowerName { get; set; }
        public string Department { get; set; }
        public string EquipmentSummary { get; set; }
        public DateTime StartDateTime { get; set; }
        public DateTime ExpectedReturnDateTime { get; set; }
        public DateTime? BorrowedAt { get; set; }
        public DateTime? ReturnedAt { get; set; }
        public string Status { get; set; }          // status stored in the database
        public string DisplayStatus { get; set; }   // what the report shows (Overdue is worked out)
        public string LateText { get; set; }        // null when on time
        public string StatusClass => (DisplayStatus ?? "").ToLowerInvariant();
        public bool IsLate => LateText != null;
    }

    public class BorrowingReportSummary
    {
        public int TotalRecords { get; set; }
        public int ReturnedCount { get; set; }
        public int CurrentlyBorrowed { get; set; }
        public int LateReturns { get; set; }
        public string AverageDuration { get; set; } = "—";
    }

    // A null value means "All".
    public class BorrowingFilterLabels
    {
        public string Period { get; set; } = "All dates";
        public string Borrower { get; set; }
        public string Equipment { get; set; }
        public string Status { get; set; }
        public string Search { get; set; }
        public bool LateOnly { get; set; }
    }

    public class BorrowingReportViewModel
    {
        public BorrowingReportFilter Filter { get; set; } = new();
        public List<BorrowingReportRow> Rows { get; set; } = new();
        public BorrowingReportSummary Summary { get; set; } = new();
        public BorrowingFilterLabels Labels { get; set; } = new();
        public List<ReportOption> Borrowers { get; set; } = new();
        public List<ReportOption> Equipments { get; set; } = new();
        public List<ReportOption> Statuses { get; set; } = new();
        public string DefaultFileName { get; set; } = "HiRAM-Borrowing-Report";
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 8;
        public int TotalPages { get; set; } = 1;
        public int FirstItem => Summary.TotalRecords == 0 ? 0 : (Page - 1) * PageSize + 1;
        public int LastItem => Math.Min(Page * PageSize, Summary.TotalRecords);
    }

    public class PrintableBorrowingReport
    {
        public string Title { get; set; } = "Borrowing History Report";
        public string GeneratedAt { get; set; }
        public string GeneratedBy { get; set; }
        public string FileName { get; set; }
        public BorrowingExportOptions Options { get; set; } = new();
        public BorrowingFilterLabels Labels { get; set; } = new();
        public BorrowingReportSummary Summary { get; set; } = new();
        public List<BorrowingReportRow> Rows { get; set; } = new();
        public List<List<BorrowingReportRow>> Pages { get; set; } = new();
    }

    public class BorrowingRecordItem
    {
        public string Name { get; set; }
        public string Code { get; set; }
        public string Category { get; set; }
        public string SerialNumber { get; set; }
        public string StorageLocation { get; set; }
    }

    public class BorrowingTimelineStep
    {
        public string Title { get; set; }
        public string When { get; set; }
        public string State { get; set; }   // done, alert or pending
    }

    public class BorrowingRecordDetails
    {
        public string ReservationNumber { get; set; }
        public string StatusLabel { get; set; }
        public string StatusClass { get; set; }
        public string BorrowerName { get; set; }
        public string Department { get; set; }
        public string ProgramYear { get; set; }
        public string ContactNumber { get; set; }
        public List<BorrowingRecordItem> Items { get; set; } = new();
        public List<BorrowingTimelineStep> Timeline { get; set; } = new();
        public string Purpose { get; set; }
        public string ConditionBorrowed { get; set; }
        public string ConditionReturned { get; set; }
        public string ReceivedBy { get; set; }
        public string Remarks { get; set; }
        public bool ActionNeeded { get; set; }
    }
}