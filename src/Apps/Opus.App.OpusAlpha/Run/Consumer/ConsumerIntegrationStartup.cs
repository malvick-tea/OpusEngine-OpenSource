using Opus.Engine.Consumer.Integration;

namespace Opus.App.OpusAlpha.Run.Consumer;

/// <summary>
/// Outcome of resolving the consumer integration for a CLI run: whether the host may proceed, the
/// integration to drive (null when no consumer was requested), and the failure reason when a
/// requested consumer could not be loaded.
/// </summary>
public sealed record ConsumerStartupResolution
{
    private ConsumerStartupResolution(bool canProceed, ConsumerIntegration? integration, string? failureReason)
    {
        CanProceed = canProceed;
        Integration = integration;
        FailureReason = failureReason;
    }

    /// <summary>True when the host may start (no consumer requested, or one loaded successfully).</summary>
    public bool CanProceed { get; }

    /// <summary>The integration to drive, or <see langword="null"/> when none was requested.</summary>
    public ConsumerIntegration? Integration { get; }

    /// <summary>The actionable reason a requested consumer could not be loaded; null otherwise.</summary>
    public string? FailureReason { get; }

    internal static ConsumerStartupResolution ProceedWithout() => new(canProceed: true, integration: null, failureReason: null);

    internal static ConsumerStartupResolution ProceedWith(ConsumerIntegration integration) =>
        new(canProceed: true, integration, failureReason: null);

    internal static ConsumerStartupResolution Abort(string failureReason) =>
        new(canProceed: false, integration: null, failureReason);
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
    /// <summary>Resolves the consumer integration for the supplied <paramref name="consumerAssemblyPath"/>.</summary>
    public static ConsumerStartupResolution Resolve(string? consumerAssemblyPath)
    {
        if (string.IsNullOrWhiteSpace(consumerAssemblyPath))
        {
            return ConsumerStartupResolution.ProceedWithout();
        }

        var loaded = ConsumerIntegrationAssemblyLoader.Load(consumerAssemblyPath);
        return loaded is { Succeeded: true, Integration: { } integration }
            ? ConsumerStartupResolution.ProceedWith(integration)
            : ConsumerStartupResolution.Abort(loaded.FailureReason ?? "Consumer integration could not be loaded.");
    }
}
