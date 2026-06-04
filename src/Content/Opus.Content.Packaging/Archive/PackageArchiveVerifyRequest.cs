using System.Security.Cryptography;

namespace Opus.Content.Packaging.Archive;

/// <summary>
/// Inputs for <see cref="PackageArchiveVerifier"/>: the archive to verify and the trust policy.
/// Supply a <see cref="PublicKey"/> to check authenticity; set <see cref="RequireSignature"/> to
/// fail an unsigned package. The engine ships no keys — the trust anchor is caller-supplied.
/// </summary>
public sealed record PackageArchiveVerifyRequest(string ArchivePath)
{
    /// <summary>Optional trust anchor. When set, a present signature is verified against it.</summary>
    public ECDsa? PublicKey { get; init; }

    /// <summary>When true, an unsigned package fails verification. Requires <see cref="PublicKey"/>
    /// (otherwise there is nothing to verify against).</summary>
    public bool RequireSignature { get; init; }

    /// <summary>Container limits applied to the untrusted archive.</summary>
    public OpusPackageArchiveLimits Limits { get; init; } = OpusPackageArchiveLimits.Default;
}
