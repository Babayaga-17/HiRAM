
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using Basecode.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace ASI.Basecode.Data.Repositories
{
    public class EquipmentRepository : BaseRepository, IEquipmentRepository
    {
        public EquipmentRepository(IUnitOfWork unitOfWork) : base(unitOfWork)
        {
        }

        public void AddEquipment(Equipment equipment)
        {
            GetDbSet<Equipment>().Add(equipment);
            UnitOfWork.SaveChanges();
        }

        public void DeleteEquipment(Equipment equipment)
        {
            GetDbSet<Equipment>().Remove(equipment);
            UnitOfWork.SaveChanges();
        }

        public IQueryable<Equipment> GetEquipments()
        {
            return GetDbSet<Equipment>().AsNoTracking();
        }

        public Equipment GetEquipmentById(int id)
        {
            return GetDbSet<Equipment>()
                .AsTracking()
                .FirstOrDefault(equipment => equipment.EquipmentId == id);
        }

        public void UpdateEquipment(Equipment equipment)
        {
            GetDbSet<Equipment>().Update(equipment);
            UnitOfWork.SaveChanges();
        }

        public bool EquipmentCodeExists(string equipmentCode, int? excludeEquipmentId = null)
        {
            var query = GetDbSet<Equipment>()
                .Where(equipment => equipment.EquipmentCode == equipmentCode);

            if (excludeEquipmentId.HasValue)
            {
                query = query.Where(
                    equipment => equipment.EquipmentId != excludeEquipmentId.Value);
            }

            return query.Any();
        }

        public bool SerialNumberExists(string serialNumber, int? excludeEquipmentId = null)
        {
            var query = GetDbSet<Equipment>()
                .Where(equipment => equipment.SerialNumber == serialNumber);

            if (excludeEquipmentId.HasValue)
            {
                query = query.Where(
                    equipment => equipment.EquipmentId != excludeEquipmentId.Value);
            }

            return query.Any();
        }

        public bool AssetTagExists(string assetTag, int? excludeEquipmentId = null)
        {
            var query = GetDbSet<Equipment>()
                .Where(equipment => equipment.AssetTag == assetTag);

            if (excludeEquipmentId.HasValue)
            {
                query = query.Where(
                    equipment => equipment.EquipmentId != excludeEquipmentId.Value);
            }

            return query.Any();
        }
    }
}
