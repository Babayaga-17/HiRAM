using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using ASI.Basecode.WebApp.Mvc;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;

namespace ASI.Basecode.WebApp.Controllers;

[Authorize(Roles = "Custodian, Admin")]
public class ReportController : ControllerBase<ReportController>
{
    private const int PageSize = 8;

    private readonly IReportService _reportService;

    public ReportController(
        IHttpContextAccessor httpContextAccessor,
        ILoggerFactory loggerFactory,
        IConfiguration configuration,
        IReportService reportService,
        IMapper mapper = null) : base(httpContextAccessor, loggerFactory, configuration, mapper)
    {
        _reportService = reportService;
    }

    // The filters come from the address (GET).
    [HttpGet]
    public IActionResult Index([FromQuery] BorrowingReportFilter filter, int page = 1)
    {
        try
        {
            return View(_reportService.GetBorrowingReport(filter, page, PageSize));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unable to load the borrowing history report.");
            TempData["ReportError"] = "Unable to load the report. Please try again.";
            return View(new BorrowingReportViewModel());
        }
    }

    // The side panel asks for one record.
    [HttpGet]
    public IActionResult Details(int id)
    {
        try
        {
            var details = _reportService.GetBorrowingRecordDetails(id);
            if (details == null)
                return NotFound(new { message = "That record was not found." });

            return Json(new
            {
                reservationNumber = details.ReservationNumber,
                statusLabel = details.StatusLabel,
                statusClass = details.StatusClass,
                borrower = new
                {
                    name = details.BorrowerName,
                    department = details.Department,
                    programYear = details.ProgramYear,
                    contact = details.ContactNumber
                },
                items = details.Items.Select(item => new
                {
                    name = item.Name,
                    code = item.Code,
                    category = item.Category,
                    serial = item.SerialNumber,
                    location = item.StorageLocation
                }),
                timeline = details.Timeline.Select(step => new { title = step.Title, when = step.When, state = step.State }),
                purpose = details.Purpose,
                conditionBorrowed = details.ConditionBorrowed,
                conditionReturned = details.ConditionReturned,
                receivedBy = details.ReceivedBy,
                remarks = details.Remarks,
                actionNeeded = details.ActionNeeded
            });
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unable to load borrowing record {ReservationId}.", id);
            return StatusCode(500, new { message = "Unable to load this record." });
        }
    }

    // Print preview (the page has a Print button).
    [HttpGet]
    public IActionResult Preview([FromQuery] BorrowingReportFilter filter, [FromQuery] BorrowingExportOptions options)
    {
        try
        {
            return View(_reportService.GetPrintableReport(filter, options, User.Identity?.Name));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unable to build the report preview.");
            TempData["ReportError"] = "Unable to open the print preview. Please try again.";
            return RedirectToAction(nameof(Index));
        }
    }

    // Download the PDF.
    [HttpGet]
    public IActionResult ExportPdf([FromQuery] BorrowingReportFilter filter, [FromQuery] BorrowingExportOptions options)
    {
        try
        {
            var report = _reportService.GetPrintableReport(filter, options, User.Identity?.Name);
            var bytes = _reportService.BuildPdf(report);

            return File(bytes, "application/pdf", report.FileName + ".pdf");
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unable to export the borrowing history report.");
            TempData["ReportError"] = "Unable to export the report. Please try again.";
            return RedirectToAction(nameof(Index));
        }
    }

    // Download the Excel workbook.
    [HttpGet]
    public IActionResult ExportExcel([FromQuery] BorrowingReportFilter filter, [FromQuery] BorrowingExportOptions options)
    {
        try
        {
            var report = _reportService.GetPrintableReport(filter, options, User.Identity?.Name);
            var bytes = _reportService.BuildExcel(report);

            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", report.FileName + ".xlsx");
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unable to export the borrowing history report to Excel.");
            TempData["ReportError"] = "Unable to export the report. Please try again.";
            return RedirectToAction(nameof(Index));
        }
    }

    // Download the CSV file.
    [HttpGet]
    public IActionResult ExportCsv([FromQuery] BorrowingReportFilter filter, [FromQuery] BorrowingExportOptions options)
    {
        try
        {
            var report = _reportService.GetPrintableReport(filter, options, User.Identity?.Name);
            var bytes = _reportService.BuildCsv(report);

            return File(bytes, "text/csv", report.FileName + ".csv");
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unable to export the borrowing history report to CSV.");
            TempData["ReportError"] = "Unable to export the report. Please try again.";
            return RedirectToAction(nameof(Index));
        }
    }
}