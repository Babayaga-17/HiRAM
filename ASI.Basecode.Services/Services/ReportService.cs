using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;

namespace ASI.Basecode.Services.Services
{
    public class ReportService : IReportService
    {
        private const int MaxExportRows = 5000;
        private static readonly string[] StatusOptions = { ReservationStatuses.Returned, ReservationStatuses.Overdue, ReservationStatuses.Borrowed, ReservationStatuses.Approved, ReservationStatuses.Pending, ReservationStatuses.Rejected, ReservationStatuses.Cancelled };

        private readonly IReportRepository _reportRepository;

        public ReportService(IReportRepository reportRepository)
        {
            _reportRepository = reportRepository;
        }

        // View, Search, Filters, Generate

        public BorrowingReportViewModel GetBorrowingReport(BorrowingReportFilter filter, int page, int pageSize)
        {
            filter = Normalize(filter);
            var now = DateTime.Now;
            var query = ApplyFilter(_reportRepository.GetBorrowingRecords(), filter, now);

            var summary = BuildSummary(query, now);
            var totalPages = Math.Max(1, (int)Math.Ceiling(summary.TotalRecords / (double)pageSize));
            page = Math.Min(Math.Max(page, 1), totalPages);

            var rows = query
                .OrderByDescending(reservation => reservation.ReservationId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList()
                .Select(reservation => ToRow(reservation, now))
                .ToList();

            var borrowers = BorrowerOptions();
            var equipments = EquipmentOptions();

            return new BorrowingReportViewModel
            {
                Filter = filter,
                Rows = rows,
                Summary = summary,
                Labels = BuildLabels(filter, borrowers, equipments),
                Borrowers = borrowers,
                Equipments = equipments,
                Statuses = StatusOptions.Select(status => new ReportOption { Value = status, Text = status }).ToList(),
                DefaultFileName = DefaultFileName(filter, now),
                Page = page,
                PageSize = pageSize,
                TotalPages = totalPages
            };
        }

        // View borrowing details

        public BorrowingRecordDetails GetBorrowingRecordDetails(int reservationId)
        {
            var reservation = _reportRepository.GetBorrowingRecordById(reservationId);
            if (reservation == null)
                return null;

            var row = ToRow(reservation, DateTime.Now);
            var profile = reservation.BorrowerUser;

            var programYear = string.Join(" · ", new[]
            {
                profile?.Program,
                profile?.YearLevel != null ? "Year " + profile.YearLevel : null
            }.Where(value => !string.IsNullOrWhiteSpace(value)));

            var timeline = new List<BorrowingTimelineStep>
            {
                Step("Requested", Stamp(reservation.RequestedAt), "done")
            };

            if (reservation.RejectedAt.HasValue)
                timeline.Add(Step("Rejected by " + FullName(reservation.RejectedByUser),
                    Stamp(reservation.RejectedAt.Value) + Reason(reservation.RejectionReason), "alert"));
            else if (reservation.ApprovedAt.HasValue)
                timeline.Add(Step("Approved by " + FullName(reservation.ApprovedByUser), Stamp(reservation.ApprovedAt.Value), "done"));
            else if (reservation.Status == ReservationStatuses.Pending)
                timeline.Add(Step("Awaiting approval", "Not yet reviewed", "pending"));

            if (reservation.CancelledAt.HasValue)
            {
                timeline.Add(Step("Cancelled", Stamp(reservation.CancelledAt.Value) + Reason(reservation.CancellationReason), "alert"));
            }
            else if (!reservation.RejectedAt.HasValue)
            {
                var borrowed = reservation.BorrowedAt.HasValue;
                var returned = reservation.ReturnedAt.HasValue;

                timeline.Add(Step("Borrowed", borrowed ? Stamp(reservation.BorrowedAt.Value) : "Not yet borrowed", borrowed ? "done" : "pending"));
                timeline.Add(Step("Due back", Stamp(reservation.ExpectedReturnDateTime),
                    row.DisplayStatus == ReservationStatuses.Overdue ? "alert" : (returned ? "done" : "pending")));
                timeline.Add(Step("Returned", returned ? Stamp(reservation.ReturnedAt.Value) : "Not yet returned", returned ? "done" : "pending"));
            }

            var items = reservation.ReservationItems.ToList();

            return new BorrowingRecordDetails
            {
                ReservationNumber = reservation.ReservationNumber,
                StatusLabel = row.DisplayStatus + (row.IsLate ? " · " + row.LateText + " late" : ""),
                StatusClass = row.StatusClass,
                BorrowerName = row.BorrowerName,
                Department = row.Department,
                ProgramYear = programYear,
                ContactNumber = profile?.User?.ContactNumber,
                Items = items.Select(item => new BorrowingRecordItem
                {
                    Name = item.Equipment?.EquipmentName,
                    Code = item.Equipment?.EquipmentCode,
                    Category = item.Equipment?.Category?.CategoryName,
                    SerialNumber = item.Equipment?.SerialNumber,
                    StorageLocation = item.Equipment?.StorageLocation
                }).ToList(),
                Timeline = timeline,
                Purpose = reservation.Purpose,
                ConditionBorrowed = JoinDistinct(items.Select(item => item.ConditionAtBorrowing), ", "),
                ConditionReturned = JoinDistinct(items.Select(item => item.ConditionAtReturn), ", "),
                ReceivedBy = reservation.ReceivedByUser == null ? null : FullName(reservation.ReceivedByUser),
                Remarks = JoinDistinct(items.Select(item => item.BorrowingRemarks), "; "),
                ActionNeeded = row.DisplayStatus == ReservationStatuses.Overdue
            };
        }

        // Print, Export

        public PrintableBorrowingReport GetPrintableReport(BorrowingReportFilter filter, BorrowingExportOptions options, string generatedBy)
        {
            filter = Normalize(filter);
            options ??= new BorrowingExportOptions();

            var now = DateTime.Now;
            var query = ApplyFilter(_reportRepository.GetBorrowingRecords(), filter, now);

            var rows = query
                .OrderByDescending(reservation => reservation.ReservationId)
                .Take(MaxExportRows)
                .ToList()
                .Select(reservation => ToRow(reservation, now))
                .ToList();

            return new PrintableBorrowingReport
            {
                GeneratedAt = now.ToString("MMM d, yyyy · h:mm tt"),
                GeneratedBy = string.IsNullOrWhiteSpace(generatedBy) ? "HiRAM User" : generatedBy,
                FileName = CleanFileName(options.FileName) ?? DefaultFileName(filter, now),
                Options = options,
                Labels = BuildLabels(filter, BorrowerOptions(), EquipmentOptions()),
                Summary = BuildSummary(query, now),
                Rows = rows,
                Pages = Paginate(rows, options)
            };
        }

        public byte[] BuildPdf(PrintableBorrowingReport report)
        {
            return BorrowingReportExport.Build(report);
        }

        public byte[] BuildCsv(PrintableBorrowingReport report)
        {
            return BorrowingReportCsv.Build(report);
        }

        public byte[] BuildExcel(PrintableBorrowingReport report)
        {
            return BorrowingReportExcel.Build(report);
        }


        // Filtering
        private static BorrowingReportFilter Normalize(BorrowingReportFilter filter)
        {
            filter ??= new BorrowingReportFilter();
            filter.Search = string.IsNullOrWhiteSpace(filter.Search) ? null : filter.Search.Trim();

            if (!StatusOptions.Contains(filter.Status))
                filter.Status = null;

            if (filter.From.HasValue && filter.To.HasValue && filter.From > filter.To)
            {
                var swap = filter.From;
                filter.From = filter.To;
                filter.To = swap;
            }

            return filter;
        }

        private static IQueryable<Reservation> ApplyFilter(IQueryable<Reservation> query, BorrowingReportFilter filter, DateTime now)
        {
            if (!string.IsNullOrEmpty(filter.Search))
            {
                var term = filter.Search;
                query = query.Where(reservation =>
                    reservation.ReservationNumber.Contains(term)
                    || (reservation.Purpose != null && reservation.Purpose.Contains(term))
                    || reservation.BorrowerUser.User.FirstName.Contains(term)
                    || reservation.BorrowerUser.User.LastName.Contains(term)
                    || (reservation.BorrowerUser.User.FirstName + " " + reservation.BorrowerUser.User.LastName).Contains(term)
                    || reservation.ReservationItems.Any(item =>
                        item.Equipment.EquipmentName.Contains(term) || item.Equipment.EquipmentCode.Contains(term)));
            }

            if (filter.BorrowerId.HasValue)
            {
                var borrowerId = filter.BorrowerId.Value;
                query = query.Where(reservation => reservation.BorrowerUserId == borrowerId);
            }

            if (filter.EquipmentId.HasValue)
            {
                var equipmentId = filter.EquipmentId.Value;
                query = query.Where(reservation => reservation.ReservationItems.Any(item => item.EquipmentId == equipmentId));
            }

            if (filter.From.HasValue)
            {
                var from = filter.From.Value.Date;
                query = query.Where(reservation => reservation.StartDateTime >= from);
            }

            if (filter.To.HasValue)
            {
                var to = filter.To.Value.Date.AddDays(1);
                query = query.Where(reservation => reservation.StartDateTime < to);
            }

            if (filter.Status == ReservationStatuses.Overdue)
            {
                query = query.Where(reservation =>
                    (reservation.Status == ReservationStatuses.Borrowed || reservation.Status == ReservationStatuses.Overdue)
                    && reservation.ExpectedReturnDateTime < now);
            }
            else if (filter.Status == ReservationStatuses.Borrowed)
            {
                query = query.Where(reservation =>
                    (reservation.Status == ReservationStatuses.Borrowed || reservation.Status == ReservationStatuses.Overdue)
                    && reservation.ExpectedReturnDateTime >= now);
            }
            else if (filter.Status != null)
            {
                var status = filter.Status;
                query = query.Where(reservation => reservation.Status == status);
            }

            if (filter.LateOnly)
                query = query.Where(LateExpression(now));

            return query;
        }

        // Late = returned after the due time, or still out past the due time.
        private static Expression<Func<Reservation, bool>> LateExpression(DateTime now)
        {
            return reservation =>
                (reservation.Status == ReservationStatuses.Returned && reservation.ReturnedAt != null && reservation.ReturnedAt > reservation.ExpectedReturnDateTime)
                || ((reservation.Status == ReservationStatuses.Borrowed || reservation.Status == ReservationStatuses.Overdue) && reservation.ExpectedReturnDateTime < now);
        }

        private static BorrowingReportSummary BuildSummary(IQueryable<Reservation> query, DateTime now)
        {
            var spans = query
                .Where(reservation => reservation.Status == ReservationStatuses.Returned && reservation.BorrowedAt != null && reservation.ReturnedAt != null)
                .Select(reservation => new { reservation.BorrowedAt, reservation.ReturnedAt })
                .ToList();

            var summary = new BorrowingReportSummary
            {
                TotalRecords = query.Count(),
                ReturnedCount = query.Count(reservation => reservation.Status == ReservationStatuses.Returned),
                CurrentlyBorrowed = query.Count(reservation => reservation.Status == ReservationStatuses.Borrowed || reservation.Status == ReservationStatuses.Overdue),
                LateReturns = query.Count(LateExpression(now))
            };

            if (spans.Count > 0)
            {
                var days = spans.Average(span => (span.ReturnedAt.Value - span.BorrowedAt.Value).TotalDays);
                summary.AverageDuration = days < 1
                    ? (days * 24).ToString("0.#") + " hrs"
                    : days.ToString("0.#") + (Math.Round(days, 1) == 1 ? " day" : " days");
            }

            return summary;
        }

        // Row mapping

        private static BorrowingReportRow ToRow(Reservation reservation, DateTime now)
        {
            var names = reservation.ReservationItems
                .Select(item => item.Equipment?.EquipmentName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .ToList();

            var isOut = reservation.Status == ReservationStatuses.Borrowed || reservation.Status == ReservationStatuses.Overdue;
            var overdue = isOut && reservation.ExpectedReturnDateTime < now;

            TimeSpan? late = null;
            if (overdue)
                late = now - reservation.ExpectedReturnDateTime;
            else if (reservation.Status == ReservationStatuses.Returned && reservation.ReturnedAt.HasValue
                     && reservation.ReturnedAt.Value > reservation.ExpectedReturnDateTime)
                late = reservation.ReturnedAt.Value - reservation.ExpectedReturnDateTime;

            return new BorrowingReportRow
            {
                ReservationId = reservation.ReservationId,
                ReservationNumber = reservation.ReservationNumber,
                Purpose = reservation.Purpose,
                BorrowerName = FullName(reservation.BorrowerUser?.User),
                Department = reservation.BorrowerUser?.Department?.DepartmentName,
                EquipmentSummary = names.Count == 0 ? "—" : names[0] + (names.Count > 1 ? " +" + (names.Count - 1) + " more" : ""),
                StartDateTime = reservation.StartDateTime,
                ExpectedReturnDateTime = reservation.ExpectedReturnDateTime,
                BorrowedAt = reservation.BorrowedAt,
                ReturnedAt = reservation.ReturnedAt,
                Status = reservation.Status,
                DisplayStatus = overdue ? ReservationStatuses.Overdue : (reservation.Status == ReservationStatuses.Overdue ? ReservationStatuses.Borrowed : reservation.Status),
                LateText = late.HasValue ? FormatLate(late.Value) : null
            };
        }

        private static string FormatLate(TimeSpan late)
        {
            if (late.TotalMinutes < 1)
                return null;

            if (late.TotalMinutes < 60)
                return (int)Math.Round(late.TotalMinutes) + " min";

            if (late.TotalHours < 24)
            {
                var hours = (int)Math.Floor(late.TotalHours);
                return hours + (hours == 1 ? " hr" : " hrs");
            }

            var days = (int)Math.Floor(late.TotalDays);
            return days + (days == 1 ? " day" : " days");
        }

        // Filter dropdowns and labels

        private List<ReportOption> BorrowerOptions()
        {
            return _reportRepository.GetBorrowers()
                .Select(borrower => new
                {
                    borrower.UserId,
                    borrower.User.FirstName,
                    borrower.User.MiddleName,
                    borrower.User.LastName,
                    borrower.User.Suffix
                })
                .ToList()
                .Select(borrower => new ReportOption
                {
                    Value = borrower.UserId.ToString(),
                    Text = FullName(borrower.FirstName, borrower.MiddleName, borrower.LastName, borrower.Suffix)
                })
                .OrderBy(option => option.Text)
                .ToList();
        }

        private List<ReportOption> EquipmentOptions()
        {
            return _reportRepository.GetEquipments()
                .OrderBy(equipment => equipment.EquipmentName)
                .Select(equipment => new { equipment.EquipmentId, equipment.EquipmentName })
                .ToList()
                .Select(equipment => new ReportOption { Value = equipment.EquipmentId.ToString(), Text = equipment.EquipmentName })
                .ToList();
        }

        private static BorrowingFilterLabels BuildLabels(BorrowingReportFilter filter, List<ReportOption> borrowers, List<ReportOption> equipments)
        {
            return new BorrowingFilterLabels
            {
                Period = PeriodLabel(filter.From, filter.To),
                Borrower = filter.BorrowerId.HasValue
                    ? borrowers.FirstOrDefault(option => option.Value == filter.BorrowerId.Value.ToString())?.Text
                    : null,
                Equipment = filter.EquipmentId.HasValue
                    ? equipments.FirstOrDefault(option => option.Value == filter.EquipmentId.Value.ToString())?.Text
                    : null,
                Status = filter.Status,
                Search = filter.Search,
                LateOnly = filter.LateOnly
            };
        }

        private static string PeriodLabel(DateTime? from, DateTime? to)
        {
            if (from.HasValue && to.HasValue)
            {
                return from.Value.Year == to.Value.Year
                    ? from.Value.ToString("MMM d") + " – " + to.Value.ToString("MMM d, yyyy")
                    : from.Value.ToString("MMM d, yyyy") + " – " + to.Value.ToString("MMM d, yyyy");
            }

            if (from.HasValue) return "From " + from.Value.ToString("MMM d, yyyy");
            if (to.HasValue) return "Until " + to.Value.ToString("MMM d, yyyy");
            return "All dates";
        }

        // Export helpers

        private static string DefaultFileName(BorrowingReportFilter filter, DateTime now)
        {
            return filter.From.HasValue && filter.To.HasValue
                ? "HiRAM-Borrowing-Report-" + filter.From.Value.ToString("yyyy-MM-dd") + "_to_" + filter.To.Value.ToString("yyyy-MM-dd")
                : "HiRAM-Borrowing-Report-" + now.ToString("yyyy-MM-dd");
        }

        private static string CleanFileName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return null;

            name = name.Trim();

            foreach (var extension in new[] { ".pdf", ".xlsx", ".csv" })
            {
                if (name.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
                    name = name.Substring(0, name.Length - extension.Length);
            }

            name = string.Concat(name.Where(character => !Path.GetInvalidFileNameChars().Contains(character)));
            if (name.Length > 80)
                name = name.Substring(0, 80);

            return string.IsNullOrWhiteSpace(name) ? null : name;
        }

        // Rows that fit on each printed sheet (the first sheet also holds the filters and summary).
        private static List<List<BorrowingReportRow>> Paginate(List<BorrowingReportRow> rows, BorrowingExportOptions options)
        {
            var firstSheet = options.Landscape ? 10 : 14;
            var nextSheets = options.Landscape ? 16 : 24;

            var pages = new List<List<BorrowingReportRow>>();
            var index = 0;
            var size = firstSheet;

            while (index < rows.Count)
            {
                pages.Add(rows.Skip(index).Take(size).ToList());
                index += size;
                size = nextSheets;
            }

            if (pages.Count == 0)
                pages.Add(new List<BorrowingReportRow>());

            return pages;
        }

        // Small helpers

        private static BorrowingTimelineStep Step(string title, string when, string state)
        {
            return new BorrowingTimelineStep { Title = title, When = when, State = state };
        }

        private static string Stamp(DateTime value) => value.ToString("MMM d, yyyy · h:mm tt");

        private static string Reason(string reason) => string.IsNullOrWhiteSpace(reason) ? "" : " · " + reason;

        private static string JoinDistinct(IEnumerable<string> values, string separator)
        {
            var list = values.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct().ToList();
            return list.Count == 0 ? null : string.Join(separator, list);
        }

        private static string FullName(User user)
        {
            return user == null ? "—" : FullName(user.FirstName, user.MiddleName, user.LastName, user.Suffix);
        }

        private static string FullName(string first, string middle, string last, string suffix)
        {
            return string.Join(" ", new[] { first, middle, last, suffix }.Where(part => !string.IsNullOrWhiteSpace(part)));
        }
    }
}