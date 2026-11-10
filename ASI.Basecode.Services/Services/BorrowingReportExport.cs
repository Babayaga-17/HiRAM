using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.ServiceModels;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ASI.Basecode.Services.Services
{
    // Downloadable PDF (Export Report).
    public static class BorrowingReportExport
    {
        private const string Ink = "#18223b";
        private const string Muted = "#728097";
        private const string Line = "#e6eaf1";
        private const string HeaderBackground = "#101d36";
        private const string LateColor = "#b3262e";

        static BorrowingReportExport()
        {
            QuestPDF.Settings.License = LicenseType.Community;
        }

        public static byte[] Build(PrintableBorrowingReport report)
        {
            var size = report.Options.Landscape ? PageSizes.A4.Landscape() : PageSizes.A4;

            return Document.Create(document =>
            {
                document.Page(page =>
                {
                    page.Size(size);
                    page.Margin(28);
                    page.DefaultTextStyle(style => style.FontSize(9).FontColor(Ink));

                    page.Header().Element(container => ComposeHeader(container, report));
                    page.Content().PaddingTop(10).Element(container => ComposeContent(container, report));
                    page.Footer().Element(ComposeFooter);
                });
            }).GeneratePdf();
        }

        private static void ComposeHeader(IContainer container, PrintableBorrowingReport report)
        {
            container.PaddingBottom(8).BorderBottom(1).BorderColor(Ink).Row(row =>
            {
                row.RelativeItem().Column(column =>
                {
                    column.Item().Text("HiRAM").FontSize(16).Bold();
                    column.Item().Text("SCHOOL RESOURCE MANAGEMENT").FontSize(7).FontColor(Muted).Bold();
                });

                row.RelativeItem().Column(column =>
                {
                    column.Item().AlignRight().Text(report.Title).FontSize(15).Bold();
                    column.Item().AlignRight()
                        .Text("Generated " + report.GeneratedAt + " · by " + report.GeneratedBy)
                        .FontSize(8).FontColor(Muted);
                });
            });
        }

        private static void ComposeContent(IContainer container, PrintableBorrowingReport report)
        {
            container.Column(column =>
            {
                column.Spacing(10);

                if (report.Options.ShowFilters)
                {
                    column.Item().DefaultTextStyle(style => style.FontSize(8).FontColor(Muted)).Text(text =>
                    {
                        AddFilter(text, "Period", report.Labels.Period);
                        AddFilter(text, "Borrower", report.Labels.Borrower ?? "All");
                        AddFilter(text, "Equipment", report.Labels.Equipment ?? "All");
                        AddFilter(text, "Status", report.Labels.Status ?? "All");

                        if (report.Labels.LateOnly)
                            AddFilter(text, "Late returns only", "Yes");

                        if (!string.IsNullOrWhiteSpace(report.Labels.Search))
                            AddFilter(text, "Search", report.Labels.Search);
                    });
                }

                if (report.Options.IncludeSummary)
                {
                    column.Item().Row(row =>
                    {
                        row.Spacing(10);
                        SummaryBox(row.RelativeItem(), report.Summary.TotalRecords.ToString(), "Reservations");
                        SummaryBox(row.RelativeItem(), report.Summary.ReturnedCount.ToString(), "Completed");
                        SummaryBox(row.RelativeItem(), report.Summary.LateReturns.ToString(), "Late returns");
                        SummaryBox(row.RelativeItem(), report.Summary.AverageDuration, "Avg. borrowing duration");
                    });
                }

                column.Item().Element(table => ComposeTable(table, report));
            });
        }

        private static void AddFilter(TextDescriptor text, string label, string value)
        {
            text.Span(label + ": ").Bold();
            text.Span(value + "      ");
        }

        private static void SummaryBox(IContainer container, string value, string label)
        {
            container.Border(1).BorderColor(Line).Padding(8).Column(column =>
            {
                column.Item().Text(value).FontSize(16).Bold();
                column.Item().Text(label).FontSize(8).FontColor(Muted);
            });
        }

        private static void ComposeTable(IContainer container, PrintableBorrowingReport report)
        {
            container.Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(88);   // Reservation
                    columns.RelativeColumn(1.4f); // Borrower
                    columns.RelativeColumn(1.6f); // Equipment
                    columns.RelativeColumn(1.2f); // Borrowed
                    columns.RelativeColumn(1.2f); // Due
                    columns.RelativeColumn(1.2f); // Returned
                    columns.ConstantColumn(58);   // Status
                    columns.ConstantColumn(50);   // Late by
                });

                table.Header(header =>
                {
                    foreach (var title in new[] { "RESERVATION", "BORROWER", "EQUIPMENT", "BORROWED", "DUE", "RETURNED", "STATUS", "LATE BY" })
                        header.Cell().Background(HeaderBackground).Padding(5)
                            .Text(title).FontSize(8).Bold().FontColor(Colors.White);
                });

                foreach (var row in report.Rows)
                {
                    var background = row.DisplayStatus == ReservationStatuses.Overdue ? "#fff4f4" : "#ffffff";

                    void Cell(string value, bool bold = false, string color = null)
                    {
                        var text = table.Cell().Background(background).BorderBottom(0.5f).BorderColor(Line)
                            .PaddingVertical(5).PaddingHorizontal(6).Text(value).FontSize(8);

                        if (bold) text.Bold();
                        if (color != null) text.FontColor(color);
                    }

                    Cell(row.ReservationNumber, bold: true);
                    Cell(row.BorrowerName);
                    Cell(row.EquipmentSummary);
                    Cell(Format(row.BorrowedAt));
                    Cell(row.ExpectedReturnDateTime.ToString("MMM d, h:mm tt"));
                    Cell(Format(row.ReturnedAt));
                    Cell(row.DisplayStatus, bold: true, color: row.DisplayStatus == ReservationStatuses.Overdue ? LateColor : null);
                    Cell(row.LateText ?? "—", bold: row.IsLate, color: row.IsLate ? LateColor : null);
                }
            });
        }

        private static void ComposeFooter(IContainer container)
        {
            container.BorderTop(1).BorderColor(Line).PaddingTop(6)
                .DefaultTextStyle(style => style.FontSize(7).FontColor(Muted))
                .Row(row =>
                {
                    row.RelativeItem().Text("HiRAM · School Resource Management · Confidential");
                    row.RelativeItem().AlignRight().Text(text =>
                    {
                        text.Span("Page ");
                        text.CurrentPageNumber();
                        text.Span(" of ");
                        text.TotalPages();
                    });
                });
        }

        private static string Format(System.DateTime? value)
        {
            return value.HasValue ? value.Value.ToString("MMM d, h:mm tt") : "—";
        }
    }
}


