using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using TradeTest.Application;
using TradeTest.Domain;

namespace TradeTest.Infrastructure;

public sealed partial class SqliteStore
{
    private static async Task InitializeResearchAsync(SqliteConnection connection, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS documents (
                document_id TEXT PRIMARY KEY, security_id TEXT NOT NULL, source_url TEXT NOT NULL,
                publisher TEXT NOT NULL, published_at TEXT NOT NULL, first_known_at TEXT NOT NULL,
                retrieved_at TEXT NOT NULL, content_hash TEXT NOT NULL, licence_id TEXT NOT NULL,
                parser_version TEXT NOT NULL, supersedes_id TEXT, content TEXT NOT NULL);
            CREATE VIRTUAL TABLE IF NOT EXISTS document_search USING fts5(
                document_id UNINDEXED, security_id UNINDEXED, content);
            CREATE TABLE IF NOT EXISTS facts (
                fact_id TEXT PRIMARY KEY, document_id TEXT NOT NULL REFERENCES documents(document_id),
                security_id TEXT NOT NULL, claim TEXT NOT NULL, first_known_at TEXT NOT NULL,
                verification INTEGER NOT NULL, supersedes_id TEXT);
            CREATE TABLE IF NOT EXISTS metrics (
                security_id TEXT NOT NULL, kind INTEGER NOT NULL, value TEXT NOT NULL,
                first_known_at TEXT NOT NULL, source_fact_id TEXT NOT NULL REFERENCES facts(fact_id),
                verification INTEGER NOT NULL,
                PRIMARY KEY(security_id, kind, first_known_at, source_fact_id));
            CREATE TABLE IF NOT EXISTS research_imports (
                batch_id TEXT PRIMARY KEY, payload_hash TEXT NOT NULL, status INTEGER NOT NULL,
                attempted_at TEXT NOT NULL, document_count INTEGER NOT NULL, fact_count INTEGER NOT NULL,
                metric_count INTEGER NOT NULL, error TEXT, quarantined_json TEXT);
            CREATE INDEX IF NOT EXISTS ix_documents_security_known ON documents(security_id,first_known_at);
            CREATE INDEX IF NOT EXISTS ix_documents_supersedes ON documents(supersedes_id,first_known_at);
            CREATE UNIQUE INDEX IF NOT EXISTS ux_document_correction ON documents(supersedes_id) WHERE supersedes_id IS NOT NULL;
            CREATE INDEX IF NOT EXISTS ix_facts_known ON facts(first_known_at);
            CREATE INDEX IF NOT EXISTS ix_facts_security_known ON facts(security_id,first_known_at);
            CREATE INDEX IF NOT EXISTS ix_facts_supersedes ON facts(supersedes_id,first_known_at);
            CREATE UNIQUE INDEX IF NOT EXISTS ux_fact_correction ON facts(supersedes_id) WHERE supersedes_id IS NOT NULL;
            CREATE INDEX IF NOT EXISTS ix_metrics_known ON metrics(first_known_at);
            CREATE INDEX IF NOT EXISTS ix_metrics_source ON metrics(source_fact_id,first_known_at);
            """;
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task<ResearchImportResult> ImportResearchAsync(ResearchBatch batch, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(batch.BatchId)) throw new ArgumentException("Batch ID is required.", nameof(batch));
        string payload = JsonSerializer.Serialize(new ResearchPayload(batch.Documents, batch.Facts, batch.Metrics),
            ResearchJsonContext.Default.ResearchPayload);
        string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
        await using var connection = await OpenAsync(ct);
        using var transaction = connection.BeginTransaction(deferred: false);
        var prior = await ReadImportAsync(connection, transaction, batch.BatchId, ct);
        if (prior is not null)
        {
            if (prior.PayloadHash != hash) throw new InvalidOperationException("Batch ID reused with a different payload.");
            return new ResearchImportResult(batch.BatchId, hash,
                prior.Status == ResearchImportStatus.Applied ? ResearchImportStatus.AlreadyApplied : prior.Status,
                prior.DocumentCount, prior.FactCount, prior.MetricCount, prior.Error);
        }
        string? error = null;
        transaction.Save("research_rows");
        try
        {
            await WriteResearchRowsAsync(connection, transaction, batch.Documents, batch.Facts, batch.Metrics, ct);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidDataException ||
            ex is SqliteException { SqliteErrorCode: 19 })
        {
            transaction.Rollback("research_rows");
            error = ex.Message;
        }
        var status = error is null ? ResearchImportStatus.Applied : ResearchImportStatus.Quarantined;
        await using var audit = connection.CreateCommand();
        audit.Transaction = transaction;
        audit.CommandText = """
            INSERT INTO research_imports(batch_id,payload_hash,status,attempted_at,document_count,
                fact_count,metric_count,error,quarantined_json)
            VALUES($id,$hash,$status,$at,$docs,$facts,$metrics,$error,$payload)
            """;
        audit.Parameters.AddWithValue("$id", batch.BatchId);
        audit.Parameters.AddWithValue("$hash", hash);
        audit.Parameters.AddWithValue("$status", (int)status);
        audit.Parameters.AddWithValue("$at", DateTimeOffset.UtcNow.ToString("O"));
        audit.Parameters.AddWithValue("$docs", batch.Documents.Count);
        audit.Parameters.AddWithValue("$facts", batch.Facts.Count);
        audit.Parameters.AddWithValue("$metrics", batch.Metrics.Count);
        audit.Parameters.AddWithValue("$error", (object?)error ?? DBNull.Value);
        audit.Parameters.AddWithValue("$payload", error is null ? DBNull.Value : payload);
        await audit.ExecuteNonQueryAsync(ct);
        await transaction.CommitAsync(ct);
        return new ResearchImportResult(batch.BatchId, hash, status,
            batch.Documents.Count, batch.Facts.Count, batch.Metrics.Count, error);
    }

    public Task AddDocumentAsync(SourceDocument document, CancellationToken cancellationToken = default) =>
        WriteSingleAsync([document], [], [], cancellationToken);
    public Task AddFactAsync(SourceFact fact, CancellationToken cancellationToken = default) =>
        WriteSingleAsync([], [fact], [], cancellationToken);
    public Task AddMetricAsync(CompanyMetric metric, CancellationToken cancellationToken = default) =>
        WriteSingleAsync([], [], [metric], cancellationToken);

    private async Task WriteSingleAsync(IReadOnlyList<SourceDocument> docs, IReadOnlyList<SourceFact> facts,
        IReadOnlyList<CompanyMetric> metrics, CancellationToken ct)
    {
        await using var connection = await OpenAsync(ct);
        using var transaction = connection.BeginTransaction(deferred: false);
        await WriteResearchRowsAsync(connection, transaction, docs, facts, metrics, ct);
        await transaction.CommitAsync(ct);
    }

    private static async Task WriteResearchRowsAsync(SqliteConnection connection, SqliteTransaction transaction,
        IReadOnlyList<SourceDocument> docs, IReadOnlyList<SourceFact> facts, IReadOnlyList<CompanyMetric> metrics,
        CancellationToken ct)
    {
        if (docs.Any(d => d is null) || facts.Any(f => f is null) || metrics.Any(m => m is null))
            throw new ArgumentException("Research rows cannot be null.");
        var documents = docs.ToDictionary(d => d.DocumentId, StringComparer.Ordinal);
        var factMap = facts.ToDictionary(f => f.FactId, StringComparer.Ordinal);
        var referencedDocs = new Dictionary<string, (string Security, DateTimeOffset Known)>();
        var referencedFacts = new Dictionary<string, (string Security, DateTimeOffset Known, VerificationState Verification)>();
        await using var lookup = connection.CreateCommand();
        lookup.Transaction = transaction;
        lookup.Parameters.Add("$id", SqliteType.Text);

        async Task<(string Security, DateTimeOffset Known)> GetDocument(string id)
        {
            if (documents.TryGetValue(id, out var doc)) return (doc.SecurityId, doc.FirstKnownAt);
            if (referencedDocs.TryGetValue(id, out var cached)) return cached;
            lookup.CommandText = "SELECT security_id,first_known_at FROM documents WHERE document_id=$id";
            lookup.Parameters["$id"].Value = id;
            await using var reader = await lookup.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct)) throw new ArgumentException($"Document not found: {id}.");
            var value = (reader.GetString(0), ParseTime(reader.GetString(1)));
            referencedDocs.Add(id, value);
            return value;
        }
        async Task<(string Security, DateTimeOffset Known, VerificationState Verification)> GetFact(string id)
        {
            if (factMap.TryGetValue(id, out var fact)) return (fact.SecurityId, fact.FirstKnownAt, fact.Verification);
            if (referencedFacts.TryGetValue(id, out var cached)) return cached;
            lookup.CommandText = "SELECT security_id,first_known_at,verification FROM facts WHERE fact_id=$id";
            lookup.Parameters["$id"].Value = id;
            await using var reader = await lookup.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct)) throw new ArgumentException($"Source fact not found: {id}.");
            var value = (reader.GetString(0), ParseTime(reader.GetString(1)), (VerificationState)reader.GetInt32(2));
            referencedFacts.Add(id, value);
            return value;
        }
        foreach (var doc in docs)
        {
            if (string.IsNullOrWhiteSpace(doc.DocumentId) || string.IsNullOrWhiteSpace(doc.SecurityId) ||
                string.IsNullOrWhiteSpace(doc.Publisher) || string.IsNullOrWhiteSpace(doc.ParserVersion) || doc.SourceUrl is null)
                throw new ArgumentException("Document ID, security, publisher, and parser version are required.");
            var validated = ResearchServices.CreateDocument(doc.DocumentId, doc.SecurityId, doc.SourceUrl,
                doc.Publisher, doc.PublishedAt, doc.FirstKnownAt, doc.RetrievedAt, doc.LicenceId,
                doc.ParserVersion, doc.Content, doc.SupersedesDocumentId);
            if (validated.ContentSha256 != doc.ContentSha256) throw new ArgumentException("Document content hash mismatch.");
            if (doc.SupersedesDocumentId is { } parentId)
            {
                var parent = await GetDocument(parentId);
                if (parent.Security != doc.SecurityId || doc.FirstKnownAt <= parent.Known)
                    throw new ArgumentException("Document correction must follow its predecessor for the same security.");
            }
        }
        foreach (var fact in facts)
        {
            if (string.IsNullOrWhiteSpace(fact.FactId) || string.IsNullOrWhiteSpace(fact.Claim) || !Enum.IsDefined(fact.Verification))
                throw new ArgumentException("Fact ID, claim, and valid verification are required.");
            var doc = await GetDocument(fact.DocumentId);
            if (doc.Security != fact.SecurityId || fact.FirstKnownAt < doc.Known)
                throw new ArgumentException("Fact security/timestamp contradicts its document.");
            if (fact.SupersedesFactId is { } parentId)
            {
                var parent = await GetFact(parentId);
                if (parent.Security != fact.SecurityId || fact.FirstKnownAt <= parent.Known)
                    throw new ArgumentException("Fact correction must follow its predecessor for the same security.");
            }
        }
        foreach (var metric in metrics)
        {
            if (!Enum.IsDefined(metric.Kind) || !Enum.IsDefined(metric.Verification))
                throw new ArgumentException("Metric kind and verification must be valid.");
            var fact = await GetFact(metric.SourceFactId);
            if (fact.Security != metric.SecurityId || metric.FirstKnownAt < fact.Known || (int)metric.Verification > (int)fact.Verification)
                throw new ArgumentException("Metric security, timestamp, or verification contradicts its source fact.");
        }

        await using var insertDoc = Command(connection, transaction, """
            INSERT INTO documents(document_id,security_id,source_url,publisher,published_at,first_known_at,
                retrieved_at,content_hash,licence_id,parser_version,supersedes_id,content)
            VALUES($id,$security,$url,$publisher,$published,$known,$retrieved,$hash,$licence,$parser,$supersedes,$content)
            """, "$id", "$security", "$url", "$publisher", "$published", "$known", "$retrieved", "$hash", "$licence", "$parser", "$supersedes", "$content");
        await using var insertSearch = Command(connection, transaction,
            "INSERT INTO document_search(document_id,security_id,content) VALUES($id,$security,$content)", "$id", "$security", "$content");
        foreach (var doc in docs)
        {
            Values(insertDoc, doc.DocumentId, doc.SecurityId, doc.SourceUrl.ToString(), doc.Publisher,
                Utc(doc.PublishedAt), Utc(doc.FirstKnownAt), Utc(doc.RetrievedAt), doc.ContentSha256,
                doc.LicenceId, doc.ParserVersion, doc.SupersedesDocumentId, doc.Content);
            await insertDoc.ExecuteNonQueryAsync(ct);
            Values(insertSearch, doc.DocumentId, doc.SecurityId, doc.Content);
            await insertSearch.ExecuteNonQueryAsync(ct);
        }
        await using var insertFact = Command(connection, transaction,
            "INSERT INTO facts(fact_id,document_id,security_id,claim,first_known_at,verification,supersedes_id) VALUES($id,$doc,$security,$claim,$known,$verification,$supersedes)",
            "$id", "$doc", "$security", "$claim", "$known", "$verification", "$supersedes");
        foreach (var fact in facts)
        {
            Values(insertFact, fact.FactId, fact.DocumentId, fact.SecurityId, fact.Claim,
                Utc(fact.FirstKnownAt), (int)fact.Verification, fact.SupersedesFactId);
            await insertFact.ExecuteNonQueryAsync(ct);
        }
        await using var insertMetric = Command(connection, transaction,
            "INSERT INTO metrics(security_id,kind,value,first_known_at,source_fact_id,verification) VALUES($security,$kind,$value,$known,$fact,$verification)",
            "$security", "$kind", "$value", "$known", "$fact", "$verification");
        foreach (var metric in metrics)
        {
            Values(insertMetric, metric.SecurityId, (int)metric.Kind, metric.Value.ToString(CultureInfo.InvariantCulture),
                Utc(metric.FirstKnownAt), metric.SourceFactId, (int)metric.Verification);
            await insertMetric.ExecuteNonQueryAsync(ct);
        }
    }

    public async Task<IReadOnlyList<SourceFact>> GetFactsAtAsync(DateTimeOffset asOf,
        string? securityId = null, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT f.fact_id,f.document_id,f.security_id,f.claim,f.first_known_at,f.verification,f.supersedes_id
            FROM facts f WHERE f.first_known_at <= $asof
            AND NOT EXISTS(SELECT 1 FROM documents d WHERE d.supersedes_id=f.document_id AND d.first_known_at <= $asof)
            """ + (securityId is null ? "" : " AND f.security_id=$security") + " ORDER BY f.fact_id";
        command.Parameters.AddWithValue("$asof", Utc(asOf));
        if (securityId is not null) command.Parameters.AddWithValue("$security", securityId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var facts = new List<SourceFact>();
        while (await reader.ReadAsync(cancellationToken))
            facts.Add(new SourceFact(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3),
                ParseTime(reader.GetString(4)), (VerificationState)reader.GetInt32(5), reader.IsDBNull(6) ? null : reader.GetString(6)));
        return facts;
    }

