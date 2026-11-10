using System.Text;

namespace Decont;

/// <summary>
/// Safe, bilingual sample CSV downloads. Headers match the importers exactly.
/// All people and account values below are illustrative and must be replaced.
/// </summary>
public static class ImportExamples
{
    private static readonly Dictionary<string, (string[] Headers, string[][] Examples)> Templates =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["employees"] = (
                ["username", "name", "email", "worker", "managerUsername", "active",
                 "costCenter", "role", "approvalMode", "fixedApproverUsername"],
                [["ana.popescu", "Ana Popescu", "ana.popescu@example.invalid", "EMP001", "manager.existent",
                  "true", "CC100", "Employee", "CostCenter", ""],
                 ["director.example", "Director Exemplu", "director@example.invalid", "EMP002",
                  "", "true", "CC200", "Employee", "Fixed", "manager.existent"]]
            ),
            ["managers"] = (
                ["username", "name", "email", "worker", "managerUsername", "active",
                 "costCenter", "role", "approvalMode", "fixedApproverUsername"],
                [["manager.existent", "Manager Exemplu", "manager@example.invalid", "MGR001",
                  "", "true", "CC100", "Manager", "CostCenter", ""]]
            ),
            ["costCenters"] = (
                ["code", "nameRo", "nameEn", "erpCode", "from", "to", "active"],
                [["CC100", "Vanzari", "Sales", "CC100", "2026-01-01", "", "true"],
                 ["CC200", "Operatiuni", "Operations", "CC200", "2026-01-01", "", "true"]]
            ),
            ["cards"] = (
                ["code", "nameRo", "nameEn", "lastFour", "issuer", "currency",
                 "ownerUsername", "from", "to", "active"],
                [["CARD001", "Card companie exemplu", "Example company card", "4321",
                  "Banca Exemplu", "RON", "ana.popescu", "2026-01-01", "", "true"]]
            ),
            ["cost-center-owners"] = (
                ["costCenterCode", "managerUsername"],
                [["CC100", "manager.existent"], ["CC200", "manager.existent"]]
            ),
            ["projects"] = (
                ["code", "nameRo", "nameEn", "from", "to", "active", "json"],
                [["PROJ001", "Proiect exemplu", "Example project", "2026-01-01", "", "true", "{}"]]
            ),
            ["expenseTypes"] = (
                ["code", "nameRo", "nameEn", "from", "to", "active", "json"],
                [["TRAVEL", "Deplasare", "Travel", "2026-01-01", "", "true", "{}"]]
            ),
            ["organizationUnits"] = (
                ["code", "nameRo", "nameEn", "from", "to", "active", "json"],
                [["DEPT100", "Departament exemplu", "Example department", "2026-01-01", "", "true", "{}"]]
            ),
            ["vat"] = (
                ["code", "nameRo", "nameEn", "from", "to", "active", "json"],
                [["21", "TVA 21", "VAT 21", "2026-01-01", "", "true",
                  "{\"rate\":21,\"salesTaxGroup\":\"VDOM\",\"itemTaxGroup\":\"G21\"}"]]
            ),
            ["bank-statement"] = (
                ["reference", "date", "merchant", "amount", "currency", "settledRon"],
                [["TXN001", "2026-11-01", "Comerciant exemplu", "-125.50", "RON", "-125.50"]]
            ),
            ["generic"] = (
                ["code", "nameRo", "nameEn", "from", "to", "active", "json"],
                [["EXAMPLE", "Exemplu", "Example", "2026-01-01", "", "true", "{}"]]
            ),
        };

    public static (string[] Headers, string[][] Examples) For(string kind) =>
        Templates.TryGetValue(kind, out var template) ? template : Templates["generic"];

    public static IResult Download(string kind)
    {
        var (headers, rows) = For(kind);
        var content = "\uFEFF" + SpreadsheetImports.CsvText([headers, .. rows]) + "\r\n";
        var safe = Templates.ContainsKey(kind) ? kind : "master";
        return Results.File(Encoding.UTF8.GetBytes(content), "text/csv; charset=utf-8",
            safe + "-import-example.csv");
    }
}
