using LoadStock.Core.Brands;
using LoadStock.Core.Model;

namespace LoadStock.Core.Tests;

public class UrlParsingTests
{
    [Fact]
    public void Zara_with_v1_uses_catalog_id()
    {
        var url = "https://www.zara.com/tr/tr/dokuma-blazer-ceket-p02557112.html?v1=514994130";
        Assert.True(UrlParsing.TryParseAny(url, out var r));
        Assert.Equal(Brand.Zara, r!.Brand);
        Assert.Equal("514994130", r.ProductId);
        Assert.False(r.NeedsCatalogResolve);
        Assert.Equal(url, r.SeoUrl);
    }

    [Fact]
    public void Zara_without_v1_flags_catalog_resolve()
    {
        var url = "https://www.zara.com/tr/tr/dokuma-blazer-ceket-p02557112.html";
        Assert.True(UrlParsing.TryParseAny(url, out var r));
        Assert.Equal(Brand.Zara, r!.Brand);
        Assert.Equal("02557112", r.ProductId);
        Assert.True(r.NeedsCatalogResolve);
    }

    [Fact]
    public void Bershka_extracts_product_and_color()
    {
        var url = "https://www.bershka.com/tr/double-sleeve-print-jumper-c0p212744460.html?colorId=251";
        Assert.True(UrlParsing.TryParseAny(url, out var r));
        Assert.Equal(Brand.Bershka, r!.Brand);
        Assert.Equal("212744460", r.ProductId);
        Assert.Equal("251", r.ColorId);
    }

    [Fact]
    public void Bershka_without_color_is_null()
    {
        var url = "https://www.bershka.com/tr/jumper-c0p212744460.html";
        Assert.True(UrlParsing.TryParseAny(url, out var r));
        Assert.Equal(Brand.Bershka, r!.Brand);
        Assert.Equal("212744460", r.ProductId);
        Assert.Null(r.ColorId);
    }

    [Fact]
    public void Stradivarius_new_format_uses_pelement_query()
    {
        var url = "https://www.stradivarius.com/tr/basic-fitted-gomlek-l06012703?categoryId=1020412944&colorId=001&pelement=475933969";
        Assert.True(UrlParsing.TryParseAny(url, out var r));
        Assert.Equal(Brand.Stradivarius, r!.Brand);
        Assert.Equal("475933969", r.ProductId);
        Assert.Equal("001", r.ColorId);
    }

    [Fact]
    public void Stradivarius_extracts_product_id_from_second_group()
    {
        var url = "https://www.stradivarius.com/tr/mom-jean-c1010274500p1234567.html";
        Assert.True(UrlParsing.TryParseAny(url, out var r));
        Assert.Equal(Brand.Stradivarius, r!.Brand);
        Assert.Equal("1234567", r.ProductId);
    }

    [Fact]
    public void Stradivarius_url_is_not_misparsed_by_bershka_pattern()
    {
        // Bershka kalıbı Stradivarius adresine de teknik olarak uyabilir; host kontrolü ayrımı sağlar.
        var url = "https://www.stradivarius.com/tr/jean-c1010274500p1234567.html";
        Assert.True(UrlParsing.TryParseStradivarius(url, out var r));
        Assert.Equal("1234567", r!.ProductId);
        Assert.False(UrlParsing.TryParseBershka(url, out _));
    }

    [Fact]
    public void Url_without_scheme_is_accepted()
    {
        var url = "www.bershka.com/tr/jumper-c0p999.html";
        Assert.True(UrlParsing.TryParseAny(url, out var r));
        Assert.Equal(Brand.Bershka, r!.Brand);
        Assert.Equal("999", r.ProductId);
    }

    [Fact]
    public void Leading_and_trailing_whitespace_is_trimmed()
    {
        var url = "   https://www.bershka.com/tr/x-c0p555.html  ";
        Assert.True(UrlParsing.TryParseAny(url, out var r));
        Assert.Equal("555", r!.ProductId);
    }

    [Theory]
    [InlineData("https://www.hm.com/tr/product-p123.html")]   // yabancı marka
    [InlineData("https://www.zara.com/tr/tr/")]               // ürün sayfası değil
    [InlineData("not a url")]
    [InlineData("")]
    public void Foreign_or_non_product_urls_fail(string url)
    {
        Assert.False(UrlParsing.TryParseAny(url, out _));
    }

    [Fact]
    public void Brand_specific_parser_rejects_other_brands()
    {
        var bershka = "https://www.bershka.com/tr/x-c0p1.html";
        Assert.False(UrlParsing.TryParseZara(bershka, out _));
        Assert.False(UrlParsing.TryParseStradivarius("https://www.bershka.com/tr/x-c0p1.html", out var _));
    }
}
