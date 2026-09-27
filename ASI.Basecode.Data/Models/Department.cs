using System;
using System.Collections.Generic;

namespace ASI.Basecode.Data.Models;

public partial class Department
{
    public int DepartmentId { get; set; }

    public string DepartmentCode { get; set; }

    public string DepartmentName { get; set; }

    public string Description { get; set; }

    public bool IsActive { get; set; }

    public string CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; }

    public string UpdatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual ICollection<BorrowerProfile> BorrowerProfiles { get; set; } = new List<BorrowerProfile>();
}