namespace ASI.Basecode.Services.Services
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Text;

    // Downloadable CSV (Export Report). Records only, so other tools can read it.
    public static class BorrowingReportCsv
    {
        private static readonly string[] Headers =
        {
            "Reservation No.", "Borrower", "Department", "Equipment", "Purpose",
            "Reserved from", "Due", "Borrowed", "Returned", "Status", "Late by"
        };

        public static byte[] Build(PrintableBorrowingReport report)
        {
            var builder = new StringBuilder();
            builder.Append(string.Join(",", Headers.Select(Escape))).Append("\r\n");

            foreach (var row in report.Rows)
            {
                var values = new[]
                {
                    row.ReservationNumber,
                    row.BorrowerName,
                    row.Department,
                    row.EquipmentSummary,
                    row.Purpose,
                    Stamp(row.StartDateTime),
                    Stamp(row.ExpectedReturnDateTime),
                    Stamp(row.BorrowedAt),
                    Stamp(row.ReturnedAt),
                    row.DisplayStatus,
                    row.LateText
                };

                builder.Append(string.Join(",", values.Select(Escape))).Append("\r\n");
            }

            // UTF-8 with a byte order mark, so Excel shows names and symbols correctly.
            var encoding = new UTF8Encoding(true);
            return encoding.GetPreamble().Concat(encoding.GetBytes(builder.ToString())).ToArray();
        }

        private static string Stamp(DateTime? value)
        {
            return value.HasValue ? value.Value.ToString("yyyy-MM-dd HH:mm") : string.Empty;
        }

        private static string Escape(string value)
        {
            value ??= string.Empty;

            // A cell that starts with = + - @ can run as a formula when the file is opened in Excel.
            if (value.Length > 0 && "=+-@\t\r".IndexOf(value[0]) >= 0)
                value = "'" + value;

            if (value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0)
                value = "\"" + value.Replace("\"", "\"\"") + "\"";

            return value;
        }
    }
}

