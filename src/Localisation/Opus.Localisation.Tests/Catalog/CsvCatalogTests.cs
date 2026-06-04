using System.IO;
using System.Text;
using FluentAssertions;
using Opus.Localisation;
using Xunit;

namespace Opus.Localisation.Tests.Catalog;

public sealed class CsvCatalogTests
{
    [Fact]
    public void Reads_simple_pairs_skipping_header()
    {
        var csv = """
            key,text
            menu.title,Opus
            menu.subtitle,Sample Subtitle
            """;

        var catalog = Read("en", csv);

        catalog.Locale.Should().Be("en");
        catalog.Get(TranslationKey.Of("menu.title")).Should().Be("Opus");
        catalog.Get(TranslationKey.Of("menu.subtitle")).Should().Be("Sample Subtitle");
        catalog.AllKeys.Should().HaveCount(2);
    }

    [Fact]
    public void Splits_on_first_comma_only_so_value_may_contain_commas()
    {
        var csv = "key,text\ngreeting.hello,Hello, world\n";

        var catalog = Read("en", csv);

        catalog.Get(TranslationKey.Of("greeting.hello")).Should().Be("Hello, world");
    }

    [Fact]
    public void Returns_key_string_when_translation_missing()
    {
        var catalog = Read("en", "key,text\nfoo,bar\n");

        catalog.Get(TranslationKey.Of("not.there")).Should().Be("not.there");
        catalog.Has(TranslationKey.Of("not.there")).Should().BeFalse();
        catalog.TryGet(TranslationKey.Of("not.there")).IsErr.Should().BeTrue();
    }

    [Fact]
    public void Skips_blank_and_commented_lines()
    {
        var csv = """
            key,text
            # comment

            real.key,real value
            """;

        var catalog = Read("en", csv);

        catalog.AllKeys.Should().ContainSingle().Which.Should().Be("real.key");
    }

    private static CsvCatalog Read(string locale, string content)
    {
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        return CsvCatalog.ReadFrom(locale, stream);
    }
}
