using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using TradeTest.Application;
using TradeTest.Domain;

namespace TradeTest.Infrastructure;

public sealed record CompanyResearchSnapshot(string SchemaVersion, string SecurityId, DateTimeOffset AsOf,
    string Query, string PacketHash, IReadOnlyList<SourceFact> Facts, IReadOnlyList<CompanyMetric> Metrics,
    IReadOnlyList<CompanyScore> Companies, IReadOnlyList<DashboardDocument> Documents,
    IReadOnlyList<string> MatchingDocumentIds, string Hash);
public sealed record ResearchHealthSnapshot(ResearchHealthReport Health, IReadOnlyList<ResearchImportAudit> Imports);

public sealed partial class SqliteStore
{
    /// <summary>Pins one read-only SQLite/WAL snapshot. Dispose promptly so writers can reclaim older WAL pages.</summary>
    public async Task<ResearchReadSnapshot> OpenResearchSnapshotAsync(CancellationToken ct = default)
    {
        var connection = await OpenAsync(ct, forceReadOnly: true);
        SqliteTransaction? transaction = null;
        try
        {
            transaction = connection.BeginTransaction(deferred: true);
            await using var pin = connection.CreateCommand();
            pin.Transaction = transaction;
            pin.CommandText = "SELECT COUNT(*) FROM sqlite_schema";
            await pin.ExecuteScalarAsync(ct); // A deferred transaction acquires its snapshot on its first read.
            return new ResearchReadSnapshot(connection, transaction);
        }
        catch
        {
            if (transaction is not null) await transaction.DisposeAsync();
            await connection.DisposeAsync();
            throw;
        }
    }

    public async Task<CompanyResearchSnapshot> ReadCompanySnapshotAsync(string securityId, DateTimeOffset asOf,
        string query = "", CancellationToken ct = default)
    {
        await using var snapshot = await OpenResearchSnapshotAsync(ct);
        return await snapshot.ReadCompanyAsync(securityId, asOf, query, ct);
    }

    public async Task<ResearchHealthSnapshot> ReadResearchHealthSnapshotAsync(int importLimit = 20, CancellationToken ct = default)
    {
        await using var snapshot = await OpenResearchSnapshotAsync(ct);
        return await snapshot.ReadHealthAsync(importLimit, ct);
    }

    public sealed class ResearchReadSnapshot : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly SqliteTransaction _transaction;
        private readonly SemaphoreSlim _gate = new(1);
        private bool _disposed;

        internal ResearchReadSnapshot(SqliteConnection connection, SqliteTransaction transaction)
        {
            _connection = connection;
            _transaction = transaction;
        }

        public Task<IReadOnlyList<SourceFact>> GetFactsAtAsync(DateTimeOffset asOf, string? securityId = null,
            CancellationToken ct = default) => ReadAsync(() => ReadFactsAtAsync(_connection, _transaction, asOf, securityId, ct), ct);

        public Task<IReadOnlyList<CompanyMetric>> GetMetricsAtAsync(DateTimeOffset asOf, string? securityId = null,
            CancellationToken ct = default) => ReadAsync(() => ReadMetricsAtAsync(_connection, _transaction, asOf, securityId, ct), ct);

        public Task<IReadOnlyList<SourceDocument>> SearchDocumentsAsync(string securityId, string query,
            DateTimeOffset asOf, int limit = 8, CancellationToken ct = default) =>
            ReadAsync(() => SqliteStore.SearchDocumentsAsync(_connection, _transaction, securityId, query, asOf, limit, ct), ct);

        public Task<ResearchHealthSnapshot> ReadHealthAsync(int importLimit = 20, CancellationToken ct = default) =>
            ReadAsync(async () => new ResearchHealthSnapshot(await ReadResearchHealthAsync(_connection, _transaction, ct),
                await ReadImportHistoryAsync(_connection, _transaction, importLimit, ct)), ct);

