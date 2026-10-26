using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using System.Collections.Generic;
using System.Linq;

namespace ASI.Basecode.Services.Services
{
    public class ReservationService : IReservationService
    {
        private readonly IReservationRepository _reservationRepository;

        public ReservationService(IReservationRepository reservationRepository)
        {
            _reservationRepository = reservationRepository;
        }

        public List<PendingReservationListItem> GetPendingReservations()
        {
            return _reservationRepository.GetPendingReservations()
                .ToList()
                .Select(ToListItem)
                .ToList();
        }

        public PendingReservationDetailsViewModel GetPendingReservationById(int reservationId)
        {
            var reservation = _reservationRepository.GetPendingReservationById(reservationId);
            if (reservation == null)
                return null;

            var listItem = ToListItem(reservation);
            var borrower = reservation.BorrowerUser;
            var user = borrower?.User;

            return new PendingReservationDetailsViewModel
            {
                ReservationId = listItem.ReservationId,
                ReservationNumber = listItem.ReservationNumber,
                BorrowerName = listItem.BorrowerName,
                BorrowerUserCode = listItem.BorrowerUserCode,
                DepartmentName = listItem.DepartmentName,
                EligibilityStatus = listItem.EligibilityStatus,
                Purpose = listItem.Purpose,
                RequestedAt = listItem.RequestedAt,
                StartDateTime = listItem.StartDateTime,
                ExpectedReturnDateTime = listItem.ExpectedReturnDateTime,
                ItemCount = listItem.ItemCount,
                EquipmentSummary = listItem.EquipmentSummary,
                Status = listItem.Status,
                ContactNumber = user?.ContactNumber,
                Program = borrower?.Program,
                YearLevel = borrower?.YearLevel,
                Items = reservation.ReservationItems
                    .OrderBy(item => item.Equipment?.EquipmentName)
                    .Select(item => new PendingReservationItemViewModel
                    {
                        EquipmentName = item.Equipment?.EquipmentName ?? "Unknown equipment",
                        EquipmentCode = item.Equipment?.EquipmentCode,
                        ConditionStatus = item.Equipment?.ConditionStatus,
                        StorageLocation = item.Equipment?.StorageLocation
                    })
                    .ToList()
            };
        }

        public int CountPendingReservations()
        {
            return _reservationRepository.CountPendingReservations();
        }

        private static PendingReservationListItem ToListItem(Reservation reservation)
        {
            var names = reservation.ReservationItems
                .Select(item => item.Equipment?.EquipmentName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .ToList();

            return new PendingReservationListItem
            {
                ReservationId = reservation.ReservationId,
                ReservationNumber = reservation.ReservationNumber,
                BorrowerName = FullName(reservation.BorrowerUser?.User),
                BorrowerUserCode = reservation.BorrowerUser?.User?.UserCode,
                DepartmentName = reservation.BorrowerUser?.Department?.DepartmentName,
                EligibilityStatus = reservation.BorrowerUser?.EligibilityStatus,
                Purpose = reservation.Purpose,
                RequestedAt = reservation.RequestedAt,
                StartDateTime = reservation.StartDateTime,
                ExpectedReturnDateTime = reservation.ExpectedReturnDateTime,
                ItemCount = reservation.ReservationItems.Count,
                EquipmentSummary = names.Count == 0
                    ? "No equipment listed"
                    : names.Count <= 2
                        ? string.Join(", ", names)
                        : $"{names[0]}, {names[1]} +{names.Count - 2} more",
                Status = reservation.Status
            };
        }

        private static string FullName(User user)
        {
            if (user == null)
                return "Unknown borrower";

            return string.Join(" ", new[] { user.FirstName, user.MiddleName, user.LastName, user.Suffix }
                .Where(part => !string.IsNullOrWhiteSpace(part)));
        }
    }
}
