using ASI.Basecode.Services.ServiceModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ASI.Basecode.Services.Interfaces
{
    public interface IEquipmentService
    {
        void AddEquipment(EquipmentFormViewModel model, string createdBy);
        void DeleteEquipment(int id, string deletedBy);
        List<EquipmentListItem> GetEquipments();
        EquipmentListItem GetEquipmentById(int id);
        bool EditEquipment(EquipmentFormViewModel model, string updatedBy);
        bool SetEquipmentStatus(int equipmentId, bool isActive, string updatedBy);
        List<EquipmentListItem> GetBorrowableEquipments();
        EquipmentListItem GetBorrowableEquipmentById(int id);
        List<EquipmentBusyPeriod> GetSchedule(int equipmentId, DateTime start, DateTime end, int? excludeReservationId = null);
        List<EquipmentCalendarEvent> GetCalendarEvents(DateTime start, DateTime end);
        EquipmentAvailabilityResult GetAvailability(int equipmentId, DateTime start, DateTime end, int? excludeReservationId = null);
    }
}