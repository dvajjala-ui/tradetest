using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;
using TradeTest.Application;
using TradeTest.Domain;

namespace TradeTest.Infrastructure;

public sealed record StoredEvent(int Version, SimulationEvent Event, string PreviousHash, string Hash);

/// <summary>Local research and append-only simulation journal. No credential or order API access.</summary>
public sealed partial class SqliteStore(string databasePath, bool readOnly = false)
{
    private readonly string _connectionString = new SqliteConnectionStringBuilder
    {
        DataSource = databasePath,
        Mode = readOnly ? SqliteOpenMode.ReadOnly : SqliteOpenMode.ReadWriteCreate
    }.ToString();

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (readOnly) throw new InvalidOperationException("A read-only research store cannot initialize or migrate a database.");
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
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
        await InitializeResearchAsync(connection, cancellationToken);
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

        await using var insert = Command(connection, (SqliteTransaction)transaction, """
            INSERT INTO events(stream,version,type,occurred_at,json,previous_hash,event_hash)
            VALUES($stream,$version,$type,$at,$json,$previous,$hash)
            """, "$stream", "$version", "$type", "$at", "$json", "$previous", "$hash");
        foreach (var item in events)
        {
            int version = ++currentVersion;
            string hash = Hash(stream, version, item, previousHash);
            Values(insert, stream, version, item.Type, Utc(item.OccurredAt), item.Json, previousHash, hash);
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
