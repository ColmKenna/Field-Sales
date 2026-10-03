namespace FieldSales.Api.Coverage;

// Business reporting, not an account or a copy of identity roles/credentials.
public sealed class RepReportingLine
{
    private RepReportingLine() { }

    public string RepSubject { get; private set; } = string.Empty;
    public string ManagerSubject { get; private set; } = string.Empty;
    public byte[] Version { get; private set; } = [];

    public static RepReportingLine Create(string? repSubject, string? managerSubject) => new()
    {
        RepSubject = CoverageSubjects.Validate(repSubject, "RepSubject"),
        ManagerSubject = CoverageSubjects.Validate(managerSubject, "ManagerSubject")
    };

    // The future Head Office handler verifies current staff eligibility before calling this.
    public void SetManager(string? managerSubject) =>
        ManagerSubject = CoverageSubjects.Validate(managerSubject, "ManagerSubject");
}
