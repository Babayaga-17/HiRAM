using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ASI.Basecode.Services.Exceptions;

public class EquipmentCategoryValidationException : Exception
{
    public IReadOnlyDictionary<string, string> Errors { get; }

    public EquipmentCategoryValidationException(IDictionary<string, string> errors)
        : base("Equipment category validation failed.")
    {
        Errors = new ReadOnlyDictionary<string, string>(
            new Dictionary<string, string>(errors));
    }
}
