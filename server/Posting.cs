namespace Decont;

public sealed class Posting
{
    public const string EngineVersion = "workbook-posting/2-owner-2026-11-06";

    public static void CheckRow(string[] row)
    {
        Guard.Require(
            row.Length == 24 && row.All(x => x != null && x.Length <= 32767),
            "posting_schema",
            "Un câmp depășește limita Excel sau schema nu are 24 de coloane.",
            "A field exceeds the Excel limit or the schema does not have 24 columns."
        );
        foreach (var column in new[] { 0, 21, 23 })
            if (row[column] != "")
                Guard.Require(
                    DateOnly.TryParseExact(
                        row[column],
                        new[] { "yyyy-MM-dd", "dd.MM.yyyy", "dd/MM/yyyy", "MM/dd/yyyy" },
                        System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.None,
                        out _
                    ),
                    "posting_date",
                    "Corectează data din coloana ERP " + Headers[column] + ". Valoarea nu a fost modificată.",
                    "Correct the date in ERP column " + Headers[column] + ". The value was not changed."
                );
        Guard.Require(
            row[15] == "" || row[15].Length == 3 && row[15].All(char.IsAsciiLetterUpper),
            "posting_currency",
            "Moneda ERP trebuie să fie un cod de trei litere mari sau necompletată, ca în șablon.",
            "ERP currency must be a three-uppercase-letter code or blank as in the template."
        );
        Guard.Require(
            (row[1] is "Ledger" or "Vendor")
                && (row[18] is "" or "Ledger" or "Vendor")
                && !string.IsNullOrWhiteSpace(row[2]),
            "posting_account_type",
            "Tipul contului sau contul ERP este invalid.",
            "ERP account type or account is invalid."
        );
    }

    public static readonly string[] Headers =
    [
        "Date",
        "Account type",
        "Account",
        "Business unit",
        "Cost center",
        "Objective",
        "Site",
        "Local account",
        "Production line",
        "Project",
        "Sub account",
        "Worker",
        "Description",
        "Debit",
        "Credit",
        "Currency",
        "Sales Tax Group",
        "Item sales tax group",
        "Offset Account type",
        "Offset account",
        "Invoice nr",
        "Invoice date",
        "Document nr",
        "Due date",
    ];

