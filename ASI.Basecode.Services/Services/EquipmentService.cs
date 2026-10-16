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

        // Status is computed from the database: is_active, condition, maintenance and reservations.
        private IQueryable<EquipmentListItem> GetEquipmentItems()
        {
            var now = DateTime.Now;

            return _equipmentRepository.GetEquipments().Select(equipment => new EquipmentListItem
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