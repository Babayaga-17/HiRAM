using ASI.Basecode.Resources.Constants;
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

        private readonly IEquipmentRepository _equipmentRepository;
        private readonly IEquipmentCategoryService _equipmentCategoryService;

        public EquipmentService(IEquipmentRepository equipmentRepository, IEquipmentCategoryService equipmentCategoryService)
        {
            _equipmentRepository = equipmentRepository;
            _equipmentCategoryService = equipmentCategoryService;
        }

        public void AddEquipment(EquipmentFormViewModel model, string createdBy, IReadOnlyList<string> photoPaths = null)
        {
            Normalize(model);

            var errors = GetValidationErrors(model);

            if (errors.Count > 0)
                throw new EquipmentValidationException(errors);

            var equipment = new Equipment
            {
                CategoryId = model.CategoryId.Value, // validated above: GetValidationErrors requires a category
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

            if (photoPaths != null)
            {
                for (var i = 0; i < photoPaths.Count; i++)
                {
                    equipment.EquipmentPhotos.Add(new EquipmentPhoto
                    {
                        StoragePath = photoPaths[i],
                        SortOrder = i
                    });
                }
            }

            _equipmentRepository.AddEquipment(equipment);
        }

        public bool EditEquipment(EquipmentFormViewModel model, string updatedBy, IReadOnlyList<string> photoPaths = null)
        {
            var equipment = _equipmentRepository.GetEquipmentById(model.EquipmentId);

            if (equipment == null)
                return false;

            Normalize(model);

            var errors = GetValidationErrors(model, equipment.EquipmentId, equipment.CategoryId);

            if (errors.Count > 0)
                throw new EquipmentValidationException(errors);

            equipment.CategoryId = model.CategoryId.Value; // validated above
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

            if (photoPaths is { Count: > 0 })
            {
                _equipmentRepository.ExecuteInTransaction(() =>
                {
                    var savedPhotos = _equipmentRepository.GetEquipmentPhotos(equipment.EquipmentId);

                    if (savedPhotos.Count + photoPaths.Count > EquipmentPhotoPolicy.MaxPhotosPerEquipment)
                    {
                        throw new EquipmentValidationException(new Dictionary<string, string>
                        {
                            ["photos"] = $"Each equipment item can have up to {EquipmentPhotoPolicy.MaxPhotosPerEquipment} photos."
                        });
                    }

                    var nextSortOrder = savedPhotos.Count == 0
                        ? 0
                        : savedPhotos[savedPhotos.Count - 1].SortOrder + 1;

                    var newPhotos = new List<EquipmentPhoto>();

                    foreach (var path in photoPaths)
                    {
                        newPhotos.Add(new EquipmentPhoto
                        {
                            EquipmentId = equipment.EquipmentId,
                            StoragePath = path,
                            SortOrder = nextSortOrder++
                        });
                    }

                    _equipmentRepository.AddEquipmentPhotos(newPhotos);
                    _equipmentRepository.UpdateEquipment(equipment);
                });
            }
            else
            {
                _equipmentRepository.UpdateEquipment(equipment);
            }

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
                        && ReservationStatuses.Blocking.Contains(reservationItem.Reservation.Status)
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

        public EquipmentAvailabilityResult GetAvailability(int equipmentId, DateTime start, DateTime end, int? excludeReservationId = null)
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

            if (!EquipmentConditions.Borrowable.Contains(equipment.ConditionStatus))
            {
                result.Reason = "Equipment in " + equipment.ConditionStatus + " condition cannot be borrowed.";
                return result;
            }

            if (start < DateTime.Now.AddMinutes(-5))
            {
                result.Reason = "The start date and time cannot be in the past.";
                return result;
            }

            result.Periods = GetBusyPeriods(equipmentId, start, end, excludeReservationId);
            result.Slots = GetFreeSlots(result.Periods, start, end);

            if (result.Periods.Any(period => period.Type == AvailabilityPeriodTypes.Maintenance))
                result.Reason = "This equipment is scheduled for maintenance during this period.";
            else if (result.Periods.Count > 0)
                result.Reason = "This equipment is already reserved during this period.";
            else
                result.IsAvailable = true;

            return result;
        }

        // Calendar reads may include past days; booking validation remains in GetAvailability.
        public List<EquipmentBusyPeriod> GetSchedule(int equipmentId, DateTime start, DateTime end, int? excludeReservationId = null)
        {
            if (end <= start || (end - start).TotalDays > 62)
                throw new ArgumentException("Choose a calendar range of up to 62 days.");

            return GetBusyPeriods(equipmentId, start, end, excludeReservationId);
        }

        // Reservations and maintenance of active equipment that overlap.
        // Only dates are returned. Borrower names are never exposed.
        public List<EquipmentCalendarEvent> GetCalendarEvents(DateTime start, DateTime end)
        {
            if (end <= start || (end - start).TotalDays > 62)
                throw new ArgumentException("Choose a calendar range of up to 62 days.");

            var reservations = _equipmentRepository.GetEquipments()
                .Where(equipment => equipment.IsActive)
                .SelectMany(equipment => equipment.ReservationItems)
                .Where(reservationItem => !reservationItem.IsReturned
                    && ReservationStatuses.Blocking.Contains(reservationItem.Reservation.Status)
                    && reservationItem.Reservation.StartDateTime < end
                    && reservationItem.Reservation.ExpectedReturnDateTime > start)
                .Select(reservationItem => new
                {
                    reservationItem.EquipmentId,
                    reservationItem.Equipment.EquipmentCode,
                    reservationItem.Equipment.EquipmentName,
                    reservationItem.Equipment.CategoryId,
                    reservationItem.Reservation.Status,
                    reservationItem.Reservation.StartDateTime,
                    reservationItem.Reservation.ExpectedReturnDateTime
                })
                .ToList()
                .Select(row => new EquipmentCalendarEvent
                {
                    EquipmentId = row.EquipmentId,
                    EquipmentCode = row.EquipmentCode,
                    EquipmentName = row.EquipmentName,
                    CategoryId = row.CategoryId,
                    Type = row.Status == ReservationStatuses.Borrowed || row.Status == ReservationStatuses.Overdue ? AvailabilityPeriodTypes.Borrowed : AvailabilityPeriodTypes.Reserved,
                    Status = row.Status,
                    Start = row.StartDateTime,
                    End = row.ExpectedReturnDateTime
                })
                .ToList();

            var maintenance = _equipmentRepository.GetEquipments()
                .Where(equipment => equipment.IsActive)
                .SelectMany(equipment => equipment.MaintenanceRecords)
                .Where(record => (record.Status == MaintenanceStatuses.Scheduled || record.Status == MaintenanceStatuses.InProgress)
                    && record.StartDateTime < end
                    && (record.EndDateTime == null || record.EndDateTime > start))
                .Select(record => new
                {
                    record.EquipmentId,
                    record.Equipment.EquipmentCode,
                    record.Equipment.EquipmentName,
                    record.Equipment.CategoryId,
                    record.Status,
                    record.StartDateTime,
                    record.EndDateTime
                })
                .ToList()
                .Select(row => new EquipmentCalendarEvent
                {
                    EquipmentId = row.EquipmentId,
                    EquipmentCode = row.EquipmentCode,
                    EquipmentName = row.EquipmentName,
                    CategoryId = row.CategoryId,
                    Type = AvailabilityPeriodTypes.Maintenance,
                    Status = row.Status,
                    Start = row.StartDateTime,
                    End = row.EndDateTime ?? end
                })
                .ToList();

            return reservations.Concat(maintenance)
                .OrderBy(item => item.Start)
                .ThenBy(item => item.EquipmentCode)
                .ToList();
        }

        private IQueryable<Equipment> GetBorrowerVisibleEquipments()
        {
            return _equipmentRepository.GetEquipments();
        }

        // Pending, approved, borrowed and overdue reservations plus maintenance that overlap [start, end).
        // Only dates are returned. Borrower names are never exposed.
        private List<EquipmentBusyPeriod> GetBusyPeriods(int equipmentId, DateTime start, DateTime end, int? excludeReservationId = null)
        {
            var reservations = _equipmentRepository.GetEquipments()
                .Where(item => item.EquipmentId == equipmentId)
                .SelectMany(item => item.ReservationItems)
                .Where(reservationItem =>
                    !reservationItem.IsReturned
                    && ReservationStatuses.Blocking.Contains(
                    reservationItem.Reservation.Status)
                    && reservationItem.Reservation.StartDateTime < end
                    && reservationItem.Reservation.ExpectedReturnDateTime > start
                    && (!excludeReservationId.HasValue
                    || reservationItem.ReservationId != excludeReservationId.Value))
                .Select(reservationItem => new EquipmentBusyPeriod
                {
                    Start = reservationItem.Reservation.StartDateTime,
                    End = reservationItem.Reservation.ExpectedReturnDateTime,
                    Type = "Reservation",
                    Label = reservationItem.Reservation.Status == ReservationStatuses.Pending || reservationItem.Reservation.Status == ReservationStatuses.Approved
                        ? "Reserved" : "In use"
                })
                .ToList();

            var maintenance = _equipmentRepository.GetEquipments()
                .Where(item => item.EquipmentId == equipmentId)
                .SelectMany(item => item.MaintenanceRecords)
                .Where(record => (record.Status == MaintenanceStatuses.Scheduled || record.Status == MaintenanceStatuses.InProgress)
                    && record.StartDateTime < end
                    && (record.EndDateTime == null || record.EndDateTime > start))
                .Select(record => new { record.StartDateTime, record.EndDateTime })
                .ToList()
                .Select(record => new EquipmentBusyPeriod
                {
                    Start = record.StartDateTime,
                    End = record.EndDateTime ?? end,
                    Type = AvailabilityPeriodTypes.Maintenance,
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
                    .Where(item => !item.IsReturned
                        && (item.Reservation.Status == ReservationStatuses.Pending || item.Reservation.Status == ReservationStatuses.Approved)
                        && item.Reservation.StartDateTime > now)
                    .Min(item => (DateTime?)item.Reservation.StartDateTime),
                Status =
                    (!equipment.IsActive || equipment.ConditionStatus == EquipmentConditions.Lost || equipment.ConditionStatus == EquipmentConditions.Damaged)
                        ? EquipmentAvailability.Unavailable
                    : equipment.MaintenanceRecords.Any(record =>
                            (record.Status == MaintenanceStatuses.InProgress || record.Status == MaintenanceStatuses.Scheduled)
                            && record.StartDateTime <= now
                            && (record.EndDateTime == null || record.EndDateTime > now))
                        ? EquipmentAvailability.UnderMaintenance
                    : equipment.ReservationItems.Any(item => !item.IsReturned
                            && (item.Reservation.Status == ReservationStatuses.Borrowed || item.Reservation.Status == ReservationStatuses.Overdue))
                        ? EquipmentAvailability.Borrowed
                    : equipment.ReservationItems.Any(item => (item.Reservation.Status == ReservationStatuses.Pending || item.Reservation.Status == ReservationStatuses.Approved)
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

        public EquipmentIndexViewModel GetEquipmentPage(EquipmentListRequest request)
        {
            const int pageSize = 5;

            var search = (request.Search ?? string.Empty).Trim();
            var page = request.Page;
            var categoryId = request.CategoryId;

            if (categoryId <= 0)
            {
                categoryId = null;
            }

            // Translate the selected status into your existing enum.
            EquipmentAvailability? selectedStatus = (request.Status ?? "").ToLowerInvariant() switch
            {
                "available" => EquipmentAvailability.Available,
                "reserved" => EquipmentAvailability.Reserved,
                "borrowed" => EquipmentAvailability.Borrowed,
                "undermaintenance" => EquipmentAvailability.UnderMaintenance,
                "unavailable" => EquipmentAvailability.Unavailable,
                _ => null
            };

            var status = selectedStatus.HasValue
                ? selectedStatus.Value.ToString().ToLowerInvariant()
                : "all";

            var allEquipment = GetEquipmentItems();
            var query = allEquipment;

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(item =>
                    item.EquipmentName.Contains(search) ||
                    item.EquipmentCode.Contains(search) ||
                    (item.Brand != null && item.Brand.Contains(search)) ||
                    (item.Model != null && item.Model.Contains(search)));
            }

            if (categoryId.HasValue)
            {
                query = query.Where(item =>
                    item.CategoryId == categoryId.Value);
            }

            if (selectedStatus.HasValue)
            {
                var availability = selectedStatus.Value;
                query = query.Where(item => item.Status == availability);
            }

            var filteredCount = query.Count();

            var totalPages = Math.Max(
                1,
                (int)Math.Ceiling((double)filteredCount / pageSize));

            page = Math.Clamp(page, 1, totalPages);

            var items = query
                .OrderBy(item => item.EquipmentName)
                .ThenBy(item => item.EquipmentId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return new EquipmentIndexViewModel
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                FilteredCount = filteredCount,
                Search = search,
                CategoryId = categoryId,
                Status = status,

                TotalEquipment = allEquipment.Count(),

                AvailableCount = allEquipment.Count(item =>
                    item.Status == EquipmentAvailability.Available),

                AttentionCount = allEquipment.Count(item =>
                    item.Status == EquipmentAvailability.UnderMaintenance ||
                    item.Status == EquipmentAvailability.Unavailable)
            };
        }

        public List<EquipmentPhoto> GetEquipmentPhotos(int equipmentId)
        {
            return _equipmentRepository.GetEquipmentPhotos(equipmentId);
        }
    }
}