    public PostingResult Build(
        Report report,
        User employee,
        List<Master> masters,
        IReadOnlyList<SupplierAccount>? supplierAccounts = null
    )
    {
        var worker = string.IsNullOrWhiteSpace(report.EmployeeWorker) ? employee.Worker : report.EmployeeWorker;
        Guard.Require(
            !string.IsNullOrWhiteSpace(worker),
            "worker",
            "Configurează codul Worker al angajatului.",
            "Configure the expense employee's Worker code."
        );
        List<string[]> rows = [];
        List<Master> used = [];
        List<PostingTrace> traces = [];
        List<PostingReconciliation> reconciliation = [];
        Guard.Require(
            report.Lines.Count(l => !l.Archived) <= 50,
            "receipt_limit",
            "Maximum 50 de cheltuieli active pe decont.",
            "Maximum 50 active expenses per report."
        );
        Master Find(string kind, string code, DateOnly date)
        {
            var m = masters.SingleOrDefault(m => m.Kind == kind && m.Code == code && Rules.Effective(m, date));
            Guard.Require(
                m != null,
                "mapping",
                "Lipsește maparea: " + kind + " / " + code,
                "Mapping missing: " + kind + " / " + code
            );
            used.Add(m!);
            return m!;
        }
        string Required(Master m, string key)
        {
            var value = Rules.Text(m, key);
            Guard.Require(value != "", "mapping", "Completează maparea: " + key, "Complete mapping: " + key);
            return value;
        }
        foreach (var line in report.Lines.Where(l => !l.Archived).OrderBy(l => l.Position))
        {
            try
            {
                decimal Money(decimal amount) =>
                    InvoiceArithmetic.Quantize(amount, 2, line.FinancialRounding ?? InvoiceRounding.HalfAwayFromZero);
                var d =
                    line.CalculatedJson != null && line.CalculationHash == FinancialCalculations.Stamp(line)
                        ? Serialization.Read<ExpenseData>(line.CalculatedJson)
                        : line.Data();
                Guard.Require(
                    !d.Taxes.Any(t => masters.Any(m => m.Id == t.RateId && Rules.Number(m, "rate") is 5 or 19)),
                    "vat_rate",
                    "Cotele TVA 5% și 19% sunt dezactivate.",
                    "VAT rates 5% and 19% are disabled."
                );
                var date = DateOnly.Parse(d.Date);
                var map = Find("accounting", d.Category, date);
                var settings = Find("erpSettings", "default", date);
                Required(settings, "approvalReference");
                var gl = d.Accounting?.MainAccount ?? Required(map, "mainAccount");
                var local = d.Accounting?.LocalAccount ?? Required(map, "localAccount");
                var sub = d.Accounting?.SubAccount ?? Rules.Text(map, "subAccount");
                var car = Rules.Data(map).TryGetProperty("vehicle", out var vehicle) && vehicle.GetBoolean();
                var fieldStates = Rules.FieldStates(d, masters);
                used.AddRange(masters.Where(m => m.Kind == "fieldRules" && Rules.Effective(m, date)));
                var cc =
                    fieldStates["costCenter"] == "NOT_APPLICABLE"
                    || fieldStates["costCenter"] == "OPTIONAL" && string.IsNullOrEmpty(d.CostCenter)
                        ? ""
                        : Required(Find("costCenters", d.CostCenter, date), "erpCode");
                var project =
                    fieldStates["project"] == "NOT_APPLICABLE" || string.IsNullOrEmpty(d.Project)
                        ? ""
                        : Required(Find("projects", d.Project, date), "erpCode");
                var clearing = Required(settings, "clearingAccount");
                var clearingLocal = Required(settings, "clearingLocal");
                var policy = car ? VehicleDeductibility.Policy(masters, date) : null;
                var percent = car ? VehicleDeductibility.Percent(masters, date) : 50m;
                if (policy != null)
                    used.Add(policy);
                var strategy =
                    d.Country != "RO" ? (car ? "FOREIGN_CAR" : "FOREIGN_STANDARD")
                    : !Rules.DomesticVat(d) ? "RO_NO_CUI"
                    : car ? "RO_CUI_CAR"
                    : "RO_CUI_STANDARD";
                var firstRow = rows.Count;
                Guid? sourceRate = null;
                int? rateBps = null;
                string reason = "SOURCE_GROSS";
                decimal NonDed(decimal value) => Money(value * (100 - percent) / 100);
                void Reconcile()
                {
                    var generated = rows.Skip(firstRow).ToList();
                    decimal Parse(string value) =>
                        value == "" ? 0 : decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
                    var gross = Rules.DomesticVat(d)
                        ? generated
                            .Where(x => x[1] == "Ledger" && x[2] == clearing && x[18] == "Vendor")
                            .Sum(x => Parse(x[14]) - Parse(x[13]))
                        : generated.Sum(x => Parse(x[13]) - Parse(x[14]));
                    var factor = d.Amount == 0 ? 0 : line.Final() / d.Amount;
                    var bases = d.Taxes.Sum(t => Money(t.Base * factor));
                    var vat = d.Taxes.Sum(t => Money(t.Vat * factor));
                    Guard.Require(
                        gross == Money(line.Final()),
                        "posting_gross",
                        "Totalul exportat diferă de documentul confirmat.",
                        "Export gross differs from the confirmed document."
                    );
                    if (Rules.DomesticVat(d) && d.VatTreatment == "STANDARD")
                        Guard.Require(
                            bases + vat + line.RoundingRon == Money(line.Final()),
                            "posting_source_total",
                            "Bazele, TVA și rotunjirea confirmată nu recompun totalul. Verifică documentul în Calcul și FX.",
                            "Bases, VAT and confirmed rounding do not reconstruct gross. Review the source in Calculations & FX."
                        );
                    reconciliation.Add(
                        new(
                            line.Id,
                            line.Position,
                            Money(line.Final()),
                            bases,
                            vat,
                            line.RoundingRon,
                            gross,
                            gross - Money(line.Final())
                        )
                    );
                }

                string[] Row(
                    string type,
                    string account,
                    decimal debit = 0,
                    decimal credit = 0,
                    string? localOverride = null,
                    string? subOverride = null,
                    string sales = "",
                    string tax = "",
                    string offsetType = "",
                    string offset = "",
                    bool invoice = false
                )
                {
                    if (debit < 0)
                    {
                        credit -= debit;
                        debit = 0;
                    }
                    if (credit < 0)
                    {
                        debit -= credit;
                        credit = 0;
                    }
                    var r = new string[24];
                    Array.Fill(r, "");
                    foreach (
                        var pair in new[]
                        {
                            (0, "exportDate"),
                            (3, "businessUnit"),
                            (5, "objective"),
                            (6, "site"),
                            (8, "productionLine"),
                            (15, "exportCurrency"),
                            (22, "documentNumber"),
                            (23, "dueDate"),
                        }
                    )
                        r[pair.Item1] = Rules
                            .Text(settings, pair.Item2)
                            .Replace("{{documentDate}}", d.Date)
                            .Replace("{{invoice}}", d.Invoice)
                            .Replace("{{reportNumber}}", report.Number);
                    r[1] = type;
                    r[2] = account;
                    r[4] = type == "Vendor" ? "" : cc;
                    r[7] = type == "Vendor" ? "" : localOverride ?? local;
                    r[9] = project;
                    r[10] = type == "Vendor" ? "" : subOverride ?? sub;
                    r[11] = worker;
                    r[12] = d.Description;
                    r[13] =
                        debit == 0
                            ? ""
                            : Money(debit).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
                    r[14] =
                        credit == 0
                            ? ""
                            : Money(credit).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
                    r[16] = sales;
                    r[17] = tax;
                    r[18] = offsetType;
                    r[19] = offset;
                    if (invoice)
                    {
                        r[20] = d.Invoice;
                        r[21] = d.Date;
                    }
                    CheckRow(r);
                    rows.Add(r);
                    var sequence = rows.Count;
                    var debitValue =
                        r[13] == "" ? 0 : decimal.Parse(r[13], System.Globalization.CultureInfo.InvariantCulture);
                    var creditValue =
                        r[14] == "" ? 0 : decimal.Parse(r[14], System.Globalization.CultureInfo.InvariantCulture);
                    Documents.CheckExcelAmount(debitValue);
                    Documents.CheckExcelAmount(creditValue);
                    PostingLineRecord.Minor(debitValue);
                    PostingLineRecord.Minor(creditValue);
                    var traceId = Serialization.Hash(
                        Serialization.Write(
                            new
                            {
                                report.Id,
                                lineId = line.Id,
                                sequence,
                                strategy,
                                sourceRate,
                                reason,
                                cells = r,
                                calculation = line.CalculationId,
                                fx = line.FxDecisionId,
                                mapping = map.Id,
                                map.Json,
                                percent = car ? (decimal?)percent : null,
                            }
                        )
                    );
                    traces.Add(
                        new(
                            traceId,
                            sequence,
                            line.Id,
                            line.Position,
                            strategy,
                            sourceRate,
                            rateBps,
                            line.CalculationId,
                            line.FxDecisionId,
                            map.Id,
                            map.Json,
                            car ? percent : null,
                            reason,
                            debitValue,
                            creditValue,
                            Serialization.Write(d),
                            report.DepartmentCode,
                            Money(line.Final()),
                            r
                        )
                    );
                    return r;
                }
                if (!Rules.DomesticVat(d))
                {
                    if (car && d.Country != "RO")
                    {
                        var half = NonDed(line.Final());
                        Row("Ledger", gl, half, offsetType: "Ledger", offset: clearing);
                        Row("Ledger", gl, line.Final() - half, subOverride: "", offsetType: "Ledger", offset: clearing);
                    }
                    else
                        Row("Ledger", gl, line.Final(), offsetType: "Ledger", offset: clearing);
                    Reconcile();
                    continue;
                }
                var supplierMaster = Find("suppliers", Rules.Cui(d.Cui), date);
                var vendor = Rules.Text(supplierMaster, "vendorAccount");
                if (vendor == "")
                {
                    var account = (supplierAccounts ?? []).SingleOrDefault(a =>
                        a.Cui == Rules.Cui(d.Cui) && a.From <= date && (a.To == null || a.To >= date)
                    );
                    Guard.Require(
                        account != null,
                        "vendor",
                        "Lipsește contul ERP al furnizorului.",
                        "ERP vendor account is missing."
                    );
                    vendor = account!.Account;
                    used.Add(
                        new Master
                        {
                            Id = account.Id,
                            Kind = "supplierAccounts",
                            Code = account.Cui,
                            From = account.From,
                            To = account.To,
                            Json = Serialization.Write(new { account.Account }),
                        }
                    );
                }
                if (d.VatTreatment != "STANDARD")
                {
                    if (car)
                    {
                        var half = NonDed(line.Final());
                        Row("Ledger", gl, half);
                        Row("Ledger", gl, line.Final() - half, subOverride: "");
                    }
                    else
                        Row("Ledger", gl, line.Final());
                    Row("Vendor", vendor, credit: line.Final(), invoice: true);
                    Row(
                        "Ledger",
                        clearing,
                        credit: line.Final(),
                        localOverride: clearingLocal,
                        subOverride: "",
                        offsetType: "Vendor",
                        offset: vendor
                    );
                    Reconcile();
                    continue;
                }
                decimal bases = 0;
                bool vendorWritten = false;
                var factor = d.TaxCurrency == "RON" ? 1m : line.Final() / d.Amount;
                var codeCount = Rules.VatCodeCount(d, masters, factor);
                // Keep all monetary rows, including a small zero-rate base. Only branch placement ignores it.
                var totalBases = d.Taxes.Sum(t => Money(t.Base * factor));
                used.AddRange(masters.Where(m => m.Kind == "policies" && m.Code == "vat" && Rules.Effective(m, date)));
                foreach (var sourceTax in d.Taxes)
                {
                    var tax = sourceTax with
                    {
                        Base = Money(sourceTax.Base * factor),
                        Vat = Money(sourceTax.Vat * factor),
                    };
                    var rate = masters.Single(m => m.Id == tax.RateId && m.Kind == "vat" && Rules.Effective(m, date));
                    used.Add(rate);
                    sourceRate = rate.Id;
                    rateBps = (int)(Rules.Number(rate, "rate") * 100);
                    reason = "VAT_COMPONENT";
                    var pct = Rules.Number(rate, "rate");
                    Guard.Require(
                        pct >= 0 && pct <= 100 && pct != 5 && pct != 19,
                        "vat",
                        "Cotă TVA ERP neacceptată.",
                        "Unsupported ERP VAT rate."
                    );
                    var sales = Required(rate, "salesTaxGroup");
                    bases += tax.Base;
                    if (!car)
                    {
                        Row("Ledger", gl, tax.Base, sales: sales, tax: Required(rate, "itemTaxGroup"));
                    }
                    else
                    {
                        var half = NonDed(tax.Base);
                        Row(
                            "Ledger",
                            gl,
                            half,
                            sales: sales,
                            tax: pct == 0
                                ? Required(rate, "itemTaxGroup")
                                : VehicleDeductibility.TaxCode(rate, policy, percent, false)
                        );
                        Row(
                            "Ledger",
                            gl,
                            tax.Base - half,
                            subOverride: "",
                            sales: sales,
                            tax: pct == 0
                                ? Required(rate, "itemTaxGroup")
                                : VehicleDeductibility.TaxCode(rate, policy, percent, true)
                        );
                        if (codeCount == 1 && !vendorWritten && (pct > 0 || d.Taxes.Count == 1))
                        {
                            Row("Vendor", vendor, credit: totalBases, invoice: true);
                            vendorWritten = true;
                        }
                        if (pct > 0)
                        {
                            var repair = Rules.Data(map).TryGetProperty("repair", out var rp) && rp.GetBoolean();
                            var nonDedAccount = Required(
                                settings,
                                repair ? "repairNonDedAccount" : "otherCarNonDedAccount"
                            );
                            var nonDedLocal = repair ? Required(settings, "repairNonDedLocal") : local;
                            var halfVat = NonDed(tax.Vat);
                            var quarter = Money(tax.Vat * (100 - percent) * (100 - percent) / 10000);
                            Row("Ledger", nonDedAccount, quarter, localOverride: nonDedLocal);
                            Row(
                                "Ledger",
                                nonDedAccount,
                                halfVat - quarter,
                                localOverride: nonDedLocal,
                                subOverride: ""
                            );
                            Row(
                                "Ledger",
                                Required(settings, "vatAccount"),
                                credit: halfVat,
                                localOverride: Required(settings, "vatLocal")
                            );
                        }
                    }
                }
                sourceRate = null;
                rateBps = null;
                reason = "DOCUMENT_CLEARING";
                if (line.RoundingRon != 0)
                {
                    Row(
                        "Ledger",
                        Required(settings, "roundingAccount"),
                        line.RoundingRon,
                        localOverride: Required(settings, "roundingLocal"),
                        subOverride: ""
                    );
                }
                if (!vendorWritten)
                    Row("Vendor", vendor, credit: bases, invoice: true);
                Row(
                    "Ledger",
                    clearing,
                    credit: line.Final(),
                    localOverride: clearingLocal,
                    subOverride: "",
                    offsetType: "Vendor",
                    offset: vendor
                );
                Reconcile();
            }
            catch (DomainException error)
            {
                throw new DomainException(
                    error.Code,
                    "Bon " + line.Position + ": " + error.Message,
                    "Receipt " + line.Position + ": " + error.En,
                    error.Status,
                    line.Id
                );
            }
        }
        Guard.Require(
            rows.Count > 0 && rows.All(r => r.Length == 24 && !string.IsNullOrWhiteSpace(r[2])),
            "schema",
            "Export ERP invalid.",
            "Invalid ERP export."
        );
        var version = Serialization.Hash(
            Serialization.Write(
                used.DistinctBy(m => m.Id)
                    .OrderBy(m => m.Id)
                    .Select(m => new
                    {
                        m.Id,
                        m.Json,
                        m.From,
                        m.To,
                    })
            )
        );
        return new(Serialization.Hash(EngineVersion + version), rows, traces, reconciliation);
    }
}
