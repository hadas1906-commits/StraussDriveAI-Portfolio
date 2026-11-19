using System.Text;
using System.Text.RegularExpressions;
using StraussDrive.Import;
using StraussDrive.Domain;

namespace StraussDrive.Api.Advisor;

/// <summary>Catalog matching only; never creates a vehicle or changes eligibility.</summary>
public static class VehicleSearch
{
    private static string ManufacturerAlias(string manufacturer) => "";
    private static string[] ModelAliases(CatalogVehicle car) => car.CommercialName switch
    {
        "PULSE" => ["פולס"], "ORBIT" => ["אורביט"], "HARBOR" => ["הרבור"],
        "MEADOW" => ["מדו"], "LANTERN" => ["לנטרן"], "SUMMIT" => ["סאמיט"],
        _ => []
    };
    private static string Normalize(string value)
    {
        var text = value.Normalize(NormalizationForm.FormKC).ToUpperInvariant();
        text = Regex.Replace(text, @"(?<=\p{L})(?=\p{N})|(?<=\p{N})(?=\p{L})", " ");
        return Regex.Replace(text, @"[^\p{L}\p{N}]+", " ").Trim();
    }

    public static CatalogVehicle[] Lookup(IEnumerable<CatalogVehicle> catalog, string query)
    {
        var cars = catalog.ToArray();
        var exact = cars.Where(c => c.Id.Equals(query.Trim(), StringComparison.OrdinalIgnoreCase)).ToArray();
        if (exact.Length > 0) return exact;
        // An unknown catalog ID must not accidentally select DEMO-10 when DEMO-1 was requested.
        if (Regex.IsMatch(query.Trim(), @"^DEMO[\s-]*\d+$", RegexOptions.IgnoreCase)) return [];
        var tokens = Normalize(query).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0) return [];
        return cars.Where(c =>
        {
            var words = Normalize(c.Manufacturer + " " + ManufacturerAlias(c.Manufacturer) + " " + c.CommercialName + " " + string.Join(" ", ModelAliases(c))).Split(' ');
            return tokens.All(token => words.Contains(token));
        }).ToArray();
    }

    public static string[] Mentions(IEnumerable<CatalogVehicle> catalog, string text)
    {
        var normalized = " " + Normalize(text) + " ";
        return catalog.Select(car => (Car: car, Position: Position(car)))
            .Where(match => match.Position >= 0).OrderBy(match => match.Position)
            .Select(match => match.Car.Id).Distinct().ToArray();

        int Position(CatalogVehicle car)
        {
            var positions = new List<int> { normalized.IndexOf(" " + Normalize(car.CommercialName) + " ", StringComparison.Ordinal) };
            if (Regex.IsMatch(text, @"(?<![\p{L}\p{N}-])" + Regex.Escape(car.Id) + @"(?![\p{L}\p{N}-])", RegexOptions.IgnoreCase))
                positions.Add(normalized.IndexOf(" " + Normalize(car.Id) + " ", StringComparison.Ordinal));
            foreach (var alias in ModelAliases(car))
            {
                // Hebrew prepositions may be attached: e.g. לפולס, מהאורביט.
                var match = Regex.Match(normalized, @"(?<![\p{L}\p{N}])(?:[ובכלמה]{0,2})" + Regex.Escape(Normalize(alias)) + @"(?![\p{L}\p{N}])");
                if (match.Success) positions.Add(match.Index);
            }
            return positions.Where(position => position >= 0).DefaultIfEmpty(-1).Min();
        }
    }
}
