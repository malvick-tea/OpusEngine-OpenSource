using System;
using FluentAssertions;
using Opus.Content.Packaging.Manifest;
using Opus.Engine.AlphaHarness.Packaging;
using Opus.Foundation;
using Xunit;

namespace Opus.Engine.AlphaHarness.Tests.Packaging;

public sealed class AlphaPackageChecklistPolicyTests
{
    [Fact]
    public void Default_policy_targets_current_opus_identity_and_canonical_alpha_shape()
    {
        var policy = AlphaPackageChecklistPolicy.Default;

        policy.EngineProductName.Should().Be(EngineIdentity.Current.ProductName);
        policy.RequiredFeatures.Should().BeEquivalentTo(new[]
        {
            PackageFeatures.Models,
            PackageFeatures.Textures,
            PackageFeatures.Fonts,
            PackageFeatures.Localisation,
        });
        policy.RequiredLocales.Should().BeEquivalentTo(new[] { "en", "ru" });
    }

    [Fact]
    public void Default_policy_validates_cleanly()
    {
        AlphaPackageChecklistPolicy.Default.Invoking(p => p.Validate()).Should().NotThrow();
    }

    [Fact]
    public void Empty_product_name_is_rejected()
    {
        var policy = AlphaPackageChecklistPolicy.Default with { EngineProductName = "  " };

        policy.Invoking(p => p.Validate())
            .Should().Throw<ArgumentException>()
            .WithMessage("*EngineProductName*");
    }

    [Fact]
    public void Empty_feature_entry_is_rejected()
    {
        var policy = AlphaPackageChecklistPolicy.Default with
        {
            RequiredFeatures = new[] { PackageFeatures.Models, " " },
        };

        policy.Invoking(p => p.Validate())
            .Should().Throw<ArgumentException>()
            .WithMessage("*RequiredFeatures*");
    }

    [Fact]
    public void Empty_locale_entry_is_rejected()
    {
        var policy = AlphaPackageChecklistPolicy.Default with
        {
            RequiredLocales = new[] { "en", " " },
        };

        policy.Invoking(p => p.Validate())
            .Should().Throw<ArgumentException>()
            .WithMessage("*RequiredLocales*");
    }
}
