using TradeTest.Api;
using TradeTest.Infrastructure;

namespace TradeTest.Tests;

public sealed class DashboardTests
{
    [Fact]
    public void Anonymous_service_is_limited_to_loopback_synthetic_data()
    {
        var access = new ApiAccess(null);
        access.ValidateBindings("http://127.0.0.1:5080;http://localhost:5080", privateData: false);
        Assert.Throws<ArgumentException>(() => access.ValidateBindings("http://0.0.0.0:5080", privateData: false));
        Assert.Throws<ArgumentException>(() => access.ValidateBindings("http://127.0.0.1:5080", privateData: true));
        Assert.Throws<ArgumentException>(() => new ApiAccess("short"));
    }

    [Fact]
    public void Authenticated_service_requires_the_complete_bearer_token()
    {
        const string token = "sample-token-012345678901234567890123456789";
        var access = new ApiAccess(token);
        access.ValidateBindings("http://0.0.0.0:5080", privateData: true);
        Assert.False(access.Allows(null));
        Assert.False(access.Allows("Basic " + token));
        Assert.False(access.Allows("Bearer " + token[..^1]));
        Assert.True(access.Allows("Bearer " + token));
    }

    [Fact]
    public async Task Read_only_store_cannot_create_migrate_or_write_a_database()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".sqlite");
        try
        {
            var readOnly = new SqliteStore(path, readOnly: true);
            await Assert.ThrowsAsync<InvalidOperationException>(() => readOnly.InitializeAsync());
            Assert.False(File.Exists(path));
            var writable = new SqliteStore(path);
            await writable.InitializeAsync();
            await Assert.ThrowsAsync<Microsoft.Data.Sqlite.SqliteException>(() => readOnly.AppendEventsAsync("test", 0,
                [new("TEST", DateTimeOffset.UtcNow, "{}") ]));
            Assert.Empty(await readOnly.ReadEventsAsync("test"));
        }
        finally { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); File.Delete(path); File.Delete(path + "-wal"); File.Delete(path + "-shm"); }
    }
}
