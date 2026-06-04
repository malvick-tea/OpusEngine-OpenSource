namespace Opus.Engine.Consumer.Lifecycle;

/// <summary>
/// Engine-neutral lifecycle hooks for external consumers. Hooks are side-effect slots:
/// they let consumer state observe host startup, per-frame execution, and host stopping
/// without the engine owning any game rules.
/// </summary>
public interface IConsumerLifecycleHook
{
    /// <summary>Called after the host has started and logged its build identity.</summary>
    void OnStarted(ConsumerLifecycleStartedContext context);

    /// <summary>Called once per render frame before scene capture.</summary>
    void OnFrame(ConsumerFrameContext context);

    /// <summary>Called when the host is stopping, before the host tears down GPU resources.</summary>
    void OnStopping(ConsumerLifecycleStoppingContext context);
}
