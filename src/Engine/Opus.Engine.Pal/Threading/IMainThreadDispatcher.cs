using System;
using System.Threading.Tasks;

namespace Opus.Engine.Pal.Threading;

/// <summary>
/// Marshals callbacks onto the main (render/UI) thread. Required because Raylib + most
/// graphics drivers are single-threaded affine, and Sim worker results must hop back
/// before they touch the renderer.
/// </summary>
public interface IMainThreadDispatcher
{
    /// <summary>True if the call site is already on the main thread.</summary>
    bool IsOnMainThread { get; }

    /// <summary>Posts a callback to the main thread queue. Returns immediately.</summary>
    void Post(Action callback);

    /// <summary>Posts and awaits completion. If already on the main thread, runs synchronously.</summary>
    Task InvokeAsync(Action callback);

    Task<T> InvokeAsync<T>(Func<T> callback);
}
