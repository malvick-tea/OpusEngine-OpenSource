using FluentAssertions;
using Opus.Foundation;
using Opus.Localisation;
using Xunit;

namespace Opus.Localisation.Tests.Catalog;

public sealed class TranslationKeyTests
{
    [Fact]
    public void Key_implicitly_converts_to_string()
    {
        TranslationKey k = TranslationKey.Of("menu.campaign");
        string s = k;
        s.Should().Be("menu.campaign");
    }

    [Fact]
    public void Empty_catalog_returns_key_as_text()
    {
        var c = EmptyCatalog.Instance;
        c.Has(TranslationKey.Of("anything")).Should().BeFalse();
        c.Get(TranslationKey.Of("anything")).Should().Be("anything");
    }

    [Fact]
    public void Empty_catalog_try_get_signals_missing()
    {
        var c = EmptyCatalog.Instance;
        var r = c.TryGet(TranslationKey.Of("missing"));
        r.IsErr.Should().BeTrue();
        r.UnwrapErr().Code.Should().Be(ErrorCode.TranslationKeyMissing);
    }
}
