using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;

namespace Decont;

public class VerifiedVatComponent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CalculationId { get; set; }
    public Guid LineId { get; set; }
    public int RateBps { get; set; }
    public string Json { get; set; } = "{}";
    public string PolicyReference { get; set; } = "";
}

public record FinancialDifference(string Field, string? Before, string? After);

public static class CalculationHistory
{
    static Dictionary<string, string?> Flatten(JsonNode? node, string path = "")
    {
        var values = new Dictionary<string, string?>();
        if (node is JsonObject obj)
        {
            foreach (var p in obj)
            foreach (var item in Flatten(p.Value, path == "" ? p.Key : path + "." + p.Key))
                values[item.Key] = item.Value;
        }
        else if (node is JsonArray array)
        {
            for (int i = 0; i < array.Count; i++)
                foreach (var item in Flatten(array[i], path + "[" + i + "]"))
                    values[item.Key] = item.Value;
        }
        else
            values[path] = node?.ToJsonString();
        return values;
    }

    public static List<FinancialDifference> Differences(string before, string after)
    {
        var a = Flatten(JsonNode.Parse(before));
        var b = Flatten(JsonNode.Parse(after));
        return a
            .Keys.Union(b.Keys)
            .Order()
            .Where(k => a.GetValueOrDefault(k) != b.GetValueOrDefault(k))
            .Select(k => new FinancialDifference(k, a.GetValueOrDefault(k), b.GetValueOrDefault(k)))
            .ToList();
    }

    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet(
            "/reports/{id:guid}/lines/{line:guid}/calculation-history",
            async (Guid id, Guid line, int? page, Guid? before, Guid? after, Db db, Reports svc, HttpContext c) =>
            {
                var report = await svc.Get(id, c.User);
                Guard.Require(
                    report.Lines.Any(l => l.Id == line),
                    "not_found",
                    "Bonul nu există.",
                    "Receipt not found.",
                    404
                );
                if (before != null && after != null)
                {
                    var a = await db.Calculations.SingleOrDefaultAsync(x =>
                        x.Id == before && x.LineId == line && x.ReportId == id
                    );
                    var b = await db.Calculations.SingleOrDefaultAsync(x =>
                        x.Id == after && x.LineId == line && x.ReportId == id
                    );
                    Guard.Require(
                        a != null && b != null,
                        "not_found",
                        "Versiunile nu există pentru acest bon.",
                        "Versions do not belong to this receipt.",
                        404
                    );
                    return (object)
                        new
                        {
                            before = a!.Id,
                            after = b!.Id,
                            differences = Differences(a.Result, b.Result),
                        };
                }
                var offset = Math.Clamp(page ?? 1, 1, 100000) - 1;
                var versions = await db
                    .Calculations.Where(x => x.ReportId == id && x.LineId == line)
                    .OrderByDescending(x => x.At)
                    .ThenByDescending(x => x.Id)
                    .Skip(offset * 25)
                    .Take(25)
                    .ToListAsync();
                var result = new List<object>();
                foreach (var v in versions)
                {
                    var prior = await db
                        .Calculations.Where(x => x.LineId == line && x.At < v.At)
                        .OrderByDescending(x => x.At)
                        .FirstOrDefaultAsync();
                    result.Add(
                        new
                        {
                            v.Id,
                            v.At,
                            v.Actor,
                            v.OutputHash,
                            v.Policy,
                            summary = Serialization.Read<FinancialPreview>(
                                System
                                    .Text.Json.JsonDocument.Parse(v.Result)
                                    .RootElement.GetProperty("preview")
                                    .GetRawText()
                            ),
                            differences = prior == null ? [] : Differences(prior.Result, v.Result),
                        }
                    );
                }
                return new
                {
                    page = offset + 1,
                    versions = result,
                    total = await db.Calculations.CountAsync(x => x.ReportId == id && x.LineId == line),
                };
            }
        );
    }
}
