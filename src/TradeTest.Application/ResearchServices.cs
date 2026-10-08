using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TradeTest.Domain;

namespace TradeTest.Application;

public static class ResearchServices
{
    public static SourceDocument CreateDocument(
        string documentId, string securityId, Uri sourceUrl, string publisher,
        DateTimeOffset publishedAt, DateTimeOffset firstKnownAt, DateTimeOffset retrievedAt,
        string licenceId, string parserVersion, string content, string? supersedesDocumentId = null)
    {
        if (!sourceUrl.IsAbsoluteUri || sourceUrl.Scheme is not ("https" or "http"))
            throw new ArgumentException("An absolute HTTP(S) source URL is required.", nameof(sourceUrl));
        if (firstKnownAt < publishedAt || retrievedAt < firstKnownAt)
            throw new ArgumentException("Source timestamps must follow publication <= first-known <= retrieval.");
        if (string.IsNullOrWhiteSpace(licenceId)) throw new ArgumentException("Licence ID is required.", nameof(licenceId));
        string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))).ToLowerInvariant();
        return new SourceDocument(documentId, securityId, sourceUrl, publisher, publishedAt,
            firstKnownAt, retrievedAt, hash, licenceId, parserVersion, supersedesDocumentId, content);
    }

    public static ResearchPacket BuildPacket(IEnumerable<SourceFact> allFacts, DateTimeOffset asOf)
    {
        var visible = allFacts.Where(f => f.FirstKnownAt <= asOf)
            .OrderBy(f => f.FactId, StringComparer.Ordinal).ToArray();
        var superseded = visible.Where(f => f.SupersedesFactId is not null)
            .Select(f => f.SupersedesFactId!).ToHashSet(StringComparer.Ordinal);
        var facts = visible.Where(f => f.Verification == VerificationState.Verified && !superseded.Contains(f.FactId)).ToArray();
        var canonical = JsonSerializer.Serialize(new { asOf, version = "packet-v1", facts });
        string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
        return new ResearchPacket(asOf, "packet-v1", facts, hash);
    }

    public static IReadOnlyList<SourceFact> CiteCompany(
        ResearchPacket packet, string securityId, string query, int maxResults = 8)
    {
        if (maxResults < 1) throw new ArgumentOutOfRangeException(nameof(maxResults));
        var words = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return packet.Facts.Where(f => f.SecurityId.Equals(securityId, StringComparison.OrdinalIgnoreCase))
            .Select(f => new { Fact = f, Hits = words.Count(w => f.Claim.Contains(w, StringComparison.OrdinalIgnoreCase)) })
            .Where(x => x.Hits > 0)
            .OrderByDescending(x => x.Hits).ThenBy(x => x.Fact.FactId, StringComparer.Ordinal)
            .Take(maxResults).Select(x => x.Fact).ToArray();
    }
}
