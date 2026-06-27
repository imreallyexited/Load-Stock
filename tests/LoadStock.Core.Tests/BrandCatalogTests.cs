using LoadStock.Core.Brands;
using LoadStock.Core.Model;

namespace LoadStock.Core.Tests;

public class BrandCatalogTests
{
    [Theory]
    [InlineData(Brand.Zara)]
    [InlineData(Brand.Bershka)]
    [InlineData(Brand.Stradivarius)]
    public void For_returns_matching_brand(Brand brand)
    {
        Assert.Equal(brand, BrandCatalog.For(brand).Brand);
    }

    [Fact]
    public void All_contains_three_turkish_try_configs()
    {
        Assert.Equal(3, BrandCatalog.All.Count);
        Assert.All(BrandCatalog.All, c => Assert.Equal("TRY", c.Currency));
        Assert.All(BrandCatalog.All, c => Assert.StartsWith("https://www.", c.Origin));
        Assert.All(BrandCatalog.All, c => Assert.Contains("/tr", c.WarmNavUrl));
    }

    [Fact]
    public void Bershka_has_static_store_and_region()
    {
        Assert.False(string.IsNullOrEmpty(BrandCatalog.Bershka.StoreId));
        Assert.False(string.IsNullOrEmpty(BrandCatalog.Bershka.RegionId));
        Assert.False(BrandCatalog.Bershka.StoreIdDynamic);
    }

    [Fact]
    public void Stradivarius_has_default_store_and_dynamic_flag()
    {
        // Varsayılan TR store/catalog vardır ama bölüme göre değişebileceği için dinamik çözüm bayrağı açıktır.
        Assert.True(BrandCatalog.Stradivarius.StoreIdDynamic);
        Assert.False(string.IsNullOrEmpty(BrandCatalog.Stradivarius.StoreId));
        Assert.False(string.IsNullOrEmpty(BrandCatalog.Stradivarius.CatalogId));
    }
}
