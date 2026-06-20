using Opus.Engine.Consumer.Integration;

namespace Opus.App.OpusAlpha.Run.Consumer;

/// <summary>
/// Outcome of resolving the consumer integration for a CLI run: whether the host may proceed, the
/// integration to drive (null when no consumer was requested), and the failure reason when a
/// requested consumer could not be loaded.
/// </summary>
public sealed record ConsumerStartupResolution
{
    private ConsumerStartupResolution(
        bool canProceed,
        ConsumerIntegration? integration,
        string? failureReason,
        IDisposable? lifetime)
    {
        CanProceed = canProceed;
        Integration = integration;
        FailureReason = failureReason;
        Lifetime = lifetime;
    }

    /// <summary>True when the host may start (no consumer requested, or one loaded successfully).</summary>
    public bool CanProceed { get; }

    /// <summary>The integration to drive, or <see langword="null"/> when none was requested.</summary>
    public ConsumerIntegration? Integration { get; }

    /// <summary>The actionable reason a requested consumer could not be loaded; null otherwise.</summary>
    public string? FailureReason { get; }

    internal IDisposable? Lifetime { get; }

    internal static ConsumerStartupResolution ProceedWithout() =>
        new(canProceed: true, integration: null, failureReason: null, lifetime: null);

    internal static ConsumerStartupResolution ProceedWith(
        ConsumerIntegration integration,
        IDisposable? lifetime) =>
        new(canProceed: true, integration, failureReason: null, lifetime);

    internal static ConsumerStartupResolution Abort(string failureReason) =>
        new(canProceed: false, integration: null, failureReason, lifetime: null);
}

/// <summary>
/// CLI startup policy that turns the optional <c>--consumer</c> path into a
/// <see cref="ConsumerStartupResolution"/>: a blank or absent path means "run the built-in sample"
/// (proceed with no integration), a populated path is loaded through
/// <see cref="ConsumerIntegrationAssemblyLoader"/>, and a load failure aborts the run with the
/// loader's reason. Shared by every alpha-host mode that can drive a consumer (window, smoke).
/// </summary>
public static class ConsumerIntegrationStartup
{
    public const string TrustKeyEnvironmentVariable = "OPUS_CONSUMER_TRUST_KEY";

    /// <summary>Resolves the consumer integration for the supplied <paramref name="consumerAssemblyPath"/>.</summary>
    public static ConsumerStartupResolution Resolve(string? consumerAssemblyPath)
    {
        var trustKeyPath = System.Environment.GetEnvironmentVariable(TrustKeyEnvironmentVariable);
        return Resolve(consumerAssemblyPath, trustKeyPath);
    }

    /// <summary>Resolves a consumer against an explicit trust key. Primarily useful for
    /// embedding hosts and deterministic tests that do not read process environment state.</summary>
    public static ConsumerStartupResolution Resolve(
        string? consumerAssemblyPath,
        string? trustKeyPath)
    {
        if (string.IsNullOrWhiteSpace(consumerAssemblyPath))
        {
            return ConsumerStartupResolution.ProceedWithout();
        }

        if (string.IsNullOrWhiteSpace(trustKeyPath))
        {
            return ConsumerStartupResolution.Abort(
                $"Set {TrustKeyEnvironmentVariable} to the trusted P-256 public-key PEM before loading a consumer.");
        }

        var loaded = ConsumerIntegrationAssemblyLoader.Load(consumerAssemblyPath, trustKeyPath);
        return loaded is { Succeeded: true, Integration: { } integration }
            ? ConsumerStartupResolution.ProceedWith(integration, loaded.Lifetime)
            : ConsumerStartupResolution.Abort(loaded.FailureReason ?? "Consumer integration could not be loaded.");
    }
}
