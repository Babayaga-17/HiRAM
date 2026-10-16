using ASI.Basecode.Data.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ASI.Basecode.Data.Interfaces
{
    public interface IEquipmentRepository
    {
        void AddEquipment(Equipment equipment);
        void DeleteEquipment(Equipment equipment);
        IQueryable<Equipment> GetEquipments();
        Equipment GetEquipmentById(int id);
        void UpdateEquipment(Equipment equipment);
        bool EquipmentCodeExists(string equipmentCode, int? excludeEquipmentId = null);
        bool SerialNumberExists(string serialNumber, int? excludeEquipmentId = null);
        bool AssetTagExists(string assetTag, int? excludeEquipmentId = null);
    }
}