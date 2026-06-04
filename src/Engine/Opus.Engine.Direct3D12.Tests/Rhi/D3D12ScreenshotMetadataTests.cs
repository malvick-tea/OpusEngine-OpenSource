using System;
using System.Collections.Generic;
using FluentAssertions;
using Opus.Engine.Rhi.Direct3D12;
using Xunit;

namespace Opus.Engine.Direct3D12.Tests.Rhi;

public sealed class D3D12ScreenshotMetadataTests
{
    [Fact]
    public void Empty_singleton_has_no_entries()
    {
        D3D12ScreenshotMetadata.Empty.Entries.Should().BeEmpty();
        D3D12ScreenshotMetadata.Empty.Count.Should().Be(0);
    }

    [Fact]
    public void From_preserves_input_order()
    {
        var pairs = new[]
        {
            new KeyValuePair<string, string>("alpha", "one"),
            new KeyValuePair<string, string>("beta", "two"),
            new KeyValuePair<string, string>("gamma", "three"),
        };

        var metadata = D3D12ScreenshotMetadata.From(pairs);

        metadata.Entries.Length.Should().Be(3);
        metadata.Entries[0].Keyword.Should().Be("alpha");
        metadata.Entries[1].Keyword.Should().Be("beta");
        metadata.Entries[2].Keyword.Should().Be("gamma");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" leading")]
    [InlineData("trailing ")]
    [InlineData("contains\ttab")]
    [InlineData("contains\nnewline")]
    public void Invalid_keyword_is_rejected(string keyword)
    {
        Action act = () => D3D12ScreenshotMetadataEntry.Create(keyword, "value");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Keyword_longer_than_seventynine_is_rejected()
    {
        var keyword = new string('k', 80);
        Action act = () => D3D12ScreenshotMetadataEntry.Create(keyword, "value");
        act.Should().Throw<ArgumentException>().WithMessage("*79*");
    }

    [Fact]
    public void Value_with_non_latin1_codepoint_is_rejected()
    {
        Action act = () => D3D12ScreenshotMetadataEntry.Create("keyword", "Привет");
        act.Should().Throw<ArgumentException>().WithMessage("*Latin-1*");
    }

    [Fact]
    public void Latin1_value_with_accented_characters_is_accepted()
    {
        var entry = D3D12ScreenshotMetadataEntry.Create("keyword", "Café résumé naïve");
        entry.Value.Should().Be("Café résumé naïve");
    }
}
