using System.Security.Cryptography;
using System.Text.Json;
using StraussDrive.Domain;


namespace StraussDrive.Import;

public sealed record SourceArtifact(string FileName, string Sha256, long Bytes);
public sealed record ValidationObservation(string EmployeeId, string Field, decimal Expected, decimal Actual)
{
    public bool Matches => Expected == Actual;
}
public sealed record ReferenceEvidence(string EmployeeId, SalaryComparison Result);
public sealed record ImportReport(string RuleVersion, int PayrollTaxYear, int VehicleRecords,
    int CatalogVehicles, int Occupations, int Employees, int References, SourceArtifact[] Sources,
    string[] Warnings, ValidationObservation[] Observations, ReferenceEvidence[] Calculations)
{
    public ValidationObservation[] Mismatches => Observations.Where(o => !o.Matches).ToArray();
}
public sealed class HackathonData
{
    public required MappedRules Rules { get; init; }
    public required IReadOnlyDictionary<VehicleKey, VehicleValue> VehicleValues { get; init; }
    public required IReadOnlyDictionary<string, CatalogVehicle> Catalog { get; init; }
    public required IReadOnlyDictionary<string, OccupationPolicy> Occupations { get; init; }
    public required IReadOnlyDictionary<string, SampleEmployee> Employees { get; init; }
    public required IReadOnlyDictionary<string, ReferenceCalculation> References { get; init; }
    public required IReadOnlyList<SourceSheet> Sheets { get; init; }
    public required string[] CsvHeaders { get; init; }
    public required JsonElement RawRules { get; init; }
    public required SourceArtifact[] Sources { get; init; }
    public required string[] Warnings { get; init; }
    public BenefitAdvisor Advisor => new(Rules.Policy, Rules.Tax, Rules.Business, Catalog, Occupations, VehicleValues);
    public SalaryComparison CompareReference(string employeeId)
    {
        if (!Employees.TryGetValue(employeeId, out var employee) || !References.TryGetValue(employeeId, out var reference))
            throw new DataImportException($"Missing employee/reference '{employeeId}'.");
        return Advisor.Compare(employee, ResolvePortfolioSelection(reference.Current), ResolvePortfolioSelection(reference.Alternative));
    }
    public string? ResolvePortfolioSelection(string label)
    {
        if (label.StartsWith("ויתור - ", StringComparison.Ordinal)) return null;
        // Names resolve only a portfolio catalog selection. Its exact technical/coded key
        // has already been validated against the CSV; a name never supplies a tax value.
        var matches = Catalog.Values.Where(c => $"{c.CommercialName} ({c.Key.RegistrationYear})" == label).ToArray();
        if (matches.Length != 1) throw new DataImportException($"Portfolio selection '{label}' requires an unambiguous catalog identity.");
        return matches[0].Id;
    }
    public ImportReport ValidateReferences()
    {
        var observations = new List<ValidationObservation>();
        var calculations = new List<ReferenceEvidence>();
        foreach (var reference in References.Values.OrderBy(r => r.EmployeeId))
        {
            var result = CompareReference(reference.EmployeeId);
            calculations.Add(new(reference.EmployeeId, result));
            void Check(string name, decimal expected, decimal actual) => observations.Add(new(reference.EmployeeId, name, expected, actual));
            decimal Display(decimal amount) => decimal.Round(amount, 0, MidpointRounding.AwayFromZero);
            Check("currentNet", reference.CurrentNet, result.Current.Salary.DisplayNetSalary);
            Check("alternativeNet", reference.AlternativeNet, result.Alternative.Salary.DisplayNetSalary);
            Check("netDifference", reference.NetDifference, result.DisplayNetDifference);
            Check("currentTax", reference.CurrentTax, Display(result.Current.Salary.IncomeTax));
            Check("alternativeTax", reference.AlternativeTax, Display(result.Alternative.Salary.IncomeTax));
            Check("currentInsurance", reference.CurrentInsurance, Display(result.Current.Salary.NationalInsurance + result.Current.Salary.HealthInsurance));
            Check("alternativeInsurance", reference.AlternativeInsurance, Display(result.Alternative.Salary.NationalInsurance + result.Alternative.Salary.HealthInsurance));
            Check("electricGross", reference.ElectricGross, Display(result.Alternative.Salary.ElectricReimbursementGross));
            Check("familyParticipation", reference.FamilyParticipation, result.Alternative.Eligibility.FamilyEmployeeShare);
            Check("eligible", reference.Eligible ? 1 : 0, result.Alternative.Eligibility.Eligible ? 1 : 0);
        }
        return new(Rules.Version, Rules.PayrollTaxYear, VehicleValues.Count, Catalog.Count, Occupations.Count, Employees.Count,
            References.Count, Sources, Warnings, observations.ToArray(), calculations.ToArray());
    }
}
