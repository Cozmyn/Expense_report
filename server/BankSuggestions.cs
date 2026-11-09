namespace Decont;

public record BankCandidate(
    Guid Id,
    Guid ReportId,
    string EmployeeId,
    decimal Amount,
    decimal OriginalAmount,
    int Score,
    int DateDistance,
    bool MerchantMatch
);

public record StatementSuggestion(
    CardTransaction Transaction,
    decimal Remaining,
    string Fingerprint,
    bool Ambiguous,
    List<BankCandidate> Candidates
);

public static class BankSuggestions
{
    public static List<StatementSuggestion> Build(
        List<Line> lines,
        List<CardTransaction> transactions,
        List<Match> matches,
        Dictionary<Guid, string> employees
    ) =>
        transactions
            .Select(t =>
            {
                var remaining = t.Amount - matches.Where(m => m.TransactionId == t.Id).Sum(m => m.Amount);
                var candidates = lines
                    .Select(l =>
                    {
                        var d = l.Data();
                        var amount = d.Amount - matches.Where(m => m.LineId == l.Id).Sum(m => m.Amount);
                        var days = Math.Abs(DateOnly.Parse(d.Date).DayNumber - DateOnly.Parse(t.Date).DayNumber);
                        var merchant =
                            !string.IsNullOrWhiteSpace(d.Supplier)
                            && t.Merchant.Contains(d.Supplier, StringComparison.OrdinalIgnoreCase);
                        return new
                        {
                            line = l,
                            data = d,
                            amount,
                            days,
                            merchant,
                        };
                    })
                    .Where(x =>
                        remaining != 0
                        && x.amount != 0
                        && Math.Sign(x.amount) == Math.Sign(remaining)
                        && x.data.CardId == t.CardId
                        && x.data.Currency == t.Currency
                        && x.days <= 7
                    )
                    .Select(x => new BankCandidate(
                        x.line.Id,
                        x.line.ReportId,
                        employees[x.line.ReportId],
                        x.amount,
                        x.data.Amount,
                        (x.amount == remaining ? 60 : 0) + (x.days == 0 ? 20 : 0) + (x.merchant ? 20 : 0),
                        x.days,
                        x.merchant
                    ))
                    .OrderByDescending(x => x.Score)
                    .ThenBy(x => x.DateDistance)
                    .ThenBy(x => x.Id)
                    .Take(5)
                    .ToList();
                return new StatementSuggestion(
                    t,
                    remaining,
                    BankTreatments.Fingerprint(t),
                    candidates.Count > 1 && candidates[0].Score == candidates[1].Score,
                    candidates
                );
            })
            .ToList();
}
