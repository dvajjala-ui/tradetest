using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using TradeTest.Application;
using TradeTest.Domain;

namespace TradeTest.Infrastructure;

public sealed record StoredEvent(int Version, SimulationEvent Event, string PreviousHash, string Hash);

/// <summary>Local research and append-only simulation journal. No credential or order API access.</summary>
public sealed class SqliteStore(string databasePath)
{
    private readonly string _connectionString = new SqliteConnectionStringBuilder
    {
        DataSource = databasePath,
        Mode = SqliteOpenMode.ReadWriteCreate
    }.ToString();

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        string? directory = Path.GetDirectoryName(Path.GetFullPath(databasePath));
        if (directory is not null) Directory.CreateDirectory(directory);
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            PRAGMA journal_mode=WAL;
            CREATE TABLE IF NOT EXISTS events (
                stream TEXT NOT NULL, version INTEGER NOT NULL, type TEXT NOT NULL,
                occurred_at TEXT NOT NULL, json TEXT NOT NULL, previous_hash TEXT NOT NULL,
                event_hash TEXT NOT NULL, PRIMARY KEY(stream, version));
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
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task AppendEventsAsync(string stream, int expectedVersion,
        IReadOnlyList<SimulationEvent> events, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(stream)) throw new ArgumentException("Stream ID is required.", nameof(stream));
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var head = connection.CreateCommand();
        head.Transaction = (SqliteTransaction)transaction;
        head.CommandText = "SELECT version, event_hash FROM events WHERE stream=$stream ORDER BY version DESC LIMIT 1";
        head.Parameters.AddWithValue("$stream", stream);
        await using var reader = await head.ExecuteReaderAsync(cancellationToken);
        int currentVersion = 0;
        string previousHash = new('0', 64);
        if (await reader.ReadAsync(cancellationToken))
        {
            currentVersion = reader.GetInt32(0);
            previousHash = reader.GetString(1);
        }
        await reader.DisposeAsync();
        if (currentVersion != expectedVersion)
            throw new InvalidOperationException($"Stream version mismatch: expected {expectedVersion}, found {currentVersion}.");

        foreach (var item in events)
        {
            int version = ++currentVersion;
            string hash = Hash(stream, version, item, previousHash);
            await using var insert = connection.CreateCommand();
            insert.Transaction = (SqliteTransaction)transaction;
            insert.CommandText = """
                INSERT INTO events(stream,version,type,occurred_at,json,previous_hash,event_hash)
                VALUES($stream,$version,$type,$at,$json,$previous,$hash)
                """;
            insert.Parameters.AddWithValue("$stream", stream);
            insert.Parameters.AddWithValue("$version", version);
            insert.Parameters.AddWithValue("$type", item.Type);
            insert.Parameters.AddWithValue("$at", item.OccurredAt.ToUniversalTime().ToString("O"));
            insert.Parameters.AddWithValue("$json", item.Json);
            insert.Parameters.AddWithValue("$previous", previousHash);
            insert.Parameters.AddWithValue("$hash", hash);
            await insert.ExecuteNonQueryAsync(cancellationToken);
            previousHash = hash;
        }
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<StoredEvent>> ReadEventsAsync(string stream, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT version,type,occurred_at,json,previous_hash,event_hash FROM events WHERE stream=$stream ORDER BY version";
        command.Parameters.AddWithValue("$stream", stream);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var events = new List<StoredEvent>();
        string previous = new('0', 64);
        while (await reader.ReadAsync(cancellationToken))
        {
            int version = reader.GetInt32(0);
            var item = new SimulationEvent(reader.GetString(1), ParseTime(reader.GetString(2)), reader.GetString(3));
            string storedPrevious = reader.GetString(4), storedHash = reader.GetString(5);
            if (version != events.Count + 1 || storedPrevious != previous || Hash(stream, version, item, previous) != storedHash)
                throw new InvalidDataException($"Event hash chain invalid at stream {stream}, version {version}.");
            events.Add(new StoredEvent(version, item, previous, storedHash));
            previous = storedHash;
        }
        return events;
    }

