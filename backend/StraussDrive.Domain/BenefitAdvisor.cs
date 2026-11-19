namespace StraussDrive.Domain;

/// <summary>Eligibility and scenario assembly use imported identities and full vehicle keys, never name-based valuation.</summary>
public sealed class BenefitAdvisor(HackathonPolicy policy, TaxRules tax, BusinessRules business,
    IReadOnlyDictionary<string, CatalogVehicle> catalog, IReadOnlyDictionary<string, OccupationPolicy> occupations,
    IReadOnlyDictionary<VehicleKey, VehicleValue> values)
{
    public BenefitEligibility CheckEligibility(SampleEmployee employee, string? carId)
    {
        if (!occupations.TryGetValue(employee.Occupation, out var occupation))
            throw new CalculationException($"Unknown occupation '{employee.Occupation}'.");
        if (employee.Group != occupation.Group || !policy.Budgets.TryGetValue(employee.Group, out var budget))
            throw new CalculationException("Employee group must match the imported occupation entitlement group.");
        if (employee.EligibleChildren < 0) throw new CalculationException("Eligible child count cannot be negative.");
        if (carId is null)
            return new(!occupation.MandatoryCar && occupation.CanWaive, "Waiver depends on occupation; mandatory cars cannot be waived.", budget, 0, 0, 0);
        if (!catalog.TryGetValue(carId, out var car)) throw new CalculationException($"Unknown catalog car '{carId}'.");
        if (policy.Family.Cars.Contains(carId))
        {
            var gap = Math.Max(0, car.MonthlyCost - budget);
            return new(employee.EligibleChildren >= policy.Family.MinChildren,
                $"Family track requires {policy.Family.MinChildren} children aged up to {policy.Family.ChildMaxAge}.",
                budget, car.MonthlyCost, gap * policy.Family.EmployeeGapShare, gap * (1 - policy.Family.EmployeeGapShare));
        }
        var own = Array.IndexOf(policy.GroupOrder, employee.Group);
        var selected = Array.IndexOf(policy.GroupOrder, car.Group);
        return new(selected >= 0 && selected <= own && car.MonthlyCost <= budget,
            "Car must be in own group or lower and within the group budget; unused budget is not paid.", budget, car.MonthlyCost, 0, 0);
    }
    public BenefitCalculation Calculate(SampleEmployee employee, string? carId, decimal? participationOverride = null)
    {
        var eligibility = CheckEligibility(employee, carId);
        if (!eligibility.Eligible) throw new CalculationException($"Ineligible benefit: {eligibility.Reason}");
        var occupation = occupations[employee.Occupation];
        var payroll = new Employee(employee.Id, new(occupation.Name, occupation.Name), employee.Gross,
            employee.CreditPoints, employee.PensionRate, employee.StudyFundRate);
        VehicleBenefitScenario scenario;
        if (carId is null)
        {
            if (participationOverride is not null and not 0) throw new CalculationException("Waiver cannot carry a car participation deduction.");
            scenario = new(payroll, TaxableMaintenanceAllowance: occupation.MaintenanceAllowance);
        }
        else
        {
            var car = catalog[carId];
            if (!values.TryGetValue(car.Key, out var value)) throw new CalculationException($"Missing full vehicle key {car.Key}.");
            if (!policy.DriveCodes.TryGetValue(value.DriveCode, out var drive)) throw new CalculationException("Unknown vehicle drive code.");
            var electric = drive == policy.DriveCodes["3"];
            scenario = new(payroll, new(car.Id, car.CommercialName, electric, value.ValueOfUse),
                (participationOverride ?? car.DefaultParticipation) + eligibility.FamilyEmployeeShare,
                IncludeElectricReimbursement: electric);
        }
        return new(eligibility, scenario, new SalaryCalculator(tax, business).Calculate(scenario));
    }
    public SalaryComparison Compare(SampleEmployee employee, string? currentId, string? alternativeId) =>
        new(Calculate(employee, currentId, employee.CurrentParticipation), Calculate(employee, alternativeId));
}
