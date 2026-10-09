using System;

namespace ASI.Basecode.Data.Exceptions;

public class EquipmentCategoryConflictException : Exception
{
    public EquipmentCategoryConflictException(Exception innerException)
        : base("An equipment category could not be saved because a unique value already exists.", innerException)
    {
    }
}
