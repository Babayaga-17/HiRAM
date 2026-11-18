using System;
using System.Collections.Generic;
using System.Linq;

namespace ASI.Basecode.Resources.Constants
{
    public static class AcademicOptions
    {
        public static readonly IReadOnlyList<AcademicDepartmentOption> Departments =
            Array.AsReadOnly(new[]
            {
                new AcademicDepartmentOption("CCS", "College of Computer Studies",
                    "BS Computer Science (AI-Focused Elective)",
                    "BS Information Technology",
                    "BS Information Technology - Software Engineering"),
                new AcademicDepartmentOption("COE", "College of Engineering",
                    "BS Computer Engineering"),
                new AcademicDepartmentOption("CBA", "College of Business and Accountancy",
                    "BS Accountancy",
                    "BS Accountancy - Information Systems",
                    "BS Business Administration - Financial Management",
                    "BS Business Administration - Human Resources Management",
                    "BS Business Administration - Marketing Management",
                    "BS Business Administration - Operations Management",
                    "BS Management Accounting"),
                new AcademicDepartmentOption("CED", "College of Education",
                    "BEEd - Elementary Education", "BSEd - English"),
                new AcademicDepartmentOption("CAS", "College of Arts and Sciences",
                    "BS Psychology"),
                new AcademicDepartmentOption("CCJE", "College of Criminal Justice Education",
                    "BS Criminology"),
                new AcademicDepartmentOption("CON", "College of Nursing", "BS Nursing"),
                new AcademicDepartmentOption("CAHS", "College of Allied Health Sciences",
                    "BS Medical Technology"),
                new AcademicDepartmentOption("CHTCA", "College of Hospitality, Tourism and Customs Administration",
                    "BS Hospitality Management", "BS Tourism Management"),
                new AcademicDepartmentOption("SOL", "School of Law", "Juris Doctor")
            });

        public static AcademicDepartmentOption? FindDepartment(string? code)
        {
            return Departments.FirstOrDefault(department =>
                string.Equals(department.Code, code, StringComparison.OrdinalIgnoreCase));
        }
    }

    public sealed class AcademicDepartmentOption
    {
        public string Code { get; }
        public string Name { get; }
        public IReadOnlyList<string> Programs { get; }

        public AcademicDepartmentOption(string code, string name, params string[] programs)
        {
            Code = code;
            Name = name;
            Programs = Array.AsReadOnly(programs);
        }
    }
}
