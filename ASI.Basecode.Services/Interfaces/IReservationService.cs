// ASI.Basecode.Services/Interfaces/IReservationService.cs
using ASI.Basecode.Services.ServiceModels;
using System.Collections.Generic;

namespace ASI.Basecode.Services.Interfaces
{
    public interface IReservationService
    {
        List<PendingReservationListItem> GetPendingReservations();
        PendingReservationDetailsViewModel GetPendingReservationById(int reservationId);
        int CountPendingReservations();

        ReservationListViewModel GetReservations(string status);
        ReservationDetailsViewModel GetReservationDetails(int reservationId);
        (bool ok, string message) Approve(int reservationId, int? actorUserId, string actorName, string remarks);
        (bool ok, string message) Reject(int reservationId, int? actorUserId, string actorName, string reason);
        (bool ok, string message) Release(ReleaseReservationRequest request, string actorName);
        (bool ok, string message) Return(ReturnReservationRequest request, int? actorUserId, string actorName);
    }
}
