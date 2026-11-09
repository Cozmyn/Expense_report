using Microsoft.EntityFrameworkCore;

namespace Decont;

// A dated master is included in the existing financial context and approval hash.
// The default is the owner's 50%; payment method never selects this percentage.
public static class VehicleDeductibility
{
    public const string Code = "vehicleDeductibility";

    public static bool IsCode(string code)
    {
        var normalized = code.Trim();
        return normalized == Code || normalized.StartsWith(Code + ".", StringComparison.Ordinal);
    }

    public static Master? Policy(IEnumerable<Master> masters, DateOnly date) =>
        masters
            .Where(m => m.Kind == "policies" && IsCode(m.Code) && Rules.Effective(m, date))
            .OrderByDescending(m => m.From)
            .ThenByDescending(m => Rules.Number(m, "version"))
            .FirstOrDefault();

    public static decimal Percent(IEnumerable<Master> masters, DateOnly date)
    {
        var policy = Policy(masters, date);
        var percent = policy == null ? 50m : Rules.Number(policy, "percentage");
        Guard.Require(
            percent is >= 0 and <= 100 && decimal.Round(percent, 2) == percent,
            "vehicle_percentage",
            "Procentul deductibil auto trebuie să fie între 0 și 100, cu maximum două zecimale.",
            "Vehicle deductible percentage must be between 0 and 100, with at most two decimals."
        );
        return percent;
    }

    public static string TaxCode(Master rate, Master? policy, decimal percent, bool deductible)
    {
        if (percent == 50)
        {
            var original = Rules.Text(rate, deductible ? "deductible50Code" : "nonDeductible50Code");
            Guard.Require(
                original != "",
                "vehicle_tax_code",
                "Completează codurile TVA auto de 50% în nomenclatorul TVA.",
                "Configure 50% vehicle VAT codes in VAT master data."
            );
            return original;
        }
        var data = policy == null ? default : Rules.Data(policy);
        var key =
            (deductible ? "deductible" : "nonDeductible")
            + Rules.Number(rate, "rate").ToString(System.Globalization.CultureInfo.InvariantCulture)
            + "Code";
        var code =
            data.ValueKind == System.Text.Json.JsonValueKind.Object
            && data.TryGetProperty(key, out var value)
            && value.ValueKind == System.Text.Json.JsonValueKind.String
                ? value.GetString() ?? ""
                : "";
        Guard.Require(
            code != "",
            "vehicle_tax_code",
            "Completează codurile TVA auto pentru procentul ales în Configurare Finance; codurile de 50% nu sunt reutilizate automat.",
            "Configure vehicle VAT codes for the selected percentage in Finance settings; 50% codes are not automatically reused."
        );
        return code;
    }

    public static void Map(RouteGroupBuilder api)
    {
        var f = api.MapGroup("/finance/vehicle-deductibility").RequireAuthorization(p => p.RequireRole("Finance"));
        f.MapGet(
            "",
            async (Db db) =>
                new
                {
                    defaultPercentage = 50,
                    versions = (await db.Masters.Where(m => m.Kind == "policies").ToListAsync())
                        .Where(m => IsCode(m.Code))
                        .OrderByDescending(m => m.From)
                        .ThenByDescending(m => Rules.Number(m, "version")),
                }
        );
        f.MapPost(
            "",
            async (VehiclePolicyInput i, Db db, Reports svc, HttpContext c) =>
                await Api.Atomic(
                    db,
                    c,
                    i,
                    async () =>
                    {
                        Guard.Reason(i.Reason);
                        Guard.Require(
                            i.Percentage is >= 0 and <= 100 && decimal.Round(i.Percentage, 2) == i.Percentage,
                            "vehicle_percentage",
                            "Introdu un procent între 0 și 100, cu maximum două zecimale.",
                            "Enter a percentage between 0 and 100, with at most two decimals."
                        );
                        var codes = i.TaxCodes ?? new();
                        Guard.Require(
                            codes.All(x =>
                                new[]
                                {
                                    "deductible11Code",
                                    "nonDeductible11Code",
                                    "deductible21Code",
                                    "nonDeductible21Code",
                                }.Contains(x.Key)
                                && x.Value != null
                                && x.Value.Length <= 80
                            ),
                            "vehicle_codes",
                            "Coduri TVA auto invalide.",
                            "Invalid vehicle VAT codes."
                        );
                        var existing = (await db.Masters.Where(m => m.Kind == "policies").ToListAsync()).Where(m =>
                            IsCode(m.Code)
                        );
                        var data = new Dictionary<string, object>
                        {
                            { "percentage", i.Percentage },
                            { "version", existing.Select(m => Rules.Number(m, "version")).DefaultIfEmpty().Max() + 1 },
                        };
                        foreach (var code in codes)
                            data[code.Key] = code.Value.Trim();
                        var master = new Master
                        {
                            Kind = "policies",
                            Code = Code + "." + Guid.NewGuid().ToString("N"),
                            NameRo = "Deductibilitate auto",
                            NameEn = "Vehicle deductibility",
                            From = i.From,
                            Json = Serialization.Write(data),
                        };
                        db.Masters.Add(master);
                        svc.Log(
                            c.User,
                            null,
                            master.Id.ToString(),
                            "VehicleDeductibilityVersioned",
                            after: master,
                            reason: i.Reason
                        );
                        return master;
                    }
                )
        );
    }
}

public record VehiclePolicyInput(
    decimal Percentage,
    DateOnly From,
    string Reason,
    Dictionary<string, string>? TaxCodes = null
);
