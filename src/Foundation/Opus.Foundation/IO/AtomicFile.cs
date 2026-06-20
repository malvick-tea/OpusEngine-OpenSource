using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Opus.Foundation.IO;

/// <summary>Crash-resistant same-directory file replacement with unpredictable staging names.</summary>
public static class AtomicFile
{
    private static readonly Encoding Utf8WithoutBom = new UTF8Encoding(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    public static void WriteAllText(string destinationPath, string content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        ArgumentNullException.ThrowIfNull(content);

        var fullPath = Path.GetFullPath(destinationPath);
        var directory = PrepareDirectory(fullPath);
        string? temporaryPath = null;
        try
        {
            using (var stream = OpenNewStagingStream(directory, out temporaryPath))
            using (var writer = new StreamWriter(
                       stream,
                       Utf8WithoutBom,
                       bufferSize: 4096,
                       leaveOpen: true))
            {
                writer.Write(content);
                writer.Flush();
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporaryPath, fullPath, overwrite: true);
        }
        finally
        {
            TryDelete(temporaryPath);
        }
    }

    public static async Task WriteAllBytesAsync(
        string destinationPath,
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);

        var fullPath = Path.GetFullPath(destinationPath);
        var directory = PrepareDirectory(fullPath);
        string? temporaryPath = null;
        try
        {
            await using (var stream = OpenNewStagingStream(directory, out temporaryPath))
            {
                await stream.WriteAsync(content, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                stream.Flush(flushToDisk: true);
            }

            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporaryPath, fullPath, overwrite: true);
        }
        finally
        {
            TryDelete(temporaryPath);
        }
    }

    public static void Copy(string sourcePath, string destinationPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);

        var fullSourcePath = Path.GetFullPath(sourcePath);
        var fullDestinationPath = Path.GetFullPath(destinationPath);
        var directory = PrepareDirectory(fullDestinationPath);
        string? temporaryPath = null;
        try
        {
            using (var source = new FileStream(
                       fullSourcePath,
                       FileMode.Open,
                       FileAccess.Read,
                       FileShare.Read,
                       bufferSize: 81920,
                       FileOptions.SequentialScan))
            using (var destination = OpenNewStagingStream(directory, out temporaryPath))
            {
                source.CopyTo(destination);
                destination.Flush(flushToDisk: true);
            }

            File.Move(temporaryPath, fullDestinationPath, overwrite: true);
        }
        finally
        {
            TryDelete(temporaryPath);
        }
    }

    private static FileStream OpenNewStagingStream(string directory, out string path)
    {
        for (var attempt = 0; attempt < 16; attempt++)
        {
            path = Path.Combine(directory, Path.GetRandomFileName());
            try
            {
                return new FileStream(
                    path,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 4096,
                    FileOptions.WriteThrough | FileOptions.Asynchronous);
            }
            catch (IOException) when (File.Exists(path) || Directory.Exists(path))
            {
            }
        }

        path = string.Empty;
        throw new IOException("Unable to allocate an atomic-write staging file.");
    }

    private static string PrepareDirectory(string fullPath)
    {
        var directory = Path.GetDirectoryName(fullPath)
            ?? throw new ArgumentException(
                "The destination path has no parent directory.",
                nameof(fullPath));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static void TryDelete(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
