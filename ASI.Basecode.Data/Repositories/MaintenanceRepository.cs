using System;
using System.Data;
using System.Linq;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
using Basecode.Data.Repositories;
using Microsoft.EntityFrameworkCore;
namespace ASI.Basecode.Data.Repositories;
public class MaintenanceRepository : BaseRepository, IMaintenanceRepository
{
    public MaintenanceRepository(IUnitOfWork unitOfWork) : base(unitOfWork) { }
    public IQueryable<MaintenanceRecord> Records() => GetDbSet<MaintenanceRecord>().AsNoTracking().Include(r => r.Equipment);
    public MaintenanceRecord Find(int id) => GetDbSet<MaintenanceRecord>().Include(r => r.Equipment).SingleOrDefault(r => r.MaintenanceId == id);
    public Equipment FindEquipment(int id) => GetDbSet<Equipment>().SingleOrDefault(e => e.EquipmentId == id);
    public string[] ReservationConflicts(int equipmentId, DateTime start) => GetDbSet<ReservationItem>()
        .Where(i => i.EquipmentId == equipmentId && !i.IsReturned && ReservationStatuses.Blocking.Contains(i.Reservation.Status) && i.Reservation.ExpectedReturnDateTime > start)
        .Select(i => new { i.Reservation.ReservationNumber, i.Reservation.StartDateTime, i.Reservation.Status })
        .OrderBy(i => i.StartDateTime).ToList()
        .Select(i => $"{i.ReservationNumber} ({i.StartDateTime:g}, {i.Status})").ToArray();    public bool HasReservationConflict(int equipmentId, DateTime start) => GetDbSet<ReservationItem>().Any(i => i.EquipmentId == equipmentId && !i.IsReturned && ReservationStatuses.Blocking.Contains(i.Reservation.Status) && i.Reservation.ExpectedReturnDateTime > start);
    public bool HasMaintenanceConflict(int equipmentId, int? excludeId = null) => GetDbSet<MaintenanceRecord>().Any(r => r.EquipmentId == equipmentId && (!excludeId.HasValue || r.MaintenanceId != excludeId.Value) && (r.Status == MaintenanceStatuses.Scheduled || r.Status == MaintenanceStatuses.InProgress));
    public string[] AdminEmails() => GetDbSet<Account>().Where(a => a.Role.RoleName == "Admin" && a.AccountStatus == "Active").Select(a => a.Email).Distinct().ToArray();
    public string ReporterEmail(int? userId) => GetDbSet<Account>().Where(a => a.UserId == userId).Select(a => a.Email).FirstOrDefault();
    public void Add(MaintenanceRecord record) => GetDbSet<MaintenanceRecord>().Add(record);
    public void Save() => UnitOfWork.SaveChanges();
    public void InTransaction(Action action)
    {
        using var transaction = Context.Database.BeginTransaction(IsolationLevel.Serializable);
        action();
        UnitOfWork.SaveChanges();
        transaction.Commit();
    }
}

