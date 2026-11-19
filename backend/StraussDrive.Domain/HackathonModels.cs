namespace StraussDrive.Domain;

public sealed record PensionTaxCredit(decimal Rate, decimal MaxContributionRate, decimal IncomeCeiling);
public sealed record VehicleKey(int TaxYear, int RegistrationYear, string VehicleTypeCode, string ManufacturerCode, string ModelCode);
public sealed record VehicleValue(VehicleKey Key, string TechnicalDescription, string DriveCode, decimal ValueOfUse,
    IReadOnlyDictionary<string, string> SourceValues);
public sealed record CatalogVehicle(string Id, string Group, string Manufacturer, string CommercialName,
    string TechnicalDescription, string Drive, decimal ListPrice, decimal MonthlyCost,
    decimal DefaultParticipation, decimal PortfolioValueOfUse, VehicleKey Key,
    IReadOnlyDictionary<string, string> SourceValues);
public sealed record OccupationPolicy(string Name, string Group, bool MandatoryCar, bool CanWaive,
    decimal MaintenanceAllowance, IReadOnlyDictionary<string, string> SourceValues);
public sealed record SampleEmployee(string Id, string Name, string Occupation, string Group,
    decimal BaseSalary, decimal Commission, decimal Bonus, decimal PensionRate, decimal StudyFundRate,
    decimal CreditPoints, int EligibleChildren, string CurrentCar, decimal CurrentParticipation,
    IReadOnlyDictionary<string, string> SourceValues)
{
    public decimal Gross => BaseSalary + Commission + Bonus;
}
public sealed record ReferenceCalculation(string EmployeeId, string Current, string Alternative, bool Eligible,
    decimal CurrentNet, decimal AlternativeNet, decimal NetDifference, decimal CurrentTax, decimal AlternativeTax,
    decimal CurrentInsurance, decimal AlternativeInsurance, decimal ElectricGross, decimal FamilyParticipation,
    IReadOnlyDictionary<string, string> SourceValues);
public sealed record FamilyPolicy(int MinChildren, int ChildMaxAge, string[] Cars, decimal EmployeeGapShare);
public sealed record HackathonPolicy(IReadOnlyDictionary<string, decimal> Budgets, string[] GroupOrder,
    FamilyPolicy Family, IReadOnlyDictionary<string, string> DriveCodes);
public sealed record BenefitEligibility(bool Eligible, string Reason, decimal Budget, decimal MonthlyCost,
    decimal FamilyEmployeeShare, decimal FamilyEmployerShare);
public sealed record BenefitCalculation(BenefitEligibility Eligibility, VehicleBenefitScenario Scenario, SalaryBreakdown Salary);
public sealed record SalaryComparison(BenefitCalculation Current, BenefitCalculation Alternative)
{
    public decimal NetDifference => Alternative.Salary.NetSalary - Current.Salary.NetSalary;
    // The fictional portfolio displays the difference of its separately displayed net amounts.
    // This presentation choice never feeds back into payroll or gross-up calculations.
    public decimal DisplayNetDifference => DifferenceOfDisplayedNets;
    public decimal RoundedRawNetDifference => decimal.Round(NetDifference, 0, MidpointRounding.AwayFromZero);
    public decimal DifferenceOfDisplayedNets => Alternative.Salary.DisplayNetSalary - Current.Salary.DisplayNetSalary;
}
