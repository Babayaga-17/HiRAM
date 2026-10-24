using ASI.Basecode.Data.Models;

namespace ASI.Basecode.Data.Interfaces
{
    public interface IAuditLogRepository
    {
        void AddAuditLog(AuditLog auditLog);
    }
}
