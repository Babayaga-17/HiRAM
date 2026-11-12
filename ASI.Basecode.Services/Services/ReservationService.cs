// ASI.Basecode.Services/Services/ReservationService.cs
using ASI.Basecode.Data.Exceptions;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Data.Repositories;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using ASI.Basecode.Services.ServiceModels.Reservation;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace ASI.Basecode.Services.Services
{
    public class ReservationService : IReservationService
    {
        private static readonly string[] Tabs = { ReservationStatuses.Pending, ReservationStatuses.Approved, ReservationStatuses.Borrowed, ReservationStatuses.Returned, ReservationStatuses.Rejected, ReservationFilters.All };

        private readonly IReservationRepository _reservationRepository;
        private readonly IEquipmentService _equipmentService;
        private readonly INotificationService _notifications;

        public ReservationService(IReservationRepository reservationRepository, 
                                  INotificationService notifications,
                                  IEquipmentService equipmentService)
        {
            _reservationRepository = reservationRepository;
            _notifications = notifications;
            _equipmentService = equipmentService;
        }

        public async Task<MyReservationPageViewModel> GetBorrowerReservationsAsync(int borrowerUserId, string status, int page, int pageSize)
        {
            if (borrowerUserId <= 0) throw new ArgumentOutOfRangeException(nameof(borrowerUserId));

            status = MyReservationPageViewModel.StatusOptions.FirstOrDefault(value => string.Equals(value, status?.Trim(), StringComparison.OrdinalIgnoreCase));
            pageSize = pageSize == 10 || pageSize == 20 ? pageSize : 5;
            var now = DateTime.Now;
            var total = await _reservationRepository.CountBorrowerReservationsAsync(borrowerUserId, null, now);
            var filteredTotal = status == null ? total : await _reservationRepository.CountBorrowerReservationsAsync(borrowerUserId, status, now);
            var totalPages = Math.Max(1, (int)Math.Ceiling(filteredTotal / (double)pageSize));
            page = Math.Clamp(page, 1, totalPages);
            var rows = await _reservationRepository.GetBorrowerReservationPageAsync(borrowerUserId, status, now, (page - 1) * pageSize, pageSize);
            return new MyReservationPageViewModel
            {
                Status = status, Page = page, PageSize = pageSize, TotalCount = total, FilteredCount = filteredTotal,
                Items = rows.Select(r =>
                {
                    var outstanding = r.Status == ReservationStatuses.Borrowed || r.Status == ReservationStatuses.Overdue;
                    return new MyReservationCard
                    {
                        ReservationId = r.ReservationId, ReservationNumber = r.ReservationNumber,
                        Status = outstanding ? (r.ExpectedReturnDateTime < now ? ReservationStatuses.Overdue : ReservationStatuses.Borrowed) : r.Status,
                        PickupAt = r.StartDateTime, ReturnBy = r.ExpectedReturnDateTime,
                        EquipmentNames = r.ReservationItems
                            .OrderBy(i => i.ReservationItemId)
                            .Select(i => i.Equipment?.EquipmentName ?? "Equipment")
                            .ToList()
                    };
                }).ToList()
            };
        }

        public async Task<List<PendingReservationListItem>> GetPendingReservationPreviewAsync()
        {
            var reservations = await _reservationRepository.GetPendingReservationPreviewAsync();
            return reservations.Select(ToListItem).ToList();
        }

        public (string ReservationNumber, string Error) Submit(ReservationFormModel form, int borrowerUserId)
        {
            var ids = form.EquipmentIds.Distinct().OrderBy(id => id).ToList();

            if (ids.Any(id => id <= 0))
                return (null, "Review your equipment selection.");

            var number = "RES-" + Guid.NewGuid().ToString("N").Substring(0, 26);
            var error = ExecuteChange(() =>
            {
                var profile = _reservationRepository.GetBorrowerProfile(borrowerUserId);
                if (profile?.EligibilityStatus != EligibilityStatuses.Eligible
                    || profile.User?.Account?.AccountStatus != AccountStatuses.Active
                    || profile.User.Account.Role?.RoleName != "Borrower")
                    return "Your account needs an eligible borrower profile before you can reserve equipment. Contact the custodian.";

                var selectionError = ValidateEquipment(ids, form.Pickup.Value, form.ExpectedReturn.Value);
                if (selectionError != null)
                    return selectionError;

                var now = PhilippineTimeNow();
                var actor = borrowerUserId.ToString();
                var reservation = new Reservation
                {
                    ReservationNumber = number,
                    BorrowerUserId = borrowerUserId,
                    Purpose = form.Purpose.Trim(),
                    StartDateTime = form.Pickup.Value,
                    ExpectedReturnDateTime = form.ExpectedReturn.Value,
                    Status = ReservationStatuses.Pending,
                    RequestedAt = now,
                    CreatedAt = now,
                    CreatedBy = actor,
                    ReservationItems = ids.Select(id => new ReservationItem
                    {
                        EquipmentId = id,
                        CreatedAt = now,
                        CreatedBy = actor
                    }).ToList()
                };
                _reservationRepository.AddReservation(reservation);
                _notifications.ReservationChanged(reservation, NotificationEvents.ReservationSubmitted);
                return null;
            }, "Equipment availability changed while submitting. Please try again.");

            return error == null ? (number, null) : (null, error);
        }

        public string Edit(int reservationId, ReservationEditFormModel form, int borrowerUserId)
        {
            var errors = new List<ValidationResult>();

            var version = ReadRowVersion(form.RowVersion);
            if (version == null)
                return "Reload the reservation before editing.";

            var ids = form.EquipmentIds.Distinct().OrderBy(id => id).ToList();
            if (ids.Any(id => id <= 0))
                return "Review your equipment selection.";

            return ExecuteChange(() =>
            {
                var reservation = _reservationRepository.GetReservationForUpdate(reservationId, borrowerUserId);
                if (reservation == null)
                    return "Reservation not found.";
                if (reservation.Status != ReservationStatuses.Pending)
                    return "Only pending reservations can be edited.";
                if (!reservation.RowVersion.SequenceEqual(version))
                    return "This reservation has changed. Reload it before editing.";

                var selectionError = ValidateEquipment(ids, form.Pickup.Value, form.ExpectedReturn.Value, reservationId);
                if (selectionError != null)
                    return selectionError;

                var now = PhilippineTimeNow();
                var actor = borrowerUserId.ToString();
                var removedItems = reservation.ReservationItems
                    .Where(item => !ids.Contains(item.EquipmentId)).ToList();
                _reservationRepository.RemoveReservationItems(removedItems);

                var existingIds = reservation.ReservationItems.Select(item => item.EquipmentId).ToHashSet();
                foreach (var id in ids.Where(id => !existingIds.Contains(id)))
                {
                    reservation.ReservationItems.Add(new ReservationItem
                    {
                        EquipmentId = id,
                        CreatedAt = now,
                        CreatedBy = actor
                    });
                }

                reservation.StartDateTime = form.Pickup.Value;
                reservation.ExpectedReturnDateTime = form.ExpectedReturn.Value;
                reservation.Purpose = form.Purpose.Trim();
                reservation.UpdatedBy = actor;
                reservation.UpdatedAt = now;
                return null;
            }, "This reservation changed while you were editing. Reload it and try again.");
        }

        public string Cancel(int reservationId, ReservationCancelFormModel form, int borrowerUserId)
        {
            var errors = new List<ValidationResult>();
            if (!Validator.TryValidateObject(form, new ValidationContext(form), errors, true))
                return errors.First().ErrorMessage;

            var version = ReadRowVersion(form.RowVersion);
            if (version == null)
                return "Reload the reservation before cancelling.";

            return ExecuteChange(() =>
            {
                var reservation = _reservationRepository.GetReservationForUpdate(reservationId, borrowerUserId);
                if (reservation == null)
                    return "Reservation not found.";
                if (reservation.Status != ReservationStatuses.Pending)
                    return "Only pending reservations can be cancelled.";
                if (!reservation.RowVersion.SequenceEqual(version))
                    return "This reservation has changed. Reload it before cancelling.";

                var now = PhilippineTimeNow();
                reservation.Status = ReservationStatuses.Cancelled;
                reservation.CancelledAt = now;
                reservation.CancellationReason = string.IsNullOrWhiteSpace(form.Reason) ? null : form.Reason.Trim();
                reservation.UpdatedBy = borrowerUserId.ToString();
                reservation.UpdatedAt = now;
                _notifications.ReservationChanged(reservation, NotificationEvents.ReservationCancelled);
                return null;
            }, "This reservation changed while you were cancelling. Reload it and try again.");
        }

        private static byte[] ReadRowVersion(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            try
            {
                var version = Convert.FromBase64String(value);
                return version.Length == 8 ? version : null;
            }
            catch (FormatException)
            {
                return null;
            }
        }

        private string ValidateEquipment(List<int> ids, DateTime pickup, DateTime expectedReturn, int? excludeReservationId = null)
        {
            foreach (var id in ids)
            {
                var equipment = _reservationRepository.GetEquipment(id);
                if (equipment == null || (equipment.CategoryId.HasValue && equipment.Category?.IsActive != true))
                    return "Some equipment is no longer available. Review your selection.";

                var availability = _equipmentService.GetAvailability(id, pickup, expectedReturn, excludeReservationId);
                if (!availability.IsAvailable)
                    return equipment.EquipmentName + ": " + availability.Reason;
            }
            return null;
        }

        private static DateTime PhilippineTimeNow() => TimeZoneInfo.ConvertTimeFromUtc(
        DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Asia/Manila"));

        public ReservationViewModel GetReservationDetails(int reservationId, int borrowerUserId)
        {
            var reservation = _reservationRepository.GetReservationDetails(reservationId, borrowerUserId);
            return reservation == null ? null : ToViewModel(reservation);
        }

        private static ReservationViewModel ToViewModel(Reservation reservation)
        {
            return new ReservationViewModel
            {
                ReservationId = reservation.ReservationId,
                ReservationNumber = reservation.ReservationNumber,
                Status = reservation.Status,
                Purpose = reservation.Purpose,
                RequestedAt = reservation.RequestedAt,
                StartDateTime = reservation.StartDateTime,
                ExpectedReturnDateTime = reservation.ExpectedReturnDateTime,
                ApprovedAt = reservation.ApprovedAt,
                RejectedAt = reservation.RejectedAt,
                BorrowedAt = reservation.BorrowedAt,
                ReturnedAt = reservation.ReturnedAt,
                CancelledAt = reservation.CancelledAt,
                RejectionReason = reservation.RejectionReason,
                CancellationReason = reservation.CancellationReason,
                RowVersion = reservation.RowVersion,
                Equipment = reservation.ReservationItems.OrderBy(item => item.Equipment.EquipmentName)
                    .Select(item => new EquipmentListItem
                    {
                        EquipmentId = item.EquipmentId,
                        EquipmentName = item.Equipment.EquipmentName,
                        EquipmentCode = item.Equipment.EquipmentCode
                    }).ToList()
            };
        }

        private string ExecuteChange(Func<string> operation, string concurrencyMessage)
        {
            try
            {
                return _reservationRepository.ExecuteInTransaction(operation);
            }
            catch (ReservationConcurrencyException)
            {
                return concurrencyMessage;
            }
        }

        // ---------------------------------------------------------------- tabs + details (any status)

        public async Task<ReservationListViewModel> GetReservationsAsync(string status)
        {
            status = Tabs.FirstOrDefault(tab => string.Equals(tab, status, StringComparison.OrdinalIgnoreCase)) ?? ReservationStatuses.Pending;

            var counts = await _reservationRepository.CountByStatus();

            var reservations = await _reservationRepository.GetReservationsByStatusAsync(status);

            var items = reservations
                .Select(ToListItem)
                .ToList();

            return new ReservationListViewModel
            {
                Status = status,
                Items = items,
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
            return Review(reservationId, reservation =>
            {
                if (reservation == null)
                    return (false, "That request was not found.");
                if (reservation.Status != ReservationStatuses.Pending)
                    return (false, "Only pending requests can be approved. This one was already reviewed.");
                if (reservation.BorrowerUser?.EligibilityStatus != EligibilityStatuses.Eligible)
                    return (false, "This borrower is not eligible to borrow, so the request can't be approved.");

                var now = DateTime.Now;
                reservation.Status = ReservationStatuses.Approved;
                reservation.ApprovedByUserId = ResolveUser(actorUserId);
                reservation.ApprovedAt = now;
                reservation.UpdatedBy = actorName;
                reservation.UpdatedAt = now;

                _notifications.ReservationChanged(reservation, NotificationEvents.ReservationApproved);
                return (true, $"{reservation.ReservationNumber} was approved. It is now ready for release.");
            });
        }

        public (bool ok, string message) Reject(int reservationId, int? actorUserId, string actorName, string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
                return (false, "Enter a reason for rejecting this request.");

            return Review(reservationId, reservation =>
            {
                if (reservation == null)
                    return (false, "That request was not found.");
                if (reservation.Status != ReservationStatuses.Pending)
                    return (false, "Only pending requests can be rejected. This one was already reviewed.");

                var now = DateTime.Now;
                reservation.Status = ReservationStatuses.Rejected;
                reservation.RejectedByUserId = ResolveUser(actorUserId);
                reservation.RejectedAt = now;
                reservation.RejectionReason = reason.Trim();
                reservation.UpdatedBy = actorName;
                reservation.UpdatedAt = now;

                _notifications.ReservationChanged(reservation, NotificationEvents.ReservationRejected);
                return (true, $"{reservation.ReservationNumber} was rejected.");
            });
        }

        public (bool ok, string message) Release(ReleaseReservationRequest request, string actorName)
        {
            if (request == null)
                return (false, "The release form was empty.");

            var reservation = _reservationRepository.GetReservationForUpdate(request.ReservationId);
            if (reservation == null)
                return (false, "That request was not found.");
            if (reservation.Status != ReservationStatuses.Approved)
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

            reservation.Status = ReservationStatuses.Borrowed;
            reservation.BorrowedAt = request.ReleasedAt ?? now;
            reservation.UpdatedBy = actorName;
            reservation.UpdatedAt = now;

            _reservationRepository.SaveChanges();
            return (true, $"{reservation.ReservationNumber} was released and marked as borrowed.");
        }

        private (bool ok, string message) Review(int id, Func<Reservation, (bool Ok, string Message)> operation)
        {
            try { return _reservationRepository.Review(id, operation); }
            catch (ASI.Basecode.Data.Exceptions.ReservationConcurrencyException)
            { return (false, "This reservation changed during review. Reload it and try again."); }
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

            var reservation = _reservationRepository.GetReservationForUpdate(request.ReservationId);

            if (reservation == null)
                return (false, "Reservation not found.");

            if (reservation.Status != ReservationStatuses.Borrowed)
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
                    || input.ConditionAtReturn == EquipmentConditions.Lost)
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

                if (input.ConditionAtReturn == EquipmentConditions.Damaged
                    && string.IsNullOrWhiteSpace(input.DamageNotes)
                    && string.IsNullOrWhiteSpace(input.MissingPartsNotes))
                {
                    return (false, "Describe the damage or missing parts.");
                }

                if (!string.IsNullOrWhiteSpace(input.MissingPartsNotes)
                    && input.ConditionAtReturn != EquipmentConditions.Damaged)
                {
                    return (false,
                        "Mark equipment with missing parts as damaged pending review.");
                }
            }

            var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Asia/Manila"));

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
                reservation.Status = ReservationStatuses.Returned;
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
