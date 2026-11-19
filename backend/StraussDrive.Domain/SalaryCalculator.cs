namespace StraussDrive.Domain;

public sealed class SalaryCalculator(TaxRules tax, BusinessRules business) : ISalaryCalculator
{
    public SalaryBreakdown Calculate(VehicleBenefitScenario scenario)
    {
        ValidateRules();
        if (scenario?.Employee is not { } employee || employee.Occupation is null ||
            string.IsNullOrWhiteSpace(employee.Id) || string.IsNullOrWhiteSpace(employee.Occupation.Id))
            throw new CalculationException("Employee identity and occupation are required.");
        if (employee.MonthlyGrossSalary < 0 || employee.TaxCreditPoints < 0 ||
            scenario.EmployeeNetParticipation < 0 || scenario.TaxableMaintenanceAllowance < 0 ||
            scenario.Car?.MonthlyTaxableValueOfUse < 0)
            throw new CalculationException("Salary, credits, vehicle value and deductions must be nonnegative.");
        if (scenario.Car is not null && (string.IsNullOrWhiteSpace(scenario.Car.Id) || string.IsNullOrWhiteSpace(scenario.Car.Name)))
            throw new CalculationException("Car identity and name are required.");
        if (scenario.Car is null && (scenario.EmployeeNetParticipation != 0 || scenario.IncludeElectricReimbursement))
            throw new CalculationException("Participation and electric reimbursement require a car.");
        if (scenario.IncludeElectricReimbursement && scenario.Car?.IsElectric != true)
            throw new CalculationException("Electric reimbursement requires an electric car.");
        if (employee.PensionRate is < 0 or > 1 || employee.StudyFundRate is < 0 or > 1 ||
            (tax.RequireEmployeeContributionRates && (employee.PensionRate is null || employee.StudyFundRate is null)))
            throw new CalculationException("Employee pension and study fund rates are required fractional rates between zero and one.");
        var baseline = Compute(scenario, 0);
        if (!scenario.IncludeElectricReimbursement) return baseline;
        decimal low = 0, high = business.GrossUpMaxGross;
        var target = baseline.NetSalary + business.ElectricReimbursementNetTarget;
        if (Compute(scenario, high).NetSalary < target)
            throw new CalculationException("Reimbursement cannot be grossed up within the configured gross limit.");
        for (var i = 0; i < business.GrossUpMaxIterations; i++)
        {
            var gross = low + (high - low) / 2;
            var result = Compute(scenario, gross);
            if (result.NetSalary == target) return result;
            if (gross == low || gross == high) break;
            if (result.NetSalary < target) low = gross; else high = gross;
        }
        // Resolve the piecewise linear segment with decimal interpolation, without display rounding.
        var lower = Compute(scenario, low);
        var upper = Compute(scenario, high);
        if (upper.NetSalary != lower.NetSalary)
        {
            var exact = Compute(scenario, low + (target - lower.NetSalary) *
                (high - low) / (upper.NetSalary - lower.NetSalary));
            if (exact.NetSalary == target) return exact;
        }
        throw new CalculationException("Gross-up could not represent the exact net target in decimal arithmetic.");
    }

