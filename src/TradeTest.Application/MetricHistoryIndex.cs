using TradeTest.Domain;

namespace TradeTest.Application;

/// <summary>Immutable metric histories, compiled once for many dated ranking queries.</summary>
public sealed class MetricHistoryIndex
{
    private readonly CompanyHistory[] _companies;
    private readonly ResearchEvidenceTimeline? _evidence;
    public bool ProvenanceValidated => _evidence is not null;

    public MetricHistoryIndex(IEnumerable<CompanyMetric> metrics,
        IReadOnlyList<SourceFact>? facts = null, IReadOnlyList<SourceDocument>? documents = null)
    {
        var rows = metrics.ToArray();
        if (rows.Any(m => m is null || string.IsNullOrWhiteSpace(m.SecurityId) || string.IsNullOrWhiteSpace(m.SourceFactId) ||
            !Enum.IsDefined(m.Kind) || !Enum.IsDefined(m.Verification)))
            throw new ArgumentException("Metrics need security/fact IDs and valid kinds/verification.");
        if (facts is not null || documents is not null)
        {
            if (facts is null || documents is null) throw new ArgumentException("Provide both fact and document histories for provenance validation.");
            _evidence = new ResearchEvidenceTimeline(facts, documents);
            foreach (var metric in rows) _evidence.Validate(metric);
        }
        _companies = rows.Where(m => m.Verification == VerificationState.Verified)
            .GroupBy(m => m.SecurityId, StringComparer.OrdinalIgnoreCase)
            .Select(company => new CompanyHistory(company.Key, company.GroupBy(m => m.Kind)
                .ToDictionary(g => g.Key, g => g.OrderBy(m => m.FirstKnownAt)
                    .ThenByDescending(m => m.SourceFactId, StringComparer.Ordinal).ToArray()))).ToArray();
        if (_companies.Any(c => c.ByKind.Values.Any(series => series.GroupBy(m => (m.FirstKnownAt, m.SourceFactId)).Any(g => g.Count() > 1))))
            throw new ArgumentException("Duplicate dated metrics for the same security, kind and source fact.");
    }

    public IReadOnlyList<CompanyScore> RankAt(DateTimeOffset asOf)
    {
        var scores = new List<CompanyScore>(_companies.Length);
        foreach (var company in _companies)
        {
            var latest = new Dictionary<CompanyMetricKind, CompanyMetric>(company.ByKind.Count);
            foreach (var (kind, series) in company.ByKind)
            {
                int lo = 0, hi = series.Length;
                while (lo < hi)
                {
                    int middle = lo + (hi - lo) / 2;
                    if (series[middle].FirstKnownAt <= asOf) lo = middle + 1;
                    else hi = middle;
                }
                for (int index = lo - 1; index >= 0; index--)
                {
                    var metric = series[index];
                    if (_evidence?.IsActive(metric.SourceFactId, asOf) == false) continue;
                    // Equal-timestamp candidates must retain the original ranker's ordinal fact-ID tie rule.
                    int first = index;
                    while (first > 0 && series[first - 1].FirstKnownAt == metric.FirstKnownAt) first--;
                    for (int candidate = first; candidate <= index; candidate++)
                    {
                        var item = series[candidate];
                        if (_evidence?.IsActive(item.SourceFactId, asOf) != false &&
                            string.CompareOrdinal(item.SourceFactId, metric.SourceFactId) < 0) metric = item;
                    }
                    latest.Add(kind, metric);
                    break;
                }
            }
            var score = LongTermRanker.ScoreCompany(company.SecurityId, latest, asOf);
            if (score is not null) scores.Add(score);
        }
        return LongTermRanker.Sort(scores);
    }

    private sealed record CompanyHistory(string SecurityId, Dictionary<CompanyMetricKind, CompanyMetric[]> ByKind);
}
