using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Opus.Engine.Pal.Filesystem;
using Opus.Foundation;
using Opus.Persistence;

namespace Opus.Engine.Pal.Windows.Persistence;

/// <summary>Windows <see cref="ISaveStore"/> backed by <see cref="IVfs"/>. Each save
/// slot maps to <c>user://saves/slot_N.gsav</c> (or <c>autosave.gsav</c> for slot 0).
/// Writes are atomic — <see cref="IVfs.WriteAllBytesAtomicAsync"/> commits the new body
/// or leaves the previous slot intact under the rename-temp-file primitive.</summary>
/// <remarks>
/// Pure delegation — the store doesn't know how the bytes are encoded. The
/// header/body framing is <see cref="SaveHeaderSerializer"/>'s job; callers ship the
/// framed byte stream straight into <see cref="WriteAsync"/>. The store stays codec-
/// agnostic so future zstd/HMAC layers slot in without touching this class.
/// </remarks>
public sealed class FileSaveStore : ISaveStore
{
    /// <summary>VFS subdirectory under <c>user://</c> where every slot lives.</summary>
    public const string SavesDirectory = "saves";

    /// <summary>File extension every slot file carries — ".gsav" disambiguates from
    /// settings.json / replay.grpl files in the same user root.</summary>
    public const string SaveFileExtension = ".gsav";

    /// <summary>Filename of slot 0 (the autosave). Manual slots use <c>slot_N.gsav</c>.</summary>
    public const string AutosaveFileName = "autosave" + SaveFileExtension;

    private readonly IVfs _vfs;

    public FileSaveStore(IVfs vfs)
    {
        ArgumentNullException.ThrowIfNull(vfs);
        _vfs = vfs;
    }

    public Task<Result<byte[]>> ReadAsync(SaveSlot slot, CancellationToken ct)
    {
        var path = ResolveSlotPath(slot);
        if (!_vfs.Exists(path))
        {
            return Task.FromResult(Result<byte[]>.Err(
                ErrorCode.NotFound, $"Save slot {slot} not found at {path}."));
        }

        try
        {
            using var stream = _vfs.OpenRead(path);
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            return Task.FromResult(Result<byte[]>.Ok(ms.ToArray()));
        }
        catch (IOException ex)
        {
            return Task.FromResult(Result<byte[]>.Err(
                new Error(ErrorCode.SaveIoFailed, $"Read failed for {path}.", ex)));
        }
    }

    public async Task<Result> WriteAsync(SaveSlot slot, byte[] body, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(body);
        var path = ResolveSlotPath(slot);
        try
        {
            await _vfs.WriteAllBytesAtomicAsync(path, body, ct).ConfigureAwait(false);
            return Result.Ok();
        }
        catch (OperationCanceledException)
        {
            return Result.Err(ErrorCode.Cancelled, $"Write to {path} cancelled.");
        }
        catch (IOException ex)
        {
            return Result.Err(new Error(ErrorCode.SaveIoFailed, $"Write failed for {path}.", ex));
        }
    }

    public Task<Result> DeleteAsync(SaveSlot slot, CancellationToken ct)
    {
        var path = ResolveSlotPath(slot);
        if (!_vfs.Exists(path))
        {
            return Task.FromResult(Result.Ok());
        }

        try
        {
            File.Delete(_vfs.Realize(path));
            return Task.FromResult(Result.Ok());
        }
        catch (IOException ex)
        {
            return Task.FromResult(Result.Err(
                new Error(ErrorCode.SaveIoFailed, $"Delete failed for {path}.", ex)));
        }
    }

    public Task<bool> ExistsAsync(SaveSlot slot, CancellationToken ct) =>
        Task.FromResult(_vfs.Exists(ResolveSlotPath(slot)));

    /// <summary>Maps a slot id to its <c>user://</c> VFS path. Visible for tests that
    /// want to assert the layout convention without going through the IVfs.</summary>
    public static string ResolveSlotPath(SaveSlot slot)
    {
        var fileName = slot.IsAutosave ? AutosaveFileName : $"slot_{slot.Index}{SaveFileExtension}";
        return $"user://{SavesDirectory}/{fileName}";
    }
}
