using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Opus.Engine.Pal.Networking;

/// <summary>
/// Tiny HTTP transport contract. Used for CDN downloads, telemetry POSTs, and remote-config
/// fetches. NOT for in-match network code — that's <see cref="ISocketTransport"/>.
/// </summary>
public interface IHttpClient
{
    Task<HttpResponse> SendAsync(HttpRequest request, CancellationToken ct);
}

public sealed record HttpRequest(
    HttpMethod Method,
    Uri Url,
    System.Collections.Generic.IReadOnlyDictionary<string, string>? Headers = null,
    Stream? Body = null,
    TimeSpan? Timeout = null);

public sealed record HttpResponse(
    int StatusCode,
    System.Collections.Generic.IReadOnlyDictionary<string, string> Headers,
    Stream Body)
{
    public bool IsSuccess => StatusCode is >= 200 and < 300;
}

public enum HttpMethod
{
    Get,
    Post,
    Put,
    Delete,
    Patch,
    Head,
}
