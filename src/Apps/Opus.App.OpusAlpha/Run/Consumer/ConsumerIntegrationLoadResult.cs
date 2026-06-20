using Opus.Engine.Consumer.Integration;

namespace Opus.App.OpusAlpha.Run.Consumer;

/// <summary>
/// Outcome of loading a <see cref="ConsumerIntegration"/> from an external assembly. Either a
/// constructed integration (success) or a single actionable failure reason (the path is missing,
/// the file is not a managed assembly, the assembly exposes zero or several integration factories,
/// or the factory threw / returned null). Modelled as a plain result rather than thrown exceptions
/// so the alpha-host CLI boundary can report a precise message and exit code without a stack trace.
/// </summary>
public sealed record ConsumerIntegrationLoadResult
{
    private ConsumerIntegrationLoadResult(
        bool succeeded,
        ConsumerIntegration? integration,
        string? failureReason,
        IDisposable? lifetime)
    {
        Succeeded = succeeded;
        Integration = integration;
        FailureReason = failureReason;
        Lifetime = lifetime;
    }

    /// <summary>True when <see cref="Integration"/> was built; false when <see cref="FailureReason"/> is set.</summary>
    public bool Succeeded { get; }

    /// <summary>The constructed integration on success; <see langword="null"/> on failure.</summary>
    public ConsumerIntegration? Integration { get; }

    /// <summary>One-line, actionable explanation on failure; <see langword="null"/> on success.</summary>
    public string? FailureReason { get; }

    internal IDisposable? Lifetime { get; }

    /// <summary>Builds a success result around a constructed integration.</summary>
    public static ConsumerIntegrationLoadResult Success(ConsumerIntegration integration)
    {
        ArgumentNullException.ThrowIfNull(integration);
        return new ConsumerIntegrationLoadResult(
            succeeded: true,
            integration,
            failureReason: null,
            lifetime: null);
    }

    /// <summary>Builds a failure result around an actionable reason.</summary>
    public static ConsumerIntegrationLoadResult Failure(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        return new ConsumerIntegrationLoadResult(
            succeeded: false,
            integration: null,
            reason,
            lifetime: null);
    }

    internal ConsumerIntegrationLoadResult AttachLifetime(IDisposable lifetime)
    {
        ArgumentNullException.ThrowIfNull(lifetime);
        if (!Succeeded || Integration is null || Lifetime is not null)
        {
            throw new InvalidOperationException(
                "A consumer lifetime can only be attached once to a successful load.");
        }

        return new ConsumerIntegrationLoadResult(
            succeeded: true,
            Integration,
            failureReason: null,
            lifetime);
    }
}
