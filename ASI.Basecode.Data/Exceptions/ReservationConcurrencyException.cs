using System;

namespace ASI.Basecode.Data.Exceptions;

public class ReservationConcurrencyException : Exception
{
    public ReservationConcurrencyException(Exception innerException)
        : base("The reservation changed during the database operation.", innerException)
    {
    }
}