    public async Task<IReadOnlyList<CompanyMetric>> GetMetricsAtAsync(DateTimeOffset asOf,
        string? securityId = null, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT m.security_id,m.kind,m.value,m.first_known_at,m.source_fact_id,m.verification
            FROM metrics m JOIN facts f ON f.fact_id=m.source_fact_id WHERE m.first_known_at <= $asof
            AND NOT EXISTS(SELECT 1 FROM facts c WHERE c.supersedes_id=f.fact_id AND c.first_known_at <= $asof)
            AND NOT EXISTS(SELECT 1 FROM documents d WHERE d.supersedes_id=f.document_id AND d.first_known_at <= $asof)
            """ + (securityId is null ? "" : " AND m.security_id=$security") + " ORDER BY m.security_id,m.kind,m.first_known_at";
        command.Parameters.AddWithValue("$asof", Utc(asOf));
        if (securityId is not null) command.Parameters.AddWithValue("$security", securityId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var metrics = new List<CompanyMetric>();
        while (await reader.ReadAsync(cancellationToken))
            metrics.Add(new CompanyMetric(reader.GetString(0), (CompanyMetricKind)reader.GetInt32(1),
                decimal.Parse(reader.GetString(2), CultureInfo.InvariantCulture), ParseTime(reader.GetString(3)),
                reader.GetString(4), (VerificationState)reader.GetInt32(5)));
        return metrics;
    }

    public async Task<IReadOnlyList<SourceDocument>> SearchDocumentsAsync(string securityId, string query,
        DateTimeOffset asOf, int limit = 8, CancellationToken cancellationToken = default)
    {
        if (limit < 1 || limit > 100) throw new ArgumentOutOfRangeException(nameof(limit));
        var words = Regex.Matches(query, "[\\p{L}\\p{N}]+", RegexOptions.CultureInvariant).Select(m => m.Value).Take(12).ToArray();
        if (words.Length == 0) return [];
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT d.document_id,d.security_id,d.source_url,d.publisher,d.published_at,d.first_known_at,
                d.retrieved_at,d.content_hash,d.licence_id,d.parser_version,d.supersedes_id,d.content
            FROM document_search s JOIN documents d ON d.document_id=s.document_id
            WHERE document_search MATCH $query AND d.security_id=$security AND d.first_known_at <= $asof
            AND NOT EXISTS(SELECT 1 FROM documents c WHERE c.supersedes_id=d.document_id AND c.first_known_at <= $asof)
            ORDER BY bm25(document_search),d.document_id LIMIT $limit
            """;
        command.Parameters.AddWithValue("$query", string.Join(" OR ", words.Select(w => $"\"{w}\"")));
        command.Parameters.AddWithValue("$security", securityId);
        command.Parameters.AddWithValue("$asof", Utc(asOf));
        command.Parameters.AddWithValue("$limit", limit);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var docs = new List<SourceDocument>();
        while (await reader.ReadAsync(cancellationToken))
            docs.Add(new SourceDocument(reader.GetString(0), reader.GetString(1), new Uri(reader.GetString(2)),
                reader.GetString(3), ParseTime(reader.GetString(4)), ParseTime(reader.GetString(5)), ParseTime(reader.GetString(6)),
                reader.GetString(7), reader.GetString(8), reader.GetString(9), reader.IsDBNull(10) ? null : reader.GetString(10), reader.GetString(11)));
        return docs;
    }

