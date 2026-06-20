using System;
using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using MemoryPack;
using Opus.Foundation;
using Opus.Persistence;
using Xunit;

namespace Opus.Persistence.Tests;

/// <summary>Adversarial regression tests for the SaveHeaderSerializer
/// security hardening landed in Audit V2. Each test pins one of the
/// new properties (HKDF domain separation, fail-closed cross-domain
/// replay rejection) so a future refactor that accidentally
/// re-introduces the original vulnerability fails loudly.</summary>
[MemoryPackable]
public partial record DomainSeparationBody(string Label);

public sealed class SaveHeaderDomainRegressionTests
{
    private static readonly AppVersion SampleVersion = new(0, 1, 0, null, null);
    private static readonly byte[] InstallKey =
        SHA256.HashData(Encoding.UTF8.GetBytes("audit-v2-install-key"));

    private static readonly byte[] SettingsDomain =
        Encoding.UTF8.GetBytes("Opus.Save.settings.v1");

    private static readonly byte[] ProgressDomain =
        Encoding.UTF8.GetBytes("Opus.Save.progress.v1");

    [Fact]
    public void Frame_round_trips_under_matching_domain()
    {
        // Sanity check: the new domain parameter does not break the happy
        // path. A frame written under one domain must read back under the
        // same domain.
        IBinaryCodec codec = new MemoryPackCodec();
        var header = SaveHeader.Current(1, SampleVersion, unixMs: 1_700_000_000_000L);
        var body = new DomainSeparationBody("settings");

        var frame = SaveHeaderSerializer.WriteFrame(
            header, body, codec, InstallKey, SettingsDomain);

        var read = SaveHeaderSerializer.ReadFrame<DomainSeparationBody>(
            frame, codec, InstallKey, SettingsDomain);

        read.IsOk.Should().BeTrue();
        read.Unwrap().Body.Should().Be(body);
    }

    [Fact]
    public void Frame_written_under_one_domain_rejects_under_another()
    {
        // Domain separation: a settings frame MAC'd under
        // "Opus.Save.settings.v1" must NOT verify under
        // "Opus.Save.progress.v1". This closes the cross-blob replay
        // vector where a tampered settings frame could be replayed as a
        // progress frame (or vice versa) because the same install key
        // wraps both.
        IBinaryCodec codec = new MemoryPackCodec();
        var header = SaveHeader.Current(1, SampleVersion, unixMs: 1_700_000_000_000L);
        var body = new DomainSeparationBody("settings");

        var frame = SaveHeaderSerializer.WriteFrame(
            header, body, codec, InstallKey, SettingsDomain);

        var read = SaveHeaderSerializer.ReadFrame<DomainSeparationBody>(
            frame, codec, InstallKey, ProgressDomain);

        read.IsErr.Should().BeTrue("a frame MAC'd under one domain must not verify under another domain");
        read.UnwrapErr().Code.Should().Be(ErrorCode.SaveCorrupt);
    }

    [Fact]
    public void Default_domain_still_round_trips()
    {
        // Backward compat: the parameterless WriteFrame / ReadFrame
        // overloads use SaveHeaderSerializer.DefaultDomain and must
        // round-trip with each other. This protects any external caller
        // that has not yet been updated to pass an explicit domain.
        IBinaryCodec codec = new MemoryPackCodec();
        var header = SaveHeader.Current(1, SampleVersion, unixMs: 1_700_000_000_000L);
        var body = new DomainSeparationBody("legacy");

        var frame = SaveHeaderSerializer.WriteFrame(header, body, codec, InstallKey);
        var read = SaveHeaderSerializer.ReadFrame<DomainSeparationBody>(frame, codec, InstallKey);

        read.IsOk.Should().BeTrue();
        read.Unwrap().Body.Should().Be(body);
    }

    [Fact]
    public void Empty_domain_is_rejected_with_save_corrupt()
    {
        // Defensive: an empty domain must fail closed. HKDF-Expand with an
        // empty info buffer is technically valid but would let two blob
        // types accidentally collide if both forgot to pass a domain —
        // the API rejects the call instead of silently deriving a key
        // from a constant.
        IBinaryCodec codec = new MemoryPackCodec();
        var header = SaveHeader.Current(1, SampleVersion, unixMs: 0);
        var body = new DomainSeparationBody("x");

        var write = () => SaveHeaderSerializer.WriteFrame(
            header, body, codec, InstallKey, Array.Empty<byte>());

        write.Should().Throw<ArgumentException>();
    }
}
