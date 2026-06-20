using System;
using System.IO;
using FluentAssertions;
using Opus.App.OpusAlpha.Run.Consumer;
using Xunit;

namespace Opus.App.OpusAlpha.Tests.Run.Consumer;

public sealed class ConsumerIntegrationAssemblyLoaderTests
{
    private const string FixtureAssemblyFileName = "Opus.App.OpusAlpha.Tests.ConsumerPluginFixture.dll";

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Load_fails_when_path_is_blank(string path)
    {
        var result = ConsumerIntegrationAssemblyLoader.Load(path, "unused.pem");

        result.Succeeded.Should().BeFalse();
        result.FailureReason.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Load_fails_when_the_assembly_file_is_missing()
    {
        var missing = Path.Combine(Path.GetTempPath(), $"opus-missing-consumer-{Guid.NewGuid():N}.dll");

        var result = ConsumerIntegrationAssemblyLoader.Load(missing, "unused.pem");

        result.Succeeded.Should().BeFalse();
        result.FailureReason.Should().Contain("was not found");
    }

    [Fact]
    public void Load_fails_when_the_file_is_not_a_managed_assembly()
    {
        var bogus = Path.Combine(Path.GetTempPath(), $"opus-bogus-consumer-{Guid.NewGuid():N}.dll");
        File.WriteAllText(bogus, "this is plainly not a portable-executable image");
        try
        {
            using var trust = ConsumerPluginTrustFixture.Create(bogus);
            var result = ConsumerIntegrationAssemblyLoader.Load(
                trust.AssemblyPath,
                trust.PublicKeyPath);

            result.Succeeded.Should().BeFalse();
            result.FailureReason.Should().Contain("not a loadable managed assembly");
        }
        finally
        {
            File.Delete(bogus);
        }
    }

    [Fact]
    public void Load_builds_the_integration_from_the_fixture_plugin_assembly()
    {
        var fixturePath = Path.Combine(AppContext.BaseDirectory, FixtureAssemblyFileName);
        File.Exists(fixturePath).Should().BeTrue(
            $"the consumer plugin fixture must be copied beside the tests at '{fixturePath}'");

        using var trust = ConsumerPluginTrustFixture.Create(fixturePath);
        var result = ConsumerIntegrationAssemblyLoader.Load(
            trust.AssemblyPath,
            trust.PublicKeyPath);

        result.Succeeded.Should().BeTrue(result.FailureReason);
        result.Integration.Should().NotBeNull();
        result.Integration!.HasContracts.Should().BeTrue();
        result.Integration.LifecycleHooks.Should().HaveCount(1);
    }

    [Fact]
    public void Load_rejects_a_tampered_signed_assembly()
    {
        var fixturePath = Path.Combine(AppContext.BaseDirectory, FixtureAssemblyFileName);
        using var trust = ConsumerPluginTrustFixture.Create(fixturePath);
        using (var stream = File.OpenWrite(trust.AssemblyPath))
        {
            stream.Position = stream.Length;
            stream.WriteByte(0xFF);
        }

        var result = ConsumerIntegrationAssemblyLoader.Load(
            trust.AssemblyPath,
            trust.PublicKeyPath);

        result.Succeeded.Should().BeFalse();
        result.FailureReason.Should().Contain("signature verification failed");
    }
}
