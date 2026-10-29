using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using ASI.Basecode.Data.Exceptions;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using Basecode.Data.Repositories;
using Microsoft.EntityFrameworkCore;

namespace ASI.Basecode.Data.Repositories;

public class EquipmentReservationRepository : BaseRepository, IEquipmentReservationRepository
{
    public EquipmentReservationRepository(IUnitOfWork unitOfWork) : base(unitOfWork)
    {
    }

    public List<Reservation> GetPendingReservations(int borrowerUserId)
    {
        return ReservationDetailsQuery()
            .Where(item => item.BorrowerUserId == borrowerUserId && item.Status == "Pending")
            .OrderByDescending(item => item.RequestedAt)
            .ThenByDescending(item => item.ReservationId)
            .ToList();
    }

    public Reservation GetReservationDetails(int reservationId, int borrowerUserId)
    {
        return ReservationDetailsQuery().SingleOrDefault(item =>
            item.ReservationId == reservationId && item.BorrowerUserId == borrowerUserId);
    }

    public Reservation GetReservationForUpdate(int reservationId, int borrowerUserId)
    {
        return GetDbSet<Reservation>().AsTracking()
            .Include(item => item.ReservationItems)
            .SingleOrDefault(item => item.ReservationId == reservationId
                && item.BorrowerUserId == borrowerUserId);
    }

    public BorrowerProfile GetBorrowerProfile(int borrowerUserId)
    {
        return GetDbSet<BorrowerProfile>().AsNoTracking()
            .Include(item => item.User).ThenInclude(user => user.Account).ThenInclude(account => account.Role)
            .SingleOrDefault(item => item.UserId == borrowerUserId);
    }

    public Equipment GetEquipment(int equipmentId)
    {
        return GetDbSet<Equipment>().AsNoTracking().Include(item => item.Category)
            .SingleOrDefault(item => item.EquipmentId == equipmentId);
    }

    public void AddReservation(Reservation reservation)
    {
        GetDbSet<Reservation>().Add(reservation);
    }

    public void RemoveReservationItems(IEnumerable<ReservationItem> items)
    {
        GetDbSet<ReservationItem>().RemoveRange(items);
    }

    public string ExecuteInTransaction(Func<string> operation)
    {
        using var transaction = Context.Database.BeginTransaction(IsolationLevel.Serializable);
        try
        {
            var error = operation();
            if (error != null)
                return error; // Disposing the uncommitted transaction rolls it back.

            UnitOfWork.SaveChanges();
            transaction.Commit();
            return null;
        }
        catch (DbUpdateConcurrencyException exception)
        {
            // Keep EF-specific exceptions inside Data. Disposal rolls back the whole write.
            throw new ReservationConcurrencyException(exception);
        }
    }

    private IQueryable<Reservation> ReservationDetailsQuery()
    {
        return GetDbSet<Reservation>().AsNoTracking()
            .Include(item => item.ReservationItems).ThenInclude(item => item.Equipment);
    }
}