namespace ASI.Basecode.Services.Services
{
    using System;
    using System.IO;
    using ClosedXML.Excel;

    // Downloadable Excel workbook (Export Report).
    public static class BorrowingReportExcel
    {
        private const string HeaderColor = "#101d36";
        private const string OverdueFill = "#fff4f4";
        private const string LateTextColor = "#b3262e";
        private const string MutedColor = "#728097";
        private const string DateFormat = "mmm d, yyyy h:mm AM/PM";

        private static readonly string[] Headers =
        {
            "Reservation No.", "Borrower", "Department", "Equipment", "Purpose",
            "Reserved from", "Due", "Borrowed", "Returned", "Status", "Late by"
        };

        private static readonly double[] Widths = { 18, 24, 22, 38, 34, 22, 22, 22, 22, 12, 14 };

        public static byte[] Build(PrintableBorrowingReport report)
        {
            using var workbook = new XLWorkbook();

            AddRecordsSheet(workbook, report);

            if (report.Options.IncludeSummary || report.Options.ShowFilters)
                AddSummarySheet(workbook, report);

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }

        private static void AddRecordsSheet(XLWorkbook workbook, PrintableBorrowingReport report)
        {
            var sheet = workbook.Worksheets.Add("Borrowing History");

            for (var column = 0; column < Headers.Length; column++)
            {
                var cell = sheet.Cell(1, column + 1);
                cell.Value = Headers[column];
                cell.Style.Font.Bold = true;
                cell.Style.Font.FontColor = XLColor.White;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml(HeaderColor);
                cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                sheet.Column(column + 1).Width = Widths[column];
            }

            sheet.Row(1).Height = 24;

            var rowNumber = 2;

            foreach (var row in report.Rows)
            {
                sheet.Cell(rowNumber, 1).Value = row.ReservationNumber ?? string.Empty;
                sheet.Cell(rowNumber, 2).Value = row.BorrowerName ?? string.Empty;
                sheet.Cell(rowNumber, 3).Value = row.Department ?? string.Empty;
                sheet.Cell(rowNumber, 4).Value = row.EquipmentSummary ?? string.Empty;
                sheet.Cell(rowNumber, 5).Value = row.Purpose ?? string.Empty;
                SetDate(sheet.Cell(rowNumber, 6), row.StartDateTime);
                SetDate(sheet.Cell(rowNumber, 7), row.ExpectedReturnDateTime);
                SetDate(sheet.Cell(rowNumber, 8), row.BorrowedAt);
                SetDate(sheet.Cell(rowNumber, 9), row.ReturnedAt);
                sheet.Cell(rowNumber, 10).Value = row.DisplayStatus ?? string.Empty;
                sheet.Cell(rowNumber, 11).Value = row.LateText ?? string.Empty;

                if (row.DisplayStatus == ReservationStatuses.Overdue)
                {
                    sheet.Range(rowNumber, 1, rowNumber, Headers.Length).Style.Fill.BackgroundColor = XLColor.FromHtml(OverdueFill);
                    sheet.Cell(rowNumber, 10).Style.Font.Bold = true;
                    sheet.Cell(rowNumber, 10).Style.Font.FontColor = XLColor.FromHtml(LateTextColor);
                }

                if (row.IsLate)
                {
                    sheet.Cell(rowNumber, 11).Style.Font.Bold = true;
                    sheet.Cell(rowNumber, 11).Style.Font.FontColor = XLColor.FromHtml(LateTextColor);
                }

                rowNumber++;
            }

            var lastRow = Math.Max(rowNumber - 1, 1);

            if (report.Rows.Count > 0)
            {
                var body = sheet.Range(2, 1, lastRow, Headers.Length);
                body.Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
                sheet.Range(2, 4, lastRow, 5).Style.Alignment.WrapText = true;
            }

            sheet.Range(1, 1, lastRow, Headers.Length).SetAutoFilter();
            sheet.SheetView.FreezeRows(1);
        }

