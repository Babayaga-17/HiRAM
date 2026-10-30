using System;
using System.Collections.Generic;
using ASI.Basecode.Data.Models;

namespace ASI.Basecode.Data.Interfaces;

public interface IEquipmentReservationRepository
{
    List<Reservation> GetPendingReservations(int borrowerUserId);
    Reservation GetReservationDetails(int reservationId, int borrowerUserId);
    Reservation GetReservationForUpdate(int reservationId, int borrowerUserId);
    BorrowerProfile GetBorrowerProfile(int borrowerUserId);
    Equipment GetEquipment(int equipmentId);
    void AddReservation(Reservation reservation);
    void RemoveReservationItems(IEnumerable<ReservationItem> items);

    // The callback returns a validation error, or null to save and commit.
    // Availability reads must run inside this transaction along with the write.
    string ExecuteInTransaction(Func<string> operation);
}