    public async Task AddDocumentAsync(SourceDocument document, CancellationToken cancellationToken = default)
    {
        string computedHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(document.Content))).ToLowerInvariant();
        if (computedHash != document.ContentSha256) throw new ArgumentException("Document content hash mismatch.", nameof(document));
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var insert = connection.CreateCommand();
        insert.Transaction = (SqliteTransaction)transaction;
        insert.CommandText = """
            INSERT INTO documents(document_id,security_id,source_url,publisher,published_at,first_known_at,
                retrieved_at,content_hash,licence_id,parser_version,supersedes_id,content)
            VALUES($id,$security,$url,$publisher,$published,$known,$retrieved,$hash,$licence,$parser,$supersedes,$content)
            """;
        insert.Parameters.AddWithValue("$id", document.DocumentId);
        insert.Parameters.AddWithValue("$security", document.SecurityId);
        insert.Parameters.AddWithValue("$url", document.SourceUrl.ToString());
        insert.Parameters.AddWithValue("$publisher", document.Publisher);
        insert.Parameters.AddWithValue("$published", document.PublishedAt.ToUniversalTime().ToString("O"));
        insert.Parameters.AddWithValue("$known", document.FirstKnownAt.ToUniversalTime().ToString("O"));
        insert.Parameters.AddWithValue("$retrieved", document.RetrievedAt.ToUniversalTime().ToString("O"));
        insert.Parameters.AddWithValue("$hash", document.ContentSha256);
        insert.Parameters.AddWithValue("$licence", document.LicenceId);
        insert.Parameters.AddWithValue("$parser", document.ParserVersion);
        insert.Parameters.AddWithValue("$supersedes", (object?)document.SupersedesDocumentId ?? DBNull.Value);
        insert.Parameters.AddWithValue("$content", document.Content);
        await insert.ExecuteNonQueryAsync(cancellationToken);
        await using var search = connection.CreateCommand();
        search.Transaction = (SqliteTransaction)transaction;
        search.CommandText = "INSERT INTO document_search(document_id,security_id,content) VALUES($id,$security,$content)";
        search.Parameters.AddWithValue("$id", document.DocumentId);
        search.Parameters.AddWithValue("$security", document.SecurityId);
        search.Parameters.AddWithValue("$content", document.Content);
        await search.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task AddFactAsync(SourceFact fact, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var check = connection.CreateCommand();
        check.CommandText = "SELECT security_id,first_known_at FROM documents WHERE document_id=$id";
        check.Parameters.AddWithValue("$id", fact.DocumentId);
        await using var reader = await check.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) throw new ArgumentException("Fact document not found.", nameof(fact));
        if (reader.GetString(0) != fact.SecurityId || fact.FirstKnownAt < ParseTime(reader.GetString(1)))
            throw new ArgumentException("Fact security/timestamp contradicts its document.", nameof(fact));
        await reader.DisposeAsync();
        await using var insert = connection.CreateCommand();
        insert.CommandText = """
            INSERT INTO facts(fact_id,document_id,security_id,claim,first_known_at,verification,supersedes_id)
            VALUES($id,$document,$security,$claim,$known,$verification,$supersedes)
            """;
        insert.Parameters.AddWithValue("$id", fact.FactId);
        insert.Parameters.AddWithValue("$document", fact.DocumentId);
        insert.Parameters.AddWithValue("$security", fact.SecurityId);
        insert.Parameters.AddWithValue("$claim", fact.Claim);
        insert.Parameters.AddWithValue("$known", fact.FirstKnownAt.ToUniversalTime().ToString("O"));
        insert.Parameters.AddWithValue("$verification", (int)fact.Verification);
        insert.Parameters.AddWithValue("$supersedes", (object?)fact.SupersedesFactId ?? DBNull.Value);
        await insert.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task AddMetricAsync(CompanyMetric metric, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var check = connection.CreateCommand();
        check.CommandText = "SELECT security_id,first_known_at,verification FROM facts WHERE fact_id=$id";
        check.Parameters.AddWithValue("$id", metric.SourceFactId);
        await using var reader = await check.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) throw new ArgumentException("Metric source fact not found.", nameof(metric));
        if (reader.GetString(0) != metric.SecurityId || metric.FirstKnownAt < ParseTime(reader.GetString(1)))
            throw new ArgumentException("Metric security/timestamp contradicts its fact.", nameof(metric));
        if ((int)metric.Verification > reader.GetInt32(2))
            throw new ArgumentException("Metric cannot have stronger verification than its source fact.", nameof(metric));
        await reader.DisposeAsync();
        await using var insert = connection.CreateCommand();
        insert.CommandText = """
            INSERT INTO metrics(security_id,kind,value,first_known_at,source_fact_id,verification)
            VALUES($security,$kind,$value,$known,$fact,$verification)
            """;
        insert.Parameters.AddWithValue("$security", metric.SecurityId);
        insert.Parameters.AddWithValue("$kind", (int)metric.Kind);
        insert.Parameters.AddWithValue("$value", metric.Value.ToString(CultureInfo.InvariantCulture));
        insert.Parameters.AddWithValue("$known", metric.FirstKnownAt.ToUniversalTime().ToString("O"));
        insert.Parameters.AddWithValue("$fact", metric.SourceFactId);
        insert.Parameters.AddWithValue("$verification", (int)metric.Verification);
        await insert.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SourceFact>> GetFactsAtAsync(DateTimeOffset asOf, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT fact_id,document_id,security_id,claim,first_known_at,verification,supersedes_id FROM facts WHERE first_known_at <= $asof ORDER BY fact_id";
        command.Parameters.AddWithValue("$asof", asOf.ToUniversalTime().ToString("O"));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var facts = new List<SourceFact>();
        while (await reader.ReadAsync(cancellationToken))
            facts.Add(new SourceFact(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3),
                ParseTime(reader.GetString(4)), (VerificationState)reader.GetInt32(5), reader.IsDBNull(6) ? null : reader.GetString(6)));
        return facts;
    }

    public async Task<IReadOnlyList<CompanyMetric>> GetMetricsAtAsync(DateTimeOffset asOf, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT security_id,kind,value,first_known_at,source_fact_id,verification FROM metrics WHERE first_known_at <= $asof ORDER BY security_id,kind,first_known_at";
        command.Parameters.AddWithValue("$asof", asOf.ToUniversalTime().ToString("O"));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var metrics = new List<CompanyMetric>();
        while (await reader.ReadAsync(cancellationToken))
            metrics.Add(new CompanyMetric(reader.GetString(0), (CompanyMetricKind)reader.GetInt32(1),
                decimal.Parse(reader.GetString(2), CultureInfo.InvariantCulture), ParseTime(reader.GetString(3)),
                reader.GetString(4), (VerificationState)reader.GetInt32(5)));
        return metrics;
    }

    public async Task<IReadOnlyList<SourceDocument>> SearchDocumentsAsync(
        string securityId, string query, DateTimeOffset asOf, int limit = 8, CancellationToken cancellationToken = default)
    {
        if (limit < 1 || limit > 100) throw new ArgumentOutOfRangeException(nameof(limit));
        var words = Regex.Matches(query, "[\\p{L}\\p{N}]+", RegexOptions.CultureInvariant)
            .Select(m => m.Value).Take(12).ToArray();
        if (words.Length == 0) return [];
        string ftsQuery = string.Join(" OR ", words.Select(w => $"\"{w}\""));
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT d.document_id,d.security_id,d.source_url,d.publisher,d.published_at,d.first_known_at,
                   d.retrieved_at,d.content_hash,d.licence_id,d.parser_version,d.supersedes_id,d.content
            FROM document_search s JOIN documents d ON d.document_id=s.document_id
            WHERE document_search MATCH $query AND d.security_id=$security AND d.first_known_at <= $asof
            ORDER BY bm25(document_search),d.document_id LIMIT $limit
            """;
        command.Parameters.AddWithValue("$query", ftsQuery);
        command.Parameters.AddWithValue("$security", securityId);
        command.Parameters.AddWithValue("$asof", asOf.ToUniversalTime().ToString("O"));
        command.Parameters.AddWithValue("$limit", limit);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var documents = new List<SourceDocument>();
        while (await reader.ReadAsync(cancellationToken))
            documents.Add(new SourceDocument(reader.GetString(0), reader.GetString(1), new Uri(reader.GetString(2)),
                reader.GetString(3), ParseTime(reader.GetString(4)), ParseTime(reader.GetString(5)),
                ParseTime(reader.GetString(6)), reader.GetString(7), reader.GetString(8), reader.GetString(9),
                reader.IsDBNull(10) ? null : reader.GetString(10), reader.GetString(11)));
        return documents;
    }

    private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var pragma = connection.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys=ON;";
        await pragma.ExecuteNonQueryAsync(cancellationToken);
        return connection;
    }

    private static DateTimeOffset ParseTime(string value) =>
        DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    private static string Hash(string stream, int version, SimulationEvent item, string previousHash)
    {
        string value = $"{stream}\n{version}\n{item.Type}\n{item.OccurredAt.ToUniversalTime():O}\n{item.Json}\n{previousHash}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    }
}
