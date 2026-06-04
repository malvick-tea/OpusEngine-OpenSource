namespace Opus.Content.Packaging.Tests.Fixtures;

/// <summary>Disposable temp directory for archive outputs and extraction targets.</summary>
internal sealed class TempDirectory : IDisposable
{
    private readonly DirectoryInfo _directory;

    public TempDirectory()
    {
        _directory = Directory.CreateTempSubdirectory("opus-archive-test-");
    }

    public string Path => _directory.FullName;

    public string Combine(string relative) => System.IO.Path.Combine(_directory.FullName, relative);

    public void Dispose()
    {
        try
        {
            _directory.Delete(recursive: true);
        }
        catch (IOException)
        {
            // Best-effort cleanup; a held handle must not mask the assertion failure.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
