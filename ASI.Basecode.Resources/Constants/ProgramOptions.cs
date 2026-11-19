using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace ASI.Basecode.Resources.Constants
{
    // Departments (including names, IDs, and active states) remain in SQL.
    // Only permitted program choices are defined here, keyed by department code.
    public static class ProgramOptions
    {
        public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> ByDepartment =
            new ReadOnlyDictionary<string, IReadOnlyList<string>>(
                new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
                {
                    ["CCS"] = Array.AsReadOnly(new[] {
                        "BS Computer Science (AI-Focused Elective)",
                        "BS Information Technology",
                        "BS Information Technology - Software Engineering" }),
                    ["COE"] = Array.AsReadOnly(new[] { "BS Computer Engineering" }),
                    ["CBA"] = Array.AsReadOnly(new[] {
                        "BS Accountancy", "BS Accountancy - Information Systems",
                        "BS Business Administration - Financial Management",
                        "BS Business Administration - Human Resources Management",
                        "BS Business Administration - Marketing Management",
                        "BS Business Administration - Operations Management",
                        "BS Management Accounting" }),
                    ["CED"] = Array.AsReadOnly(new[] { "BEEd - Elementary Education", "BSEd - English" }),
                    ["CAS"] = Array.AsReadOnly(new[] { "BS Psychology" }),
                    ["CCJE"] = Array.AsReadOnly(new[] { "BS Criminology" }),
                    ["CON"] = Array.AsReadOnly(new[] { "BS Nursing" }),
                    ["CAHS"] = Array.AsReadOnly(new[] { "BS Medical Technology" }),
                    ["CHTCA"] = Array.AsReadOnly(new[] { "BS Hospitality Management", "BS Tourism Management" }),
                    ["SOL"] = Array.AsReadOnly(new[] { "Juris Doctor" })
                });

        public static IReadOnlyList<string> GetPrograms(string? departmentCode)
        {
            return departmentCode != null && ByDepartment.TryGetValue(departmentCode, out var programs)
                ? programs : Array.Empty<string>();
        }

        public static bool IsProgramAllowed(string? departmentCode, string? program)
        {
            return !string.IsNullOrWhiteSpace(program)
                && GetPrograms(departmentCode).Contains(program, StringComparer.Ordinal);
        }
    }
}
