using System.Security.Cryptography;
using System.Text.Json;
using StraussDrive.Domain;

namespace StraussDrive.Import;

public sealed record SourceSheet(string Name, string[][] Rows);
public sealed class DataImportException(string message) : Exception(message);
public sealed record MappedRules(TaxRules Tax, BusinessRules Business, HackathonPolicy Policy, int PayrollTaxYear, string Version);

// Compatibility names keep the original application layers independent of source format.
// Only this original JSON fixture is supported. No challenge artifacts are read.
public static class HackathonImporter
{
    public static string FindRoot(string start)
    {
        for (var dir = new DirectoryInfo(start); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "data", "portfolio-demo.json"))) return dir.FullName;
        throw new DataImportException("Cannot locate data/portfolio-demo.json.");
    }
    public static HackathonData Load(string root)
    {
        try { return LoadCore(root); }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or ArgumentException or KeyNotFoundException or OverflowException)
        { throw new DataImportException("Invalid fictional dataset: " + ex.Message); }
    }
    private static HackathonData LoadCore(string root)
    {
        var path = Path.Combine(root, "data", "portfolio-demo.json");
        var bytes = File.ReadAllBytes(path);
        using var doc = JsonDocument.Parse(bytes);
        var raw = doc.RootElement; var t = raw.GetProperty("tax");
        string Text(JsonElement j, string key) => j.GetProperty(key).GetString() ?? throw new DataImportException("Missing text: " + key);
        decimal Amount(JsonElement j, string key) => j.GetProperty(key).GetDecimal();
        var version = Text(raw, "version");
        TaxBracket[] Flat(decimal rate) => [new(null, rate)];
        var tax = new TaxRules("configured", version,
            t.GetProperty("incomeBrackets").EnumerateArray().Select(b => new TaxBracket(b[0].ValueKind == JsonValueKind.Null ? null : b[0].GetDecimal(), b[1].GetDecimal())).ToArray(),
            Amount(t, "creditPoint"), Flat(Amount(t, "nationalInsurance")), Flat(Amount(t, "healthInsurance")), new(0, null), new(0, null), null, true);
        var business = new BusinessRules("configured", version, Amount(raw, "electricNetBonus"), 256, 1000000, false, false);
        var budgets = raw.GetProperty("budgets").EnumerateObject().ToDictionary(x => x.Name, x => x.Value.GetDecimal());
        var f = raw.GetProperty("family");
        var familyIds = raw.GetProperty("vehicles").EnumerateArray().Where(v => v.TryGetProperty("family", out var b) && b.GetBoolean()).Select(v => Text(v, "id")).ToArray();
        var share = Amount(f, "employeeGapShare");
        if (share is < 0 or > 1 || budgets.Count == 0 || budgets.Values.Any(v => v <= 0) || f.GetProperty("minChildren").GetInt32() < 1)
            throw new DataImportException("Invalid fictional policy.");
        var policy = new HackathonPolicy(budgets, budgets.Keys.ToArray(),
            new(f.GetProperty("minChildren").GetInt32(), f.GetProperty("maxAge").GetInt32(), familyIds, share),
            new Dictionary<string, string> { ["0"] = "בנזין פיקטיבי", ["3"] = "חשמל פיקטיבי" });
        var values = new Dictionary<VehicleKey, VehicleValue>(); var catalog = new Dictionary<string, CatalogVehicle>();
        foreach (var v in raw.GetProperty("vehicles").EnumerateArray())
        {
            var id = Text(v, "id"); var group = Text(v, "group"); var drive = Text(v, "drive");
            if (!budgets.ContainsKey(group) || !policy.DriveCodes.ContainsKey(drive)) throw new DataImportException("Invalid vehicle group/drive.");
            var key = new VehicleKey(2099, 2099, "FICTIONAL", "ORIGINAL", id);
            var source = new Dictionary<string, string> { ["provenance"] = "Original fictional portfolio fixture" };
            var value = Amount(v, "value");
            values.Add(key, new(key, Text(v, "name"), drive, value, source));
            catalog.Add(id, new(id, group, "סטודיו אוריון — יצרן פיקטיבי", Text(v, "name"), Text(v, "name"), policy.DriveCodes[drive], 0,
                Amount(v, "cost"), Amount(v, "participation"), value, key, source));
        }
        var occupations = raw.GetProperty("occupations").EnumerateArray().Select(o => new OccupationPolicy(Text(o, "name"), Text(o, "group"),
            o.GetProperty("mandatory").GetBoolean(), !o.GetProperty("mandatory").GetBoolean(), Amount(o, "allowance"), new Dictionary<string, string>())).ToDictionary(o => o.Name);
        string Label(string? id) => id is null ? "ויתור - הדגמה" : $"{catalog[id].CommercialName} (2099)";
        var employees = raw.GetProperty("employees").EnumerateArray().Select(e => new SampleEmployee(Text(e, "id"), Text(e, "name"), Text(e, "occupation"),
            occupations[Text(e, "occupation")].Group, Amount(e, "base"), Amount(e, "commission"), Amount(e, "bonus"), .05m, .01m, 2,
            e.GetProperty("children").GetInt32(), Label(e.GetProperty("current").GetString()),
            e.GetProperty("current").GetString() is { } id ? catalog[id].DefaultParticipation : 0, new Dictionary<string, string>())).ToDictionary(e => e.Id);
        var references = raw.GetProperty("references").EnumerateArray().Select(r => new ReferenceCalculation(Text(r, "employeeId"),
            Label(r.GetProperty("current").GetString()), Label(r.GetProperty("alternative").GetString()), true,
            Amount(r, "currentNet"), Amount(r, "alternativeNet"), Amount(r, "difference"), Amount(r, "currentTax"), Amount(r, "alternativeTax"),
            Amount(r, "currentInsurance"), Amount(r, "alternativeInsurance"), Amount(r, "electricGross"), Amount(r, "familyShare"), new Dictionary<string, string>())).ToDictionary(r => r.EmployeeId);
        // Current selections are required even before references are populated by the independent fixture authoring tool.
        foreach (var e in raw.GetProperty("employees").EnumerateArray())
        {
            var id = Text(e, "id");
            if (!references.ContainsKey(id)) throw new DataImportException("Missing independent financial reference: " + id);
            if (references[id].Current != Label(e.GetProperty("current").GetString())) throw new DataImportException("Reference baseline mismatch.");
        }
        var data = new HackathonData { Rules = new(tax, business, policy, 2099, version), VehicleValues = values, Catalog = catalog, Occupations = occupations,
            Employees = employees, References = references, Sheets = [], CsvHeaders = [], RawRules = raw.Clone(),
            Sources = [new("portfolio-demo.json", Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(), bytes.LongLength)],
            Warnings = ["All vehicles, people, policies and financial tables are original fiction. No payroll/legal accuracy is asserted."] };
        foreach (var employee in employees.Values)
            _ = data.Advisor.Calculate(employee, data.ResolvePortfolioSelection(references[employee.Id].Current), employee.CurrentParticipation);
        return data;
    }
}
