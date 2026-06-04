namespace Opus.Engine.Consumer.Scene;

/// <summary>
/// Engine-neutral scene source implemented by an external consumer. The source declares
/// draw items, cameras, and lighting for a frame; the engine host renders the declaration
/// through its backend without owning any consumer rules.
/// </summary>
public interface IConsumerSceneSource
{
    /// <summary>Describes the scene for the supplied frame context.</summary>
    ConsumerSceneFrame DescribeFrame(ConsumerSceneFrameContext context);
}
