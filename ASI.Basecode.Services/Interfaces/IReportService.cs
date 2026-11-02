using ASI.Basecode.Services.ServiceModels;

namespace ASI.Basecode.Services.Interfaces
{
    public interface IReportService
    {
        BorrowingReportViewModel GetBorrowingReport(BorrowingReportFilter filter, int page, int pageSize);
        BorrowingRecordDetails GetBorrowingRecordDetails(int reservationId);
        PrintableBorrowingReport GetPrintableReport(BorrowingReportFilter filter, BorrowingExportOptions options, string generatedBy);
        byte[] BuildPdf(PrintableBorrowingReport report);
        byte[] BuildCsv(PrintableBorrowingReport report);
        byte[] BuildExcel(PrintableBorrowingReport report);
    }
}