    public async Task<ResearchHealthReport> GetResearchHealthAsync(CancellationToken ct = default)
    {
        await using var connection = await OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT (SELECT COUNT(*) FROM documents),(SELECT COUNT(*) FROM facts),(SELECT COUNT(*) FROM metrics),
                (SELECT COUNT(*) FROM facts WHERE verification != 2),
                (SELECT COUNT(*) FROM research_imports WHERE status=0),
                (SELECT COUNT(*) FROM research_imports WHERE status=2),(SELECT MAX(first_known_at) FROM documents)
            """;
        await using var reader = await command.ExecuteReaderAsync(ct);
        await reader.ReadAsync(ct);
        return new ResearchHealthReport(reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2), reader.GetInt32(3),
            reader.GetInt32(4), reader.GetInt32(5), reader.IsDBNull(6) ? null : ParseTime(reader.GetString(6)));
    }

    public async Task<IReadOnlyList<ResearchImportAudit>> GetImportHistoryAsync(int limit = 20, CancellationToken ct = default)
    {
        if (limit is < 1 or > 1000) throw new ArgumentOutOfRangeException(nameof(limit));
        await using var connection = await OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT batch_id,payload_hash,status,attempted_at,document_count,fact_count,metric_count,error FROM research_imports ORDER BY attempted_at DESC,batch_id LIMIT $limit";
        command.Parameters.AddWithValue("$limit", limit);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var items = new List<ResearchImportAudit>();
        while (await reader.ReadAsync(ct)) items.Add(ReadAudit(reader));
        return items;
    }

    private static async Task<ResearchImportAudit?> ReadImportAsync(SqliteConnection connection,
        SqliteTransaction transaction, string id, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT batch_id,payload_hash,status,attempted_at,document_count,fact_count,metric_count,error FROM research_imports WHERE batch_id=$id";
        command.Parameters.AddWithValue("$id", id);
        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? ReadAudit(reader) : null;
    }
    private static ResearchImportAudit ReadAudit(SqliteDataReader r) => new(r.GetString(0), r.GetString(1),
        (ResearchImportStatus)r.GetInt32(2), ParseTime(r.GetString(3)), r.GetInt32(4), r.GetInt32(5), r.GetInt32(6), r.IsDBNull(7) ? null : r.GetString(7));
    private static string Utc(DateTimeOffset at) => at.ToUniversalTime().ToString("O");
    private static SqliteCommand Command(SqliteConnection connection, SqliteTransaction transaction, string sql, params string[] parameters)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        foreach (string parameter in parameters) command.Parameters.Add(new SqliteParameter(parameter, DBNull.Value));
        return command;
    }
    private static void Values(SqliteCommand command, params object?[] values)
    {
        for (int i = 0; i < values.Length; i++) command.Parameters[i].Value = values[i] ?? DBNull.Value;
    }
}
