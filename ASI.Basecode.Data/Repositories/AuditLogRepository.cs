using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using Basecode.Data.Repositories;

namespace ASI.Basecode.Data.Repositories
{
    public class AuditLogRepository : BaseRepository, IAuditLogRepository
    {
        public AuditLogRepository(IUnitOfWork unitOfWork) : base(unitOfWork)
        {
        }

        public void AddAuditLog(AuditLog auditLog)
        {
            GetDbSet<AuditLog>().Add(auditLog);
            UnitOfWork.SaveChanges();
        }
    }
}
