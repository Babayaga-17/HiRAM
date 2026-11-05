using System;
using System.Linq;
using ASI.Basecode.Data.Models;
namespace ASI.Basecode.Data.Interfaces;
public interface IMaintenanceRepository
{
    IQueryable<MaintenanceRecord> Records();
    MaintenanceRecord Find(int id);
    Equipment FindEquipment(int id);
    string[] ReservationConflicts(int equipmentId, DateTime start);
    bool HasReservationConflict(int equipmentId, DateTime start);
    bool HasMaintenanceConflict(int equipmentId, int? excludeId = null);
    string[] AdminEmails();
    string ReporterEmail(int? userId);
    void Add(MaintenanceRecord record);
    void Save();
    void InTransaction(Action action);
}

