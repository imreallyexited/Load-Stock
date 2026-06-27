using LoadStock.Core.Diff;
using LoadStock.Core.Model;

namespace LoadStock.Core.Tests;

public class StateDifferTests
{
    [Theory]
    [InlineData(null, true, false)]   // ilk görülme, stokta -> tetikleme YOK
    [InlineData(null, false, false)]  // ilk görülme, tükenmiş -> yok
    [InlineData(false, true, true)]   // tükenmişti -> stokta -> RESTOCK
    [InlineData(true, true, false)]   // zaten stoktaydı -> yok
    [InlineData(false, false, false)] // tükenmişti -> hâlâ tükenmiş -> yok
    [InlineData(true, false, false)]  // stoktaydı -> tükendi -> yok
    public void IsRestock_truth_table(bool? prior, bool current, bool expected)
    {
        Assert.Equal(expected, StateDiffer.IsRestock(prior, current));
    }

    private static SizeAvailability Size(string id, bool inStock)
        => new(id, id, inStock, false, null, inStock ? "in_stock" : "out_of_stock");

    [Fact]
    public void Diff_fires_only_for_genuine_out_to_in_transition()
    {
        var prior = new Dictionary<string, bool> { ["a"] = false, ["b"] = true, ["c"] = false };
        var current = new[] { Size("a", true), Size("b", true), Size("c", false), Size("d", true) };

        var events = StateDiffer.Diff(prior, current, watchAnySize: true, watchedSizeIds: new HashSet<string>());

        Assert.Single(events);
        Assert.Equal("a", events[0].SizeId); // sadece a: false->true. b zaten stokta, c hâlâ tükenmiş, d ilk görülme.
    }

    [Fact]
    public void Diff_respects_size_filter()
    {
        var prior = new Dictionary<string, bool> { ["a"] = false, ["b"] = false };
        var current = new[] { Size("a", true), Size("b", true) };

        var events = StateDiffer.Diff(prior, current, watchAnySize: false, watchedSizeIds: new HashSet<string> { "b" });

        Assert.Single(events);
        Assert.Equal("b", events[0].SizeId);
    }

    [Fact]
    public void Diff_empty_when_no_watched_size_matches()
    {
        var prior = new Dictionary<string, bool> { ["a"] = false };
        var current = new[] { Size("a", true) };

        var events = StateDiffer.Diff(prior, current, watchAnySize: false, watchedSizeIds: new HashSet<string> { "zzz" });

        Assert.Empty(events);
    }
}
