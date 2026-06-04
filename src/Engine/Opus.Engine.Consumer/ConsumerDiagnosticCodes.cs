namespace Opus.Engine.Consumer;

/// <summary>
/// Stable diagnostic codes emitted by the consumer-integration surface. Codes are
/// append-only: never renumber or repurpose an entry once it has shipped to a tester
/// machine. The <c>OPDX-CSR-*</c> namespace covers consumer registration, callbacks,
/// asset resolution, scene capture, and telemetry capture.
/// </summary>
public static class ConsumerDiagnosticCodes
{
    /// <summary>Consumer integration registration was invalid at the host boundary.</summary>
    public const string RegistrationInvalid = "OPDX-CSR-001";

    /// <summary>Consumer asset catalog threw or returned an unusable asset resolution.</summary>
    public const string AssetCatalogFailed = "OPDX-CSR-002";

    /// <summary>Consumer scene source threw or returned an unusable frame description.</summary>
    public const string SceneSourceFailed = "OPDX-CSR-003";

    /// <summary>Consumer telemetry provider threw or returned an unusable snapshot.</summary>
    public const string TelemetryProviderFailed = "OPDX-CSR-004";

    /// <summary>Consumer lifecycle hook threw while the host was dispatching a lifecycle event.</summary>
    public const string LifecycleHookFailed = "OPDX-CSR-005";

    /// <summary>Consumer draw item references an asset id the current host bridge cannot map.</summary>
    public const string UnsupportedDrawAsset = "OPDX-CSR-006";
}
