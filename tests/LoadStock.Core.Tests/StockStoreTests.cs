using LoadStock.Core.Data;
using LoadStock.Core.Model;

namespace LoadStock.Core.Tests;

public class StockStoreTests : IDisposable
{
    private readonly string _dbPath;
    private readonly StockStore _store;

    public StockStoreTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), "stockload_test_" + Guid.NewGuid().ToString("N") + ".db");
        _store = new StockStore(_dbPath);
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try { File.Delete(_dbPath); } catch { }
    }

    [Fact]
    public void Add_is_idempotent_on_brand_product_color()
    {
        var pk1 = _store.AddOrUpdateProduct(Brand.Bershka, "209128229", "717", "https://x", "Ceket", true);
        var pk2 = _store.AddOrUpdateProduct(Brand.Bershka, "209128229", "717", "https://x", "Ceket", true);
        Assert.Equal(pk1, pk2);
        Assert.Single(_store.GetProducts());
    }

    [Fact]
    public void Watched_sizes_roundtrip()
    {
        var pk = _store.AddOrUpdateProduct(Brand.Zara, "562838470", null, "https://z", "Bermuda", false);
        _store.SetWatchedSizes(pk, new[] { "s1", "s2", "s2" });
        var got = _store.GetWatchedSizes(pk);
        Assert.Equal(2, got.Count);
        Assert.Contains("s1", got);
        Assert.Contains("s2", got);
    }

    [Fact]
    public void ApplyPoll_persists_state_and_event()
    {
        var pk = _store.AddOrUpdateProduct(Brand.Bershka, "1", "800", "https://b", "X", true);
        var current = new List<SizeAvailability>
        {
            new("a", "S", true, false, 159900, "in_stock"),
            new("b", "M", false, false, null, "out_of_stock"),
        };
        var events = new List<(string, string?, long?)> { ("a", "S", 159900) };

        _store.ApplyPoll(pk, current, events);

        var states = _store.GetLastStates(pk);
        Assert.True(states["a"].InStock);
        Assert.False(states["b"].InStock);
        Assert.Equal(159900, states["a"].PriceMinor);

        var hist = _store.GetRecentEvents();
        Assert.Single(hist);
        Assert.Equal("a", hist[0].SizeId);
        Assert.Equal("restock", hist[0].EventType);
        Assert.Equal(Brand.Bershka, hist[0].Brand);
        Assert.Equal("X", hist[0].ProductName);
    }

    [Fact]
    public void ApplyPoll_upserts_existing_state()
    {
        var pk = _store.AddOrUpdateProduct(Brand.Zara, "1", null, "https://z", "X", true);
        var noEvents = new List<(string, string?, long?)>();
        _store.ApplyPoll(pk, new List<SizeAvailability> { new("a", "S", false, false, null, "out_of_stock") }, noEvents);
        _store.ApplyPoll(pk, new List<SizeAvailability> { new("a", "S", true, false, null, "in_stock") }, noEvents);

        var states = _store.GetLastStates(pk);
        Assert.Single(states);
        Assert.True(states["a"].InStock);
    }

    [Fact]
    public void Remove_cascades_children()
    {
        var pk = _store.AddOrUpdateProduct(Brand.Bershka, "1", "800", "https://b", "X", true);
        _store.SetWatchedSizes(pk, new[] { "a" });
        _store.ApplyPoll(pk, new List<SizeAvailability> { new("a", "S", true, false, null, "in_stock") },
            new List<(string, string?, long?)> { ("a", "S", null) });

        _store.RemoveProduct(pk);

        Assert.Empty(_store.GetProducts());
        Assert.Empty(_store.GetWatchedSizes(pk));
        Assert.Empty(_store.GetLastStates(pk));
        Assert.Empty(_store.GetRecentEvents());
    }

    [Fact]
    public void Settings_roundtrip()
    {
        Assert.Null(_store.GetSetting("poll_interval_sec"));
        _store.SetSetting("poll_interval_sec", "300");
        Assert.Equal("300", _store.GetSetting("poll_interval_sec"));
        _store.SetSetting("poll_interval_sec", "180");
        Assert.Equal("180", _store.GetSetting("poll_interval_sec"));
    }
}
