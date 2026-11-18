using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ASI.Basecode.Services.Exceptions;
public class ReservationValidationException : Exception
{
    public IReadOnlyList<ValidationResult> Errors { get; }

    public ReservationValidationException(IEnumerable<ValidationResult> errors) : base("Reservation validation failed.")
    {
        Errors = errors.ToList().AsReadOnly();
    }
}
