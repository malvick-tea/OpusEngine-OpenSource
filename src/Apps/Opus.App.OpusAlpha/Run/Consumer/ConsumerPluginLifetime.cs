using System.Runtime.Loader;

namespace Opus.App.OpusAlpha.Run.Consumer;

/// <summary>Owns the collectible load context for one verified consumer plugin.</summary>
internal sealed class ConsumerPluginLifetime : IDisposable
{
    private AssemblyLoadContext? _context;

    public ConsumerPluginLifetime(AssemblyLoadContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public void Dispose()
    {
        Interlocked.Exchange(ref _context, null)?.Unload();
    }
}
