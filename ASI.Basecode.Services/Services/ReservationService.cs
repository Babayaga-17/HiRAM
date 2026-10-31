// ASI.Basecode.Services/Services/ReservationService.cs
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Data.Repositories;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ASI.Basecode.Services.Services
{
    public class ReservationService : IReservationService
    {
        private static readonly string[] Tabs = { "Pending", "Approved", "Borrowed", "Returned", "Rejected", "All" };

        private readonly IReservationRepository _reservationRepository;

        public ReservationService(IReservationRepository reservationRepository)
        {
            _reservationRepository = reservationRepository;
        }

        // ---------------------------------------------------------------- existing (pending only)

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

        // ---------------------------------------------------------------- tabs + details (any status)

        public ReservationListViewModel GetReservations(string status)
        {
            status = Tabs.FirstOrDefault(tab => string.Equals(tab, status, StringComparison.OrdinalIgnoreCase)) ?? "Pending";

            var counts = _reservationRepository.CountByStatus();
            return new ReservationListViewModel
            {
                Status = status,
                Items = _reservationRepository.GetReservationsByStatus(status).ToList().Select(ToListItem).ToList(),
                Counts = counts,
                TotalCount = counts.Values.Sum()
            };
        }

        public ReservationDetailsViewModel GetReservationDetails(int reservationId)
        {
            var reservation = _reservationRepository.GetReservationById(reservationId);
            if (reservation == null)
                return null;

            var listItem = ToListItem(reservation);
            var borrower = reservation.BorrowerUser;

            return new ReservationDetailsViewModel
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
                ContactNumber = borrower?.User?.ContactNumber,
                Program = borrower?.Program,
                YearLevel = borrower?.YearLevel,
                ApprovedAt = reservation.ApprovedAt,
                ApprovedByName = reservation.ApprovedByUser == null ? null : FullName(reservation.ApprovedByUser),
                RejectedAt = reservation.RejectedAt,
                RejectionReason = reservation.RejectionReason,
                BorrowedAt = reservation.BorrowedAt,
                ReturnedAt = reservation.ReturnedAt,
                RowVersion = Convert.ToBase64String(reservation.RowVersion),
                Items = reservation.ReservationItems
                    .OrderBy(item => item.Equipment?.EquipmentName)
                    .Select(item => new ReservationItemViewModel
                    {
                        ReservationItemId = item.ReservationItemId,
                        EquipmentName = item.Equipment?.EquipmentName ?? "Unknown equipment",
                        EquipmentCode = item.Equipment?.EquipmentCode,
                        ConditionStatus = item.Equipment?.ConditionStatus,
                        StorageLocation = item.Equipment?.StorageLocation,
                        ConditionAtBorrowing = item.ConditionAtBorrowing,
                        BorrowingRemarks = item.BorrowingRemarks,
                        IsReturned = item.IsReturned,
                        ReturnedAt = item.ReturnedAt,
                        ConditionAtReturn = item.ConditionAtReturn,
                        ReturnRemarks = item.ReturnRemarks,
                        DamageNotes = item.DamageNotes,
                        MissingPartsNotes = item.MissingPartsNotes
                    })
                    .ToList()

            };
        }

        // ---------------------------------------------------------------- approve / reject / release

        public (bool ok, string message) Approve(int reservationId, int? actorUserId, string actorName, string remarks)
        {
            var reservation = _reservationRepository.GetReservationForUpdate(reservationId);
            if (reservation == null)
                return (false, "That request was not found.");
            if (reservation.Status != ReservationRepository.PendingStatus)
                return (false, "Only pending requests can be approved. This one was already reviewed.");
            if (reservation.BorrowerUser?.EligibilityStatus != "Eligible")
                return (false, "This borrower is not eligible to borrow, so the request can't be approved.");

            var now = DateTime.Now;
            reservation.Status = ReservationRepository.ApprovedStatus;
            reservation.ApprovedByUserId = ResolveUser(actorUserId);
            reservation.ApprovedAt = now;
            reservation.UpdatedBy = actorName;
            reservation.UpdatedAt = now;

            _reservationRepository.SaveChanges();
            return (true, $"{reservation.ReservationNumber} was approved. It is now ready for release.");
        }

        public (bool ok, string message) Reject(int reservationId, int? actorUserId, string actorName, string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
                return (false, "Enter a reason for rejecting this request.");

            var reservation = _reservationRepository.GetReservationForUpdate(reservationId);
            if (reservation == null)
                return (false, "That request was not found.");
            if (reservation.Status != ReservationRepository.PendingStatus)
                return (false, "Only pending requests can be rejected. This one was already reviewed.");

            var now = DateTime.Now;
            reservation.Status = ReservationRepository.RejectedStatus;
            reservation.RejectedByUserId = ResolveUser(actorUserId);
            reservation.RejectedAt = now;
            reservation.RejectionReason = reason.Trim();
            reservation.UpdatedBy = actorName;
            reservation.UpdatedAt = now;

            _reservationRepository.SaveChanges();
            return (true, $"{reservation.ReservationNumber} was rejected.");
        }

        public (bool ok, string message) Release(ReleaseReservationRequest request, string actorName)
        {
            if (request == null)
                return (false, "The release form was empty.");

            var reservation = _reservationRepository.GetReservationForUpdate(request.ReservationId);
            if (reservation == null)
                return (false, "That request was not found.");
            if (reservation.Status != ReservationRepository.ApprovedStatus)
                return (false, "Only approved requests can be released.");
            if (reservation.ReservationItems.Count == 0)
                return (false, "This request has no equipment to release.");

            var inputs = request.Items ?? new List<ReleaseItemRequest>();
            foreach (var item in reservation.ReservationItems)
            {
                var input = inputs.FirstOrDefault(i => i.ReservationItemId == item.ReservationItemId);
                if (input == null || string.IsNullOrWhiteSpace(input.ConditionAtBorrowing))
                    return (false, "Record the condition of every item before releasing.");
            }

            var now = DateTime.Now;
            foreach (var item in reservation.ReservationItems)
            {
                var input = inputs.First(i => i.ReservationItemId == item.ReservationItemId);
                item.ConditionAtBorrowing = input.ConditionAtBorrowing.Trim();
                item.BorrowingRemarks = string.IsNullOrWhiteSpace(input.BorrowingRemarks) ? null : input.BorrowingRemarks.Trim();
                item.UpdatedBy = actorName;
                item.UpdatedAt = now;
            }

            reservation.Status = ReservationRepository.BorrowedStatus;
            reservation.BorrowedAt = request.ReleasedAt ?? now;
            reservation.UpdatedBy = actorName;
            reservation.UpdatedAt = now;

            _reservationRepository.SaveChanges();
            return (true, $"{reservation.ReservationNumber} was released and marked as borrowed.");
        }

        // ---------------------------------------------------------------- helpers

        // Only keep the approver's id when it really is a row in Users (avoids a foreign key error).
        private int? ResolveUser(int? userId)
        {
            return userId.HasValue && _reservationRepository.UserExists(userId.Value) ? userId : null;
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

        public (bool ok, string message) Return(ReturnReservationRequest request, int? actorUserId, string actorName)
        {
            if (request == null)
                return (false, "The return form was empty.");

            var reservation =
                _reservationRepository.GetReservationForUpdate(request.ReservationId);

            if (reservation == null)
                return (false, "Reservation not found.");

            if (reservation.Status != ReservationRepository.BorrowedStatus)
                return (false, "Only borrowed reservations can be returned.");

            if (string.IsNullOrWhiteSpace(request.RowVersion)
                || request.RowVersion !=
                    Convert.ToBase64String(reservation.RowVersion))
            {
                return (false,
                    "This reservation has changed. Reload it before recording the return.");
            }

            var receiverId = ResolveUser(actorUserId);

            if (!receiverId.HasValue)
                return (false, "Your user account could not be verified. Sign in again.");

            var selected = (request.Items ?? new List<ReturnItemRequest>())
                .Where(item => item != null && item.Receive)
                .ToList();

            if (selected.Count == 0)
                return (false, "Select at least one item you have received.");

            if (selected.Select(item => item.ReservationItemId)
                .Distinct().Count() != selected.Count)
            {
                return (false, "An item was submitted more than once. Reload the form.");
            }

            // Validate the entire selection before changing any records.
            foreach (var input in selected)
            {
                var item = reservation.ReservationItems.SingleOrDefault(
                    item => item.ReservationItemId == input.ReservationItemId);

                if (item == null)
                    return (false, "A selected item does not belong to this reservation.");

                if (item.IsReturned)
                    return (false, "A selected item has already been returned.");

                if (item.Equipment == null)
                    return (false, "The equipment record could not be found.");

                if (!EquipmentFormViewModel.Conditions.Contains(input.ConditionAtReturn)
                    || input.ConditionAtReturn == "Lost")
                {
                    return (false,
                        "Select a valid condition for each received item.");
                }

                var notes = new[]
                {
            input.ReturnRemarks,
            input.DamageNotes,
            input.MissingPartsNotes
        };

                if (notes.Any(note => note?.Length > 500))
                    return (false, "Each inspection note must be 500 characters or fewer.");

                if (input.ConditionAtReturn == "Damaged"
                    && string.IsNullOrWhiteSpace(input.DamageNotes)
                    && string.IsNullOrWhiteSpace(input.MissingPartsNotes))
                {
                    return (false, "Describe the damage or missing parts.");
                }

                if (!string.IsNullOrWhiteSpace(input.MissingPartsNotes)
                    && input.ConditionAtReturn != "Damaged")
                {
                    return (false,
                        "Mark equipment with missing parts as damaged pending review.");
                }
            }

            var now = TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.UtcNow,
                TimeZoneInfo.FindSystemTimeZoneById("Asia/Manila"));

            foreach (var input in selected)
            {
                var item = reservation.ReservationItems.Single(
                    item => item.ReservationItemId == input.ReservationItemId);

                item.IsReturned = true;
                item.ReturnedAt = now;
                item.ConditionAtReturn = input.ConditionAtReturn;
                item.ReturnRemarks = CleanReturnNote(input.ReturnRemarks);
                item.DamageNotes = CleanReturnNote(input.DamageNotes);
                item.MissingPartsNotes = CleanReturnNote(input.MissingPartsNotes);
                item.UpdatedBy = actorName;
                item.UpdatedAt = now;

                item.Equipment.ConditionStatus = input.ConditionAtReturn;
                item.Equipment.UpdatedBy = actorName;
                item.Equipment.UpdatedAt = now;
            }

            var allReturned = reservation.ReservationItems.All(item => item.IsReturned);

            if (allReturned)
            {
                reservation.Status = "Returned";
                reservation.ReturnedAt = now;
                reservation.ReceivedByUserId = receiverId;
            }

            // Updating the parent also activates its row-version concurrency check.
            reservation.UpdatedBy = actorName;
            reservation.UpdatedAt = now;

            _reservationRepository.SaveChanges();

            return (true, allReturned
                ? "All items returned. Reservation completed."
                : "Selected items returned. Other items are still awaiting return.");
        }

        private static string CleanReturnNote(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }
}
