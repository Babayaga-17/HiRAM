using System;
using System.Collections.Generic;

namespace ASI.Basecode.Data.Models;

public partial class EquipmentPhoto
{
    public int PhotoId { get; set; }

    public int EquipmentId { get; set; }

    public string StoragePath { get; set; }

    public int SortOrder { get; set; }

    public virtual Equipment Equipment { get; set; }
}