        private static void AddSummarySheet(XLWorkbook workbook, PrintableBorrowingReport report)
        {
            var sheet = workbook.Worksheets.Add("Summary");
            sheet.Column(1).Width = 26;
            sheet.Column(2).Width = 50;

            var row = 1;

            sheet.Cell(row, 1).Value = report.Title;
            sheet.Cell(row, 1).Style.Font.Bold = true;
            sheet.Cell(row, 1).Style.Font.FontSize = 16;
            row++;

            sheet.Cell(row, 1).Value = "Generated " + report.GeneratedAt + " · by " + report.GeneratedBy;
            sheet.Cell(row, 1).Style.Font.FontColor = XLColor.FromHtml(MutedColor);
            row += 2;

            if (report.Options.ShowFilters)
            {
                row = Section(sheet, row, "Applied filters");
                row = Line(sheet, row, "Period", report.Labels.Period);
                row = Line(sheet, row, "Borrower", report.Labels.Borrower ?? "All");
                row = Line(sheet, row, "Equipment", report.Labels.Equipment ?? "All");
                row = Line(sheet, row, "Status", report.Labels.Status ?? "All");

                if (report.Labels.LateOnly)
                    row = Line(sheet, row, "Late returns only", "Yes");

                if (!string.IsNullOrWhiteSpace(report.Labels.Search))
                    row = Line(sheet, row, "Search", report.Labels.Search);

                row++;
            }

            if (report.Options.IncludeSummary)
            {
                row = Section(sheet, row, "Summary");
                row = Line(sheet, row, "Reservations", report.Summary.TotalRecords);
                row = Line(sheet, row, "Completed", report.Summary.ReturnedCount);
                row = Line(sheet, row, "Currently borrowed", report.Summary.CurrentlyBorrowed);
                row = Line(sheet, row, "Late returns", report.Summary.LateReturns);
                row = Line(sheet, row, "Avg. borrowing duration", report.Summary.AverageDuration);
            }
        }

        private static int Section(IXLWorksheet sheet, int row, string title)
        {
            sheet.Cell(row, 1).Value = title;
            sheet.Cell(row, 1).Style.Font.Bold = true;
            sheet.Range(row, 1, row, 2).Style.Fill.BackgroundColor = XLColor.FromHtml("#eef1f6");
            return row + 1;
        }

        private static int Line(IXLWorksheet sheet, int row, string label, string value)
        {
            sheet.Cell(row, 1).Value = label;
            sheet.Cell(row, 2).Value = value ?? string.Empty;
            return row + 1;
        }

        private static int Line(IXLWorksheet sheet, int row, string label, int value)
        {
            sheet.Cell(row, 1).Value = label;
            sheet.Cell(row, 2).Value = value;
            sheet.Cell(row, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
            return row + 1;
        }

        // Real date cells, so the columns can be sorted and filtered in Excel.
        private static void SetDate(IXLCell cell, DateTime? value)
        {
            if (!value.HasValue)
                return;

            cell.Value = value.Value;
            cell.Style.DateFormat.Format = DateFormat;
        }
    }
}