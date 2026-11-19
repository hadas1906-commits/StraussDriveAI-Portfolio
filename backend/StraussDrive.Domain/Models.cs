namespace StraussDrive.Domain;

public sealed record Occupation(string Id, string Name);
public sealed record Employee(string Id, Occupation Occupation, decimal MonthlyGrossSalary, decimal TaxCreditPoints,
    decimal? PensionRate = null, decimal? StudyFundRate = null);
public sealed record Car(string Id, string Name, bool IsElectric, decimal MonthlyTaxableValueOfUse);
public sealed record VehicleBenefitScenario(Employee Employee, Car? Car = null,
    decimal EmployeeNetParticipation = 0, decimal TaxableMaintenanceAllowance = 0,
    bool IncludeElectricReimbursement = false);
public sealed record TaxBracket(decimal? UpperBound, decimal Rate);
public sealed record ContributionRules(decimal Rate, decimal? MonthlyCeiling);
public sealed record TaxRules(string Status, string Source, TaxBracket[] IncomeTax,
    decimal TaxCreditPointValue, TaxBracket[] NationalInsurance, TaxBracket[] HealthInsurance,
    ContributionRules EmployeePension, ContributionRules StudyFund, PensionTaxCredit? PensionCredit = null,
    bool RequireEmployeeContributionRates = false);
public sealed record BusinessRules(string Status, string Source, decimal ElectricReimbursementNetTarget,
    int GrossUpMaxIterations, decimal GrossUpMaxGross,
    bool PensionIncludesValueOfUse, bool StudyFundIncludesValueOfUse);
public sealed record SalaryBreakdown(decimal GrossCashSalary, decimal TaxableIncome,
    decimal IncomeTax, decimal NationalInsurance, decimal HealthInsurance,
    decimal EmployeePension, decimal StudyFund, decimal EmployeeNetParticipation,
    decimal ElectricReimbursementGross, decimal NetSalary)
{
    // Presentation only; every calculation above retains decimal precision.
    public decimal DisplayNetSalary => decimal.Round(NetSalary, 0, MidpointRounding.AwayFromZero);
}
public sealed class CalculationException(string message) : Exception(message);
public interface ISalaryCalculator { SalaryBreakdown Calculate(VehicleBenefitScenario scenario); }
