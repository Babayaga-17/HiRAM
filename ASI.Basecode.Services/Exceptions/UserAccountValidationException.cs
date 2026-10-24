using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ASI.Basecode.Services.Exceptions;

/// <summary>
/// Thrown by UserAccountService when input or a business rule fails.
/// Keys are form field names; string.Empty is a general (non-field) error.
/// </summary>
public class UserAccountValidationException : Exception
{
    public IReadOnlyDictionary<string, string> Errors { get; }

    public UserAccountValidationException(IDictionary<string, string> errors) : base("User account validation failed.")
    {
        Errors = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(errors));
    }

    public UserAccountValidationException(string field, string message)
        : this(new Dictionary<string, string> { [field ?? string.Empty] = message })
    {
    }
}
