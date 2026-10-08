using TradeTest.Domain;

namespace TradeTest.Application;

/// <summary>Validates source links once, then checks dated withdrawal of corrected facts and documents in constant time.</summary>
internal sealed class ResearchEvidenceTimeline
{
    private readonly Dictionary<string, SourceFact> _facts;
    private readonly Dictionary<string, DateTimeOffset> _withdrawn = new(StringComparer.Ordinal);

    public ResearchEvidenceTimeline(IReadOnlyList<SourceFact> facts, IReadOnlyList<SourceDocument> documents)
    {
        if (facts.Any(f => f is null) || documents.Any(d => d is null)) throw new ArgumentException("Provenance rows cannot be null.");
        var docs = documents.ToDictionary(d => d.DocumentId, StringComparer.Ordinal);
        _facts = facts.ToDictionary(f => f.FactId, StringComparer.Ordinal);
        var documentCorrections = new Dictionary<string, DateTimeOffset>(StringComparer.Ordinal);
        foreach (var document in documents)
        {
            if (string.IsNullOrWhiteSpace(document.DocumentId) || string.IsNullOrWhiteSpace(document.SecurityId) ||
                string.IsNullOrWhiteSpace(document.Publisher) || string.IsNullOrWhiteSpace(document.ParserVersion))
                throw new ArgumentException("Source document metadata is incomplete.");
            var validated = ResearchServices.CreateDocument(document.DocumentId, document.SecurityId, document.SourceUrl,
                document.Publisher, document.PublishedAt, document.FirstKnownAt, document.RetrievedAt,
                document.LicenceId, document.ParserVersion, document.Content, document.SupersedesDocumentId);
            if (document.ContentSha256 != validated.ContentSha256) throw new ArgumentException("Source document hash differs from its content.");
            if (document.SupersedesDocumentId is { } parentId)
            {
                if (!docs.TryGetValue(parentId, out var parent) || document.SecurityId != parent.SecurityId || document.FirstKnownAt <= parent.FirstKnownAt)
                    throw new ArgumentException("Document correction needs a valid earlier predecessor for the same security.");
                if (!documentCorrections.TryAdd(parentId, document.FirstKnownAt)) throw new ArgumentException("Document correction chains cannot fork.");
            }
        }
        foreach (var fact in facts)
        {
            if (string.IsNullOrWhiteSpace(fact.FactId) || string.IsNullOrWhiteSpace(fact.Claim) || !Enum.IsDefined(fact.Verification) ||
                !docs.TryGetValue(fact.DocumentId, out var document) || fact.SecurityId != document.SecurityId || fact.FirstKnownAt < document.FirstKnownAt)
                throw new ArgumentException("Fact provenance, timestamp or verification is invalid.");
            if (documentCorrections.TryGetValue(fact.DocumentId, out var known)) _withdrawn[fact.FactId] = known;
        }
        var factCorrections = new HashSet<string>(StringComparer.Ordinal);
        foreach (var fact in facts.Where(f => f.SupersedesFactId is not null))
        {
            string parentId = fact.SupersedesFactId!;
            if (!_facts.TryGetValue(parentId, out var parent) || fact.SecurityId != parent.SecurityId || fact.FirstKnownAt <= parent.FirstKnownAt || !factCorrections.Add(parentId))
                throw new ArgumentException("Fact correction needs one earlier predecessor for the same security.");
            if (!_withdrawn.TryGetValue(parentId, out var existing) || fact.FirstKnownAt < existing) _withdrawn[parentId] = fact.FirstKnownAt;
        }
    }

    public void Validate(CompanyMetric metric)
    {
        if (!_facts.TryGetValue(metric.SourceFactId, out var fact) || metric.SecurityId != fact.SecurityId ||
            metric.FirstKnownAt < fact.FirstKnownAt || (int)metric.Verification > (int)fact.Verification)
            throw new ArgumentException("Metric provenance, timestamp or verification contradicts its source fact.");
    }

    public bool IsActive(string factId, DateTimeOffset asOf) => !_withdrawn.TryGetValue(factId, out var known) || known > asOf;
}
