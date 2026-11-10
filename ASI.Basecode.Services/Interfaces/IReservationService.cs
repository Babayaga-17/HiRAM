// ASI.Basecode.Services/Interfaces/IReservationService.cs
using ASI.Basecode.Services.ServiceModels;
using ASI.Basecode.Services.ServiceModels.Reservation;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ASI.Basecode.Services.Interfaces
{
    public interface IReservationService
    {
        (string ReservationNumber, string Error) Submit(ReservationFormModel form, int borrowerUserId);
        public string ExecuteChange(Func<string> operation, string concurrencyMessage);


        Task<MyReservationPageViewModel> GetBorrowerReservationsAsync(int borrowerUserId, string status, int page, int pageSize);
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
