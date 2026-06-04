namespace Opus.Engine.Consumer.Integration;

/// <summary>
/// Entry point an external consumer assembly exposes so an Opus host can discover and build
/// the consumer's <see cref="ConsumerIntegration"/> by reflection after loading the assembly
/// from disk. A host scans a loaded plugin assembly for the single public, concrete
/// implementation that has a public parameterless constructor, instantiates it, and calls
/// <see cref="CreateIntegration"/> once at startup.
/// </summary>
/// <remarks>
/// Implementations must be public, concrete (non-abstract), and declare a public parameterless
/// constructor. Exactly one implementation per plugin assembly is supported: zero or several are
/// reported as a load failure rather than guessed. The factory is the only reflection contract
/// between host and plugin — every other consumer contract is referenced through normal typed
/// engine assemblies that the host and plugin share by identity.
/// </remarks>
public interface IConsumerIntegrationFactory
{
    /// <summary>
    /// Builds the consumer registration facade the host drives. Called once after the plugin
    /// assembly is loaded. Returning <see langword="null"/> is treated as a load failure.
    /// </summary>
    ConsumerIntegration CreateIntegration();
}
