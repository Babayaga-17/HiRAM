using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Data.Repositories;
using ASI.Basecode.Services.Exceptions;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ASI.Basecode.Services.Services
{
    public class EquipmentService : IEquipmentService
    {
        private static readonly string[] BlockingReservationStatuses = { "Approved", "Borrowed", "Overdue" };
        private static readonly string[] BorrowableConditions = { "Excellent", "Good", "Fair" };

        private readonly IEquipmentRepository _equipmentRepository;
        private readonly IEquipmentCategoryService _equipmentCategoryService;

        public EquipmentService(IEquipmentRepository equipmentRepository, IEquipmentCategoryService equipmentCategoryService)
        {
            _equipmentRepository = equipmentRepository;
            _equipmentCategoryService = equipmentCategoryService;
        }

        public void AddEquipment(EquipmentFormViewModel model, string createdBy)
        {
            Normalize(model);

            var errors = GetValidationErrors(model);

            if (errors.Count > 0)
                throw new EquipmentValidationException(errors);

            var equipment = new Equipment
            {
                CategoryId = model.CategoryId,
                EquipmentCode = model.EquipmentCode,
                AssetTag = model.AssetTag,
                EquipmentName = model.EquipmentName,
                Brand = model.Brand,
                Model = model.Model,
                SerialNumber = model.SerialNumber,
                Description = model.Description,
                ConditionStatus = model.ConditionStatus,
                StorageLocation = model.StorageLocation,
                AcquisitionDate = model.AcquisitionDate.HasValue ? DateOnly.FromDateTime(model.AcquisitionDate.Value) : null,
                AcquisitionCost = model.AcquisitionCost,
                IsActive = model.IsActive,
                CreatedBy = createdBy
            };

            _equipmentRepository.AddEquipment(equipment);
        }

        public bool EditEquipment(EquipmentFormViewModel model, string updatedBy)
        {
            var equipment = _equipmentRepository.GetEquipmentById(model.EquipmentId);

            if (equipment == null)
                return false;

            Normalize(model);

            var errors = GetValidationErrors(model, equipment.EquipmentId, equipment.CategoryId);

            if (errors.Count > 0)
                throw new EquipmentValidationException(errors);

            equipment.CategoryId = model.CategoryId;
            equipment.EquipmentCode = model.EquipmentCode;
            equipment.AssetTag = model.AssetTag;
            equipment.EquipmentName = model.EquipmentName;
            equipment.Brand = model.Brand;
            equipment.Model = model.Model;
            equipment.SerialNumber = model.SerialNumber;
            equipment.Description = model.Description;
            equipment.ConditionStatus = model.ConditionStatus;
            equipment.StorageLocation = model.StorageLocation;
            equipment.AcquisitionDate = model.AcquisitionDate.HasValue ? DateOnly.FromDateTime(model.AcquisitionDate.Value) : null;
            equipment.AcquisitionCost = model.AcquisitionCost;
            equipment.IsActive = model.IsActive;
            equipment.UpdatedBy = updatedBy;
            equipment.UpdatedAt = DateTime.Now;

            _equipmentRepository.UpdateEquipment(equipment);

            return true;
        }

        public bool SetEquipmentStatus(int equipmentId, bool isActive, string updatedBy)
        {
            var equipment = _equipmentRepository.GetEquipmentById(equipmentId);

            if (equipment == null)
                return false;

            if (equipment.IsActive == isActive)
                return true;

            if (!isActive)
            {
                var now = DateTime.Now;
                var hasActiveBooking = _equipmentRepository.GetEquipments().Any(item => item.EquipmentId == equipmentId
                    && item.ReservationItems.Any(reservationItem => !reservationItem.IsReturned
                        && BlockingReservationStatuses.Contains(reservationItem.Reservation.Status)
                        && reservationItem.Reservation.ExpectedReturnDateTime > now));

                if (hasActiveBooking)
                {
                    throw new EquipmentValidationException(new Dictionary<string, string>
                    {
                        [string.Empty] = "This equipment has an approved or active reservation. Resolve it before deactivating."
                    });
                }
            }

            equipment.IsActive = isActive;
            equipment.UpdatedBy = updatedBy;
            equipment.UpdatedAt = DateTime.Now;
            _equipmentRepository.UpdateEquipment(equipment);

            return true;
        }

        public void DeleteEquipment(int id, string deletedBy)
        {
            var equipment = _equipmentRepository.GetEquipmentById(id);

            if (equipment == null)
            {
                throw new EquipmentValidationException(new Dictionary<string, string>
                {
                    [string.Empty] = "Equipment not found."
                });
            }

            if (equipment.IsActive)
            {
                throw new EquipmentValidationException(new Dictionary<string, string>
                {
                    [string.Empty] = "Please deactivate the equipment before deleting it."
                });
            }

            _equipmentRepository.DeleteEquipment(equipment);
        }

        public List<EquipmentListItem> GetEquipments()
        {
            return GetEquipmentItems().OrderBy(item => item.EquipmentName).ToList();
        }

        public EquipmentListItem GetEquipmentById(int id)
        {
            return GetEquipmentItems().Where(item => item.EquipmentId == id).SingleOrDefault();
        }

        // equipment a borrower can browse (active equipment in active categories)
        public List<EquipmentListItem> GetBorrowableEquipments()
        {
            return GetEquipmentItems(GetBorrowerVisibleEquipments())
                .OrderBy(item => item.EquipmentName)
                .ToList();
        }

        public EquipmentListItem GetBorrowableEquipmentById(int id)
        {
            return GetEquipmentItems(GetBorrowerVisibleEquipments().Where(equipment => equipment.EquipmentId == id))
                .SingleOrDefault();
        }

        public EquipmentAvailabilityResult GetAvailability(int equipmentId, DateTime start, DateTime end)
        {
            var result = new EquipmentAvailabilityResult();

            if (end <= start)
            {
                result.Reason = "The end date and time must be after the start date and time.";
                return result;
            }

            var equipment = GetBorrowerVisibleEquipments()
                .Where(item => item.EquipmentId == equipmentId)
                .Select(item => new { item.IsActive, item.ConditionStatus })
                .SingleOrDefault();

            if (equipment == null)
            {
                result.Reason = "This equipment is not available for borrowing.";
                return result;
            }

            if (!equipment.IsActive)
            {
                result.Reason = "This equipment is inactive and cannot be borrowed.";
                return result;
            }

            if (!BorrowableConditions.Contains(equipment.ConditionStatus))
            {
                result.Reason = "Equipment in " + equipment.ConditionStatus + " condition cannot be borrowed.";
                return result;
            }

            if (start < DateTime.Now.AddMinutes(-5))
            {
                result.Reason = "The start date and time cannot be in the past.";
                return result;
            }

            result.Periods = GetBusyPeriods(equipmentId, start, end);
            result.Slots = GetFreeSlots(result.Periods, start, end);

            if (result.Periods.Any(period => period.Type == "Maintenance"))
                result.Reason = "This equipment is scheduled for maintenance during this period.";
            else if (result.Periods.Count > 0)
                result.Reason = "This equipment is already reserved during this period.";
            else
                result.IsAvailable = true;

            return result;
        }

        private IQueryable<Equipment> GetBorrowerVisibleEquipments()
        {
            return _equipmentRepository.GetEquipments();
        }

        // Approved, borrowed and overdue reservations plus scheduled maintenance that overlap [start, end).
        // Only dates are returned. Borrower names are never exposed.
        private List<EquipmentBusyPeriod> GetBusyPeriods(int equipmentId, DateTime start, DateTime end)
        {
            var reservations = _equipmentRepository.GetEquipments()
                .Where(item => item.EquipmentId == equipmentId)
                .SelectMany(item => item.ReservationItems)
                .Where(reservationItem => !reservationItem.IsReturned
                    && BlockingReservationStatuses.Contains(reservationItem.Reservation.Status)
                    && reservationItem.Reservation.StartDateTime < end
                    && reservationItem.Reservation.ExpectedReturnDateTime > start)
                .Select(reservationItem => new EquipmentBusyPeriod
                {
                    Start = reservationItem.Reservation.StartDateTime,
                    End = reservationItem.Reservation.ExpectedReturnDateTime,
                    Type = "Reservation",
                    Label = reservationItem.Reservation.Status == "Approved" ? "Reserved" : "In use"
                })
                .ToList();

            var maintenance = _equipmentRepository.GetEquipments()
                .Where(item => item.EquipmentId == equipmentId)
                .SelectMany(item => item.MaintenanceRecords)
                .Where(record => (record.Status == "Scheduled" || record.Status == "InProgress")
                    && record.StartDateTime < end
                    && (record.EndDateTime == null || record.EndDateTime > start))
                .Select(record => new { record.StartDateTime, record.EndDateTime })
                .ToList()
                .Select(record => new EquipmentBusyPeriod
                {
                    Start = record.StartDateTime,
                    End = record.EndDateTime ?? end,
                    Type = "Maintenance",
                    Label = "Under maintenance"
                })
                .ToList();

            return reservations.Concat(maintenance).OrderBy(period => period.Start).ToList();
        }

        // The gaps between busy periods are the dates and times the equipment is free.
        private static List<EquipmentFreeSlot> GetFreeSlots(List<EquipmentBusyPeriod> periods, DateTime start, DateTime end)
        {
            var slots = new List<EquipmentFreeSlot>();
            var cursor = start;

            foreach (var period in periods.OrderBy(item => item.Start))
            {
                if (period.Start > cursor)
                    slots.Add(new EquipmentFreeSlot { Start = cursor, End = period.Start });

                if (period.End > cursor)
                    cursor = period.End;
            }

            if (cursor < end)
                slots.Add(new EquipmentFreeSlot { Start = cursor, End = end });

            return slots;
        }

        private IQueryable<EquipmentListItem> GetEquipmentItems(IQueryable<Equipment> source = null)
        {
            var now = DateTime.Now;

            return (source ?? _equipmentRepository.GetEquipments()).Select(equipment => new EquipmentListItem
            {
                EquipmentId = equipment.EquipmentId,
                EquipmentCode = equipment.EquipmentCode,
                AssetTag = equipment.AssetTag,
                EquipmentName = equipment.EquipmentName,
                CategoryId = equipment.CategoryId,
                CategoryName = equipment.Category != null ? equipment.Category.CategoryName : string.Empty,
                Brand = equipment.Brand,
                Model = equipment.Model,
                SerialNumber = equipment.SerialNumber,
                Description = equipment.Description,
                ConditionStatus = equipment.ConditionStatus,
                StorageLocation = equipment.StorageLocation,
                AcquisitionDate = equipment.AcquisitionDate,
                AcquisitionCost = equipment.AcquisitionCost,
                IsActive = equipment.IsActive,
                NextReservationStart = equipment.ReservationItems
                    .Where(item => !item.IsReturned && item.Reservation.Status == "Approved" && item.Reservation.StartDateTime > now)
                    .Min(item => (DateTime?)item.Reservation.StartDateTime),
                Status =
                    (!equipment.IsActive || equipment.ConditionStatus == "Lost" || equipment.ConditionStatus == "Damaged")
                        ? EquipmentAvailability.Unavailable
                    : equipment.MaintenanceRecords.Any(record =>
                            (record.Status == "InProgress" || record.Status == "Scheduled")
                            && record.StartDateTime <= now
                            && (record.EndDateTime == null || record.EndDateTime > now))
                        ? EquipmentAvailability.UnderMaintenance
                    : equipment.ReservationItems.Any(item => !item.IsReturned
                            && (item.Reservation.Status == "Borrowed" || item.Reservation.Status == "Overdue"))
                        ? EquipmentAvailability.Borrowed
                    : equipment.ReservationItems.Any(item => item.Reservation.Status == "Approved"
                            && item.Reservation.StartDateTime <= now
                            && item.Reservation.ExpectedReturnDateTime > now)
                        ? EquipmentAvailability.Reserved
                    : EquipmentAvailability.Available
            });
        }

        private static string Clean(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        // Blank serial number and asset tag must become null because of the unique indexes.
        private static void Normalize(EquipmentFormViewModel model)
        {
            model.EquipmentCode = model.EquipmentCode?.Trim();
            model.EquipmentName = model.EquipmentName?.Trim();
            model.ConditionStatus = model.ConditionStatus?.Trim();
            model.AssetTag = Clean(model.AssetTag);
            model.SerialNumber = Clean(model.SerialNumber);
            model.Brand = Clean(model.Brand);
            model.Model = Clean(model.Model);
            model.Description = Clean(model.Description);
            model.StorageLocation = Clean(model.StorageLocation);
        }

        private Dictionary<string, string> GetValidationErrors(EquipmentFormViewModel model, int? equipmentId = null, int? currentCategoryId = null)
        {
            var errors = new Dictionary<string, string>();

            if (_equipmentRepository.EquipmentCodeExists(model.EquipmentCode, equipmentId))
                errors.Add(nameof(model.EquipmentCode), "This equipment code is already in use.");

            if (model.AssetTag != null && _equipmentRepository.AssetTagExists(model.AssetTag, equipmentId))
                errors.Add(nameof(model.AssetTag), "This asset tag is already in use.");

            if (model.SerialNumber != null && _equipmentRepository.SerialNumberExists(model.SerialNumber, equipmentId))
                errors.Add(nameof(model.SerialNumber), "This serial number is already in use.");

            if (!EquipmentFormViewModel.Conditions.Contains(model.ConditionStatus))
                errors.Add(nameof(model.ConditionStatus), "Select a valid condition.");

            if (!model.CategoryId.HasValue)
            {
                errors.Add(nameof(model.CategoryId), "Please select a category.");
            }
            else
            {
                var category = _equipmentCategoryService.GetEquipmentCategoryById(model.CategoryId.Value);

                if (category == null)
                    errors.Add(nameof(model.CategoryId), "The selected category no longer exists.");
                else if (!category.IsActive && category.CategoryId != currentCategoryId)
                    errors.Add(nameof(model.CategoryId), "The selected category is inactive. Choose an active category.");
            }

            if (model.AcquisitionDate.HasValue && model.AcquisitionDate.Value.Date > DateTime.Today)
                errors.Add(nameof(model.AcquisitionDate), "Acquisition date cannot be in the future.");

            return errors;
        }
    }
}