using ASI.Basecode.Resources.Constants;
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
            .Where(item => item.BorrowerUserId == borrowerUserId && item.Status == ReservationStatuses.Pending)
            .OrderByDescending(item => item.RequestedAt)
            .ThenByDescending(item => item.ReservationId)
            .ToList();
    }

    public List<Reservation> GetBorrowerReservations(int borrowerUserId)
    {
        if (borrowerUserId <= 0) throw new ArgumentOutOfRangeException(nameof(borrowerUserId));
        return ReservationDetailsQuery()
            .Where(item => item.BorrowerUserId == borrowerUserId)
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

    public void RemoveReservationItems(IEnumerable<ReservationItem> items)
    {
        GetDbSet<ReservationItem>().RemoveRange(items);
    }

    private IQueryable<Reservation> ReservationDetailsQuery()
    {
        return GetDbSet<Reservation>().AsNoTracking()
            .Include(item => item.ReservationItems).ThenInclude(item => item.Equipment);
    }
}