    private SalaryBreakdown Compute(VehicleBenefitScenario s, decimal reimbursement)
    {
        var e = s.Employee;
        var value = s.Car?.MonthlyTaxableValueOfUse ?? 0;
        var cash = e.MonthlyGrossSalary + s.TaxableMaintenanceAllowance + reimbursement;
        var taxable = cash + value;

        var national = Progressive(taxable, tax.NationalInsurance);
        var health = Progressive(taxable, tax.HealthInsurance);
        var pension = Contribution(e.MonthlyGrossSalary + (business.PensionIncludesValueOfUse ? value : 0), tax.EmployeePension with { Rate = e.PensionRate ?? tax.EmployeePension.Rate });
        var study = Contribution(e.MonthlyGrossSalary + (business.StudyFundIncludesValueOfUse ? value : 0), tax.StudyFund with { Rate = e.StudyFundRate ?? tax.StudyFund.Rate });
        var pensionCredit = tax.PensionCredit is { } credit
            ? Math.Min(pension, Math.Min(e.MonthlyGrossSalary, credit.IncomeCeiling) * credit.MaxContributionRate) * credit.Rate : 0;
        var totalCredits = e.TaxCreditPoints * tax.TaxCreditPointValue + pensionCredit;
        var income = Math.Max(0, Progressive(taxable, tax.IncomeTax) - totalCredits);
        return new(cash, taxable, income, national, health, pension, study,
            s.EmployeeNetParticipation, reimbursement,
            reimbursement == 0
                ? cash - (income + national + health + pension + study + s.EmployeeNetParticipation)
                : Compute(s, 0).NetSalary + NetIncrement(e.MonthlyGrossSalary + s.TaxableMaintenanceAllowance + value, reimbursement, totalCredits));
    }
    // Integrate marginal take-home rates directly. Subtracting nearly equal large
    // salary/tax totals would lose decimal precision in the final gross-up digits.
    private decimal NetIncrement(decimal start, decimal gross, decimal credits)
    {
        var boundaries = tax.IncomeTax.Concat(tax.NationalInsurance).Concat(tax.HealthInsurance)
            .Where(b => b.UpperBound.HasValue).Select(b => b.UpperBound!.Value).ToList();
        decimal previous = 0, accumulatedTax = 0;
        decimal? creditExhaustion = credits == 0 ? 0 : null;
        foreach (var b in tax.IncomeTax)
        {
            if (creditExhaustion.HasValue) break;
            if (b.Rate > 0 && (b.UpperBound is null || accumulatedTax + (b.UpperBound.Value - previous) * b.Rate >= credits))
                creditExhaustion = previous + (credits - accumulatedTax) / b.Rate;
            if (b.UpperBound is { } end) { accumulatedTax += (end - previous) * b.Rate; previous = end; }
        }
        if (creditExhaustion is { } threshold) boundaries.Add(threshold);
        var points = boundaries.Select(b => b - start).Where(b => b > 0 && b < gross).Append(gross).Distinct().Order().ToArray();
        decimal net = 0, cursor = 0;
        foreach (var end in points)
        {
            var incomeRate = creditExhaustion is { } exhausted && start + cursor >= exhausted ? RateAt(start + cursor, tax.IncomeTax) : 0;
            var takeHomeRate = 1 - incomeRate - RateAt(start + cursor, tax.NationalInsurance) - RateAt(start + cursor, tax.HealthInsurance);
            net += (end - cursor) * takeHomeRate;
            cursor = end;
        }
        return net;
    }
    private static decimal RateAt(decimal income, TaxBracket[] brackets) =>
        brackets.First(b => b.UpperBound is null || b.UpperBound > income).Rate;
    private static decimal Contribution(decimal basis, ContributionRules rules) =>
        Math.Min(basis, rules.MonthlyCeiling ?? basis) * rules.Rate;
    public static decimal Progressive(decimal basis, IReadOnlyList<TaxBracket> brackets)
    {
        decimal total = 0, previous = 0;
        foreach (var bracket in brackets)
        {
            var end = bracket.UpperBound ?? Math.Max(basis, previous);
            total += Math.Max(0, Math.Min(basis, end) - previous) * bracket.Rate;
            previous = end;
        }
        return total;
    }
    private void ValidateRules()
    {
        if (tax is null || business is null || tax.Status != "configured" || business.Status != "configured" ||
            string.IsNullOrWhiteSpace(tax.Source) || string.IsNullOrWhiteSpace(business.Source))
            throw new CalculationException("Fictional tax and business rules have not been imported and configured.");
        ValidateBrackets(tax.IncomeTax); ValidateBrackets(tax.NationalInsurance); ValidateBrackets(tax.HealthInsurance);
        if (tax.TaxCreditPointValue < 0 || !ValidContribution(tax.EmployeePension) || !ValidContribution(tax.StudyFund) ||
            business.ElectricReimbursementNetTarget <= 0 ||
            business.GrossUpMaxIterations <= 0 || business.GrossUpMaxGross < business.ElectricReimbursementNetTarget)
            throw new CalculationException("Invalid tax credits, contributions or gross-up configuration.");
        if (tax.PensionCredit is { } pensionCredit && (pensionCredit.Rate is < 0 or > 1 ||
            pensionCredit.MaxContributionRate is < 0 or > 1 || pensionCredit.IncomeCeiling <= 0))
            throw new CalculationException("Invalid pension tax credit configuration.");
        // A positive marginal take-home rate makes the gross-up search monotonic.
        if (tax.IncomeTax.Max(x => x.Rate) + tax.NationalInsurance.Max(x => x.Rate) + tax.HealthInsurance.Max(x => x.Rate) >= 1)
            throw new CalculationException("Combined marginal payroll tax rates must be less than one.");
    }
    private static bool ValidContribution(ContributionRules? r) => r is not null && r.Rate >= 0 && r.Rate <= 1 && (r.MonthlyCeiling is null || r.MonthlyCeiling >= 0);
    private static void ValidateBrackets(TaxBracket[]? brackets)
    {
        if (brackets is null || brackets.Length == 0) throw new CalculationException("A complete progressive bracket schedule is required.");
        decimal previous = 0;
        for (var i = 0; i < brackets.Length; i++)
        {
            var b = brackets[i];
            if (b is null || b.Rate < 0 || b.Rate >= 1 ||
                (i == brackets.Length - 1 ? b.UpperBound is not null : b.UpperBound is null || b.UpperBound <= previous))
                throw new CalculationException("Brackets must increase, use fractional rates, and end with an unbounded bracket.");
            previous = b.UpperBound ?? previous;
        }
    }
}
