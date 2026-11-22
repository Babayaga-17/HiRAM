using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ASI.Basecode.Services.Exceptions;

public class EquipmentValidationException : Exception
{
    public IReadOnlyDictionary<string, string> Errors { get; }

    public EquipmentValidationException(IDictionary<string, string> errors) : base("Equipment validation failed.")
    {
        Errors = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(errors));
    }
}