        public Task<CompanyResearchSnapshot> ReadCompanyAsync(string securityId, DateTimeOffset asOf,
            string query = "", CancellationToken ct = default) => ReadAsync(async () =>
        {
            if (string.IsNullOrWhiteSpace(securityId) || securityId.Length > 128 || query.Length > 500)
                throw new ArgumentException("Use a security ID up to 128 characters and a query up to 500 characters.");
            asOf = asOf.ToUniversalTime();
            var packet = ResearchServices.BuildPacket(await ReadFactsAtAsync(_connection, _transaction, asOf, securityId, ct), asOf);
            var factMap = packet.Facts.ToDictionary(f => f.FactId, StringComparer.Ordinal);
            var metrics = (await ReadMetricsAtAsync(_connection, _transaction, asOf, securityId, ct))
                .Where(m => m.Verification == VerificationState.Verified)
                .GroupBy(m => m.Kind).Select(g => g.OrderByDescending(m => m.FirstKnownAt)
                    .ThenBy(m => m.SourceFactId, StringComparer.Ordinal).First())
                .OrderBy(m => m.Kind).ToArray();
            foreach (var metric in metrics)
                if (!factMap.TryGetValue(metric.SourceFactId, out var fact) || metric.FirstKnownAt < fact.FirstKnownAt ||
                    metric.SecurityId != fact.SecurityId || !Enum.IsDefined(metric.Kind))
                    throw new InvalidDataException("Metric does not match active verified evidence in this database snapshot.");
            var matches = await SqliteStore.SearchDocumentsAsync(_connection, _transaction, securityId, query, asOf, 8, ct);
            var cited = await ReadDocumentsByIdsAsync(_connection, _transaction,
                packet.Facts.Select(f => f.DocumentId).Distinct(StringComparer.Ordinal).ToArray(), ct);
            var documents = cited.Concat(matches).DistinctBy(d => d.DocumentId, StringComparer.Ordinal)
                .OrderBy(d => d.DocumentId, StringComparer.Ordinal).ToArray();
            var documentMap = documents.ToDictionary(d => d.DocumentId, StringComparer.Ordinal);
            foreach (var document in documents)
            {
                SourceDocument validated;
                try
                {
                    validated = ResearchServices.CreateDocument(document.DocumentId, document.SecurityId, document.SourceUrl,
                        document.Publisher, document.PublishedAt, document.FirstKnownAt, document.RetrievedAt,
                        document.LicenceId, document.ParserVersion, document.Content, document.SupersedesDocumentId);
                }
                catch (ArgumentException ex) { throw new InvalidDataException("Source document metadata failed integrity checks.", ex); }
                if (document.SecurityId != securityId || document.FirstKnownAt > asOf || validated.ContentSha256 != document.ContentSha256)
                    throw new InvalidDataException("Source document failed dated identity or content integrity checks.");
            }
            foreach (var fact in packet.Facts)
                if (!documentMap.TryGetValue(fact.DocumentId, out var document) || fact.FirstKnownAt < document.FirstKnownAt)
                    throw new InvalidDataException("Fact does not match its source document in this database snapshot.");

            var report = new CompanyResearchSnapshot("tradetest-company-v1", securityId, asOf, query, packet.Hash,
                packet.Facts, metrics, new LongTermRanker().Rank(metrics, asOf),
                documents.Select(d => new DashboardDocument(d.DocumentId, d.SecurityId, d.Publisher, d.SourceUrl,
                    d.PublishedAt, d.FirstKnownAt, d.LicenceId, d.ContentSha256)).ToArray(),
                matches.Select(d => d.DocumentId).ToArray(), "");
            string hash = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(report, ResearchJson.OutputOptions)))
                .ToLowerInvariant();
            return report with { Hash = hash };
        }, ct);

        private async Task<T> ReadAsync<T>(Func<Task<T>> read, CancellationToken ct)
        {
            await _gate.WaitAsync(ct);
            try
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                return await read();
            }
            finally { _gate.Release(); }
        }

        public async ValueTask DisposeAsync()
        {
            await _gate.WaitAsync();
            try
            {
                if (_disposed) return;
                _disposed = true;
                await _transaction.DisposeAsync();
                await _connection.DisposeAsync();
            }
            finally { _gate.Release(); }
        }
    }

    private static async Task<IReadOnlyList<SourceDocument>> ReadDocumentsByIdsAsync(SqliteConnection connection,
        SqliteTransaction transaction, IReadOnlyList<string> ids, CancellationToken ct)
    {
        var documents = new List<SourceDocument>();
        foreach (var chunk in ids.Chunk(500))
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            var names = chunk.Select((id, i) => "$id" + i).ToArray();
            command.CommandText = """
                SELECT document_id,security_id,source_url,publisher,published_at,first_known_at,
                    retrieved_at,content_hash,licence_id,parser_version,supersedes_id,content FROM documents
                """ + " WHERE document_id IN (" + string.Join(',', names) + ")";
            for (int i = 0; i < chunk.Length; i++) command.Parameters.AddWithValue(names[i], chunk[i]);
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct)) documents.Add(ReadSourceDocument(reader));
        }
        return documents;
    }

    private static SourceDocument ReadSourceDocument(SqliteDataReader reader) => new(reader.GetString(0), reader.GetString(1),
        new Uri(reader.GetString(2)), reader.GetString(3), ParseTime(reader.GetString(4)), ParseTime(reader.GetString(5)),
        ParseTime(reader.GetString(6)), reader.GetString(7), reader.GetString(8), reader.GetString(9),
        reader.IsDBNull(10) ? null : reader.GetString(10), reader.GetString(11));
}
