using System;
using System.Collections.Generic;

namespace ASI.Basecode.Data.Models;

public partial class EquipmentCategory
{
    public int CategoryId { get; set; }

    public string CategoryCode { get; set; }

    public string CategoryName { get; set; }

    public string Description { get; set; }

    public bool IsActive { get; set; }

    public string CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; }

    public string UpdatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual ICollection<Equipment> Equipment { get; set; } = new List<Equipment>();
